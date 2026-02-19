using System.Drawing;
using System.Drawing.Imaging;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace DistroNexus.Tests.UIAutomation;

internal static class ScreenshotVerifier
{
    private const string UpdateBaselinesFlag = "DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES";

    public static void VerifyWindow(Window window, string snapshotName, double maxDiffRatio = 0.001)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        Retry.WhileException(
            () =>
            {
                var _ = window.BoundingRectangle;
            },
            timeout: TimeSpan.FromSeconds(3),
            throwOnTimeout: true);

        var bounds = window.BoundingRectangle;
        var left = Math.Max(0, (int)Math.Round(Convert.ToDouble(bounds.Left)));
        var top = Math.Max(0, (int)Math.Round(Convert.ToDouble(bounds.Top)));
        var width = Math.Max(1, (int)Math.Round(Convert.ToDouble(bounds.Width)));
        var height = Math.Max(1, (int)Math.Round(Convert.ToDouble(bounds.Height)));

        using var actualBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(actualBitmap))
        {
            graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        var outputRoot = Path.Combine(AppContext.BaseDirectory, "TestResults", "UIAutomationScreenshots");
        var actualDir = Path.Combine(outputRoot, "actual");
        var diffDir = Path.Combine(outputRoot, "diff");
        Directory.CreateDirectory(actualDir);
        Directory.CreateDirectory(diffDir);

        var baselineDir = Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "src", "Client", "DistroNexus.Tests", "UIAutomation", "Baselines");
        Directory.CreateDirectory(baselineDir);

        var actualPath = Path.Combine(actualDir, $"{snapshotName}.png");
        var baselinePath = Path.Combine(baselineDir, $"{snapshotName}.png");
        var diffPath = Path.Combine(diffDir, $"{snapshotName}.diff.png");

        actualBitmap.Save(actualPath, ImageFormat.Png);

        var shouldUpdateBaselines = string.Equals(Environment.GetEnvironmentVariable(UpdateBaselinesFlag), "1", StringComparison.OrdinalIgnoreCase);
        if (shouldUpdateBaselines || !File.Exists(baselinePath))
        {
            actualBitmap.Save(baselinePath, ImageFormat.Png);
        }

        Assert.True(File.Exists(baselinePath),
            $"Missing screenshot baseline: {baselinePath}. Set {UpdateBaselinesFlag}=1 and rerun to create it.");

        using var baselineBitmap = new Bitmap(baselinePath);
        Assert.True(baselineBitmap.Width == actualBitmap.Width && baselineBitmap.Height == actualBitmap.Height,
            $"Baseline size mismatch for '{snapshotName}'. Expected {baselineBitmap.Width}x{baselineBitmap.Height}, actual {actualBitmap.Width}x{actualBitmap.Height}. " +
            $"Set {UpdateBaselinesFlag}=1 to refresh baseline.");

        using var diffBitmap = new Bitmap(actualBitmap.Width, actualBitmap.Height, PixelFormat.Format32bppArgb);
        var comparedPixels = actualBitmap.Width * actualBitmap.Height;
        var changedPixels = 0;

        for (var y = 0; y < actualBitmap.Height; y++)
        {
            for (var x = 0; x < actualBitmap.Width; x++)
            {
                var expected = baselineBitmap.GetPixel(x, y);
                var actual = actualBitmap.GetPixel(x, y);
                if (IsDifferent(expected, actual))
                {
                    changedPixels++;
                    diffBitmap.SetPixel(x, y, Color.Red);
                }
                else
                {
                    diffBitmap.SetPixel(x, y, actual);
                }
            }
        }

        var diffRatio = comparedPixels == 0 ? 0 : (double)changedPixels / comparedPixels;
        if (diffRatio > maxDiffRatio)
        {
            diffBitmap.Save(diffPath, ImageFormat.Png);
        }

        Assert.True(
            diffRatio <= maxDiffRatio,
            $"Screenshot regression detected for '{snapshotName}'. Diff ratio: {diffRatio:P4}, allowed: {maxDiffRatio:P4}. " +
            $"baseline={baselinePath}; actual={actualPath}; diff={diffPath}");
    }

    private static bool IsDifferent(Color expected, Color actual)
    {
        const int perChannelTolerance = 8;
        return Math.Abs(expected.R - actual.R) > perChannelTolerance ||
               Math.Abs(expected.G - actual.G) > perChannelTolerance ||
               Math.Abs(expected.B - actual.B) > perChannelTolerance ||
               Math.Abs(expected.A - actual.A) > perChannelTolerance;
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for screenshot baseline verification.");
    }
}