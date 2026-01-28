using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

public class SettingsServiceTests
{
    private readonly Mock<ILogger<SettingsService>> _mockLogger;

    public SettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<SettingsService>>();
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileNotExists_ReturnsDefaultSettings()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act
        var settings = await service.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(@"C:\WSL", settings.DefaultInstallPath);
        Assert.Equal(2, settings.DefaultWslVersion);
    }

    [Fact]
    public async Task LoadSettingsAsync_CalledTwice_ReturnsCachedSettings()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act
        var settings1 = await service.LoadSettingsAsync();
        var settings2 = await service.LoadSettingsAsync();

        // Assert
        Assert.Same(settings1, settings2);
    }

    [Fact]
    public void GetSettingsPath_ReturnsValidPath()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act
        var path = service.GetSettingsPath();

        // Assert
        Assert.NotNull(path);
        Assert.Contains("DistroNexus", path);
        Assert.EndsWith("settings.json", path);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            service.SaveSettingsAsync(null!));
    }

    [Fact]
    public async Task SaveSettingsAsync_WithValidSettings_CompletesSuccessfully()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);
        var settings = new GlobalSettings
        {
            DefaultInstallPath = @"D:\Test",
            DefaultUsername = "testuser"
        };

        // Act - should not throw
        await service.SaveSettingsAsync(settings);

        // Assert - Load and verify
        var loadedSettings = await service.LoadSettingsAsync();
        Assert.Equal(@"D:\Test", loadedSettings.DefaultInstallPath);
        Assert.Equal("testuser", loadedSettings.DefaultUsername);
    }

    [Fact]
    public async Task ResetSettingsAsync_ResetsToDefaults()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);
        var customSettings = new GlobalSettings
        {
            DefaultInstallPath = @"X:\Custom\Path",
            DefaultUsername = "custom"
        };
        await service.SaveSettingsAsync(customSettings);

        // Act
        await service.ResetSettingsAsync();
        var settings = await service.LoadSettingsAsync();

        // Assert
        Assert.Equal(@"C:\WSL", settings.DefaultInstallPath);
        Assert.Equal("root", settings.DefaultUsername);
    }
}
