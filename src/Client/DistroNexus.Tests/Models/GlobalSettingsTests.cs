using DistroNexus.Core.Models;

namespace DistroNexus.Tests.Models;

public class GlobalSettingsTests
{
    [Fact]
    public void GlobalSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new GlobalSettings();

        // Assert
        Assert.Equal(@"C:\WSL", settings.DefaultInstallPath);
        Assert.Equal(2, settings.DefaultWslVersion);
        Assert.Equal("root", settings.DefaultUsername);
        Assert.Equal("~", settings.TerminalStartPath);
        Assert.True(settings.EnableLogging);
        Assert.True(settings.CheckUpdatesOnStartup);
    }

    [Fact]
    public void GlobalSettings_SetCustomValues_WorkCorrectly()
    {
        // Arrange
        var settings = new GlobalSettings
        {
            DefaultInstallPath = @"D:\WSL",
            DefaultWslVersion = 1,
            DefaultUsername = "admin",
            TerminalStartPath = "/home/admin",
            EnableLogging = false,
            CheckUpdatesOnStartup = false,
            CatalogUrl = "https://custom.example.com/distros.json"
        };

        // Assert
        Assert.Equal(@"D:\WSL", settings.DefaultInstallPath);
        Assert.Equal(1, settings.DefaultWslVersion);
        Assert.Equal("admin", settings.DefaultUsername);
        Assert.Equal("/home/admin", settings.TerminalStartPath);
        Assert.False(settings.EnableLogging);
        Assert.False(settings.CheckUpdatesOnStartup);
        Assert.Equal("https://custom.example.com/distros.json", settings.CatalogUrl);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GlobalSettings_WslVersion_AcceptsValidValues(int version)
    {
        // Arrange & Act
        var settings = new GlobalSettings { DefaultWslVersion = version };

        // Assert
        Assert.Equal(version, settings.DefaultWslVersion);
    }
}
