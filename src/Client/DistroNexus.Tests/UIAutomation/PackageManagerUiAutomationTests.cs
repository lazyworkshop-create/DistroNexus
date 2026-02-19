using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace DistroNexus.Tests.UIAutomation;

[Collection("UIAutomation")]
public class PackageManagerUiAutomationTests
{
    [Fact]
    [Trait("Category", "UIAutomation")]
    public void Open_PackageManager_Page_When_Navigation_Button_Present()
    {
        if (!ShouldRunUiAutomation())
        {
            return;
        }

        using var session = UiAutomationSession.LaunchFromEnvironment();
        var navigated = session.TryOpenPackageManagerPage();

        Assert.True(navigated, "Package Manager navigation button was not found in the current UI session.");

        var packagePageMarker = Retry.WhileNull(
            () => FindButtonByNames(session.MainWindow, "Refresh", "刷新"),
            timeout: TimeSpan.FromSeconds(10),
            throwOnTimeout: false).Result;

        Assert.NotNull(packagePageMarker);
    }

    [Fact]
    [Trait("Category", "UIAutomation")]
    public void Start_Download_Should_Show_Download_In_Progress_Controls()
    {
        if (!ShouldRunUiAutomation())
        {
            return;
        }

        Environment.SetEnvironmentVariable("DISTRONEXUS_UI_AUTOMATION_FAKE_DOWNLOAD", "1");

        try
        {
            using var session = UiAutomationSession.LaunchFromEnvironment();
            var navigated = session.TryOpenPackageManagerPage();

            Assert.True(navigated, "Package Manager navigation button was not found in the current UI session.");

            Retry.WhileNull(
                () => FindButtonByNames(session.MainWindow, "Refresh", "刷新"),
                timeout: TimeSpan.FromSeconds(10),
                throwOnTimeout: true,
                timeoutMessage: "Package Manager page did not become ready.");

            var downloadButton = Retry.WhileNull(
                () => FindDownloadCandidateButton(session.MainWindow),
                timeout: TimeSpan.FromSeconds(20),
                throwOnTimeout: false).Result;

            Assert.NotNull(downloadButton);

            downloadButton!.Invoke();

            var downloadingStateReached = Retry.WhileFalse(
                () => HasDownloadInProgressControls(session.MainWindow),
                timeout: TimeSpan.FromSeconds(20),
                throwOnTimeout: false).Result;

            Assert.True(downloadingStateReached, "Download-in-progress controls (cancel/progress/status) were not detected after clicking Download.");

            var completionReached = Retry.WhileFalse(
                () => HasCompletedStateControls(session.MainWindow),
                timeout: TimeSpan.FromSeconds(20),
                throwOnTimeout: false).Result;

            Assert.True(completionReached, "Download completion controls (completed/install) were not detected after simulated download.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISTRONEXUS_UI_AUTOMATION_FAKE_DOWNLOAD", null);
        }
    }

    private static Button? FindDownloadCandidateButton(Window window)
    {
        var candidates = window.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.Button)
                  .And(cf.ByAutomationId("PackageItemDownloadButton")))
            .Select(element => element.AsButton())
            .Where(button => button.IsEnabled)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates.FirstOrDefault();
    }

    private static bool HasDownloadInProgressControls(Window window)
    {
        var progressBars = window.FindAllDescendants(cf =>
            cf.ByControlType(ControlType.ProgressBar)
              .And(cf.ByAutomationId("PackageItemProgressBar")));
        if (progressBars.Length == 0)
        {
            return false;
        }

        var cancelButton = window.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button)
              .And(cf.ByAutomationId("PackageItemCancelButton")));
        var speedText = window.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text)
              .And(cf.ByAutomationId("PackageItemSpeedText")));
                var completedText = window.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.Text)
              .And(cf.ByAutomationId("PackageItemStatusText")));

        var hasCancel = cancelButton is not null;
        var hasSpeedText = speedText is not null;
        var hasStatusText = completedText is not null;

        return hasCancel || hasSpeedText || hasStatusText;
    }

    private static bool HasCompletedStateControls(Window window)
    {
        var statusText = window.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text)
              .And(cf.ByAutomationId("PackageItemStatusText")));

        if (statusText != null)
        {
            var name = statusText.Name ?? string.Empty;
            if (name.Contains("Completed", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("完成", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var installButton = window.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button)
              .And(cf.ByAutomationId("PackageItemInstallButton")));

        return installButton != null;
    }

    private static Button? FindButtonByNames(Window window, params string[] names)
    {
        foreach (var name in names)
        {
            var button = window.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button)
                  .And(cf.ByName(name)))?.AsButton();

            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static bool ShouldRunUiAutomation()
    {
        return string.Equals(Environment.GetEnvironmentVariable("DISTRONEXUS_RUN_UI_AUTOMATION"), "1", StringComparison.OrdinalIgnoreCase);
    }
}
