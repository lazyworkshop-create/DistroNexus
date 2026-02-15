using FlaUI.Core.AutomationElements;

namespace DistroNexus.Tests.UIAutomation;

public class TemplateUiAutomationSmokeTests
{
    [Fact]
    [Trait("Category", "UIAutomation")]
    public void Launch_App_And_Validate_Main_Window()
    {
        if (!ShouldRunUiAutomation())
        {
            return;
        }

        using var session = UiAutomationSession.LaunchFromEnvironment();

        Assert.False(string.IsNullOrWhiteSpace(session.MainWindow.Title));
    }

    [Fact]
    [Trait("Category", "UIAutomation")]
    public void Open_Templates_Page_When_Navigation_Button_Present()
    {
        if (!ShouldRunUiAutomation())
        {
            return;
        }

        using var session = UiAutomationSession.LaunchFromEnvironment();
        var navigated = session.TryOpenTemplatesPage();

        Assert.True(navigated, "Templates navigation button was not found in the current UI session.");
    }

    private static bool ShouldRunUiAutomation()
    {
        return string.Equals(Environment.GetEnvironmentVariable("DISTRONEXUS_RUN_UI_AUTOMATION"), "1", StringComparison.OrdinalIgnoreCase);
    }
}
