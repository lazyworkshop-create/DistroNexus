using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace DistroNexus.Tests.UIAutomation;

public sealed class UiAutomationSession : IDisposable
{
    public Application Application { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }

    private UiAutomationSession(Application application, UIA3Automation automation, Window mainWindow)
    {
        Application = application;
        Automation = automation;
        MainWindow = mainWindow;
    }

    public static UiAutomationSession LaunchFromEnvironment()
    {
        var appPath = ResolveDesktopExecutablePath();
        var app = Application.Launch(appPath);
        var automation = new UIA3Automation();

        Retry.WhileNull(
            () => app.GetMainWindow(automation),
            timeout: TimeSpan.FromSeconds(30),
            ignoreException: true,
            throwOnTimeout: true,
            timeoutMessage: "Failed to locate DistroNexus main window in UI automation session.");

        var mainWindow = app.GetMainWindow(automation)
            ?? throw new InvalidOperationException("Main window was not available.");

        mainWindow.WaitUntilClickable();
        return new UiAutomationSession(app, automation, mainWindow);
    }

    public bool TryOpenTemplatesPage()
    {
        // Uses localized/English fallback for common dashboard navigation labels.
        var navTargetNames = new[] { "Templates", "Template", "模板" };

        foreach (var target in navTargetNames)
        {
            var candidate = MainWindow.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button)
                  .And(cf.ByName(target)))?.AsButton();

            if (candidate != null)
            {
                candidate.Invoke();
                return true;
            }
        }

        return false;
    }

    public bool TryOpenPackageManagerPage()
    {
        // Uses localized/English fallback for common dashboard navigation labels.
        var navTargetNames = new[] { "Package Manager", "Package", "软件包管理", "包管理" };

        foreach (var target in navTargetNames)
        {
            var candidate = MainWindow.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button)
                  .And(cf.ByName(target)))?.AsButton();

            if (candidate != null)
            {
                candidate.Invoke();
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        try
        {
            if (!IsProcessExited())
            {
                Application.Close();
                Application.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(2));
            }
        }
        catch
        {
            // ignored, force kill in finally
        }
        finally
        {
            if (!IsProcessExited())
            {
                try { Application.Kill(); } catch { }
            }

            Automation.Dispose();
            Application.Dispose();
        }
    }

    private bool IsProcessExited()
    {
        try
        {
            return Application.HasExited;
        }
        catch
        {
            // Can happen when process is already detached from handle.
            return true;
        }
    }

    private static string ResolveDesktopExecutablePath()
    {
        var configured = Environment.GetEnvironmentVariable("DISTRONEXUS_DESKTOP_EXE");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var root = FindRepositoryRoot(AppContext.BaseDirectory);
        var candidate = Path.Combine(
            root,
            "src",
            "Client",
            "DistroNexus.Desktop",
            "bin",
            "Debug",
            "net10.0-windows",
            "DistroNexus.Desktop.exe");

        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                "Could not locate DistroNexus desktop executable for UI automation. Build desktop first or set DISTRONEXUS_DESKTOP_EXE.",
                candidate);
        }

        return candidate;
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "docs")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for UI automation test session.");
    }
}
