using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly Mock<ILogger<SettingsService>> _mockLogger;
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly bool _hadSettings;

    public SettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<SettingsService>>();

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
        _backupPath = Path.Combine(appFolder, $"settings.json.test-backup-{Guid.NewGuid():N}");
        _hadSettings = File.Exists(_settingsPath);

        if (_hadSettings)
        {
            File.Copy(_settingsPath, _backupPath, true);
        }

        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_hadSettings && File.Exists(_backupPath))
            {
                File.Copy(_backupPath, _settingsPath, true);
                File.Delete(_backupPath);
            }
            else if (!_hadSettings && File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void LoadSettings_WhenFileNotExists_ReturnsDefaultSettings()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act
        var settings = service.LoadSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(@"C:\WSL", settings.DefaultInstallPath);
        Assert.Equal(2, settings.DefaultWslVersion);
    }

    [Fact]
    public void LoadSettings_CalledTwice_ReturnsCachedSettings()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act
        var settings1 = service.LoadSettings();
        var settings2 = service.LoadSettings();

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
    public void SaveSettings_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            service.SaveSettings(null!));
    }

    [Fact]
    public void SaveSettings_WithValidSettings_CompletesSuccessfully()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);
        var settings = new GlobalSettings
        {
            DefaultInstallPath = @"D:\Test",
            DefaultUsername = "testuser"
        };

        // Act - should not throw
        service.SaveSettings(settings);

        // Assert - Load and verify
        var loadedSettings = service.LoadSettings();
        Assert.Equal(@"D:\Test", loadedSettings.DefaultInstallPath);
        Assert.Equal("testuser", loadedSettings.DefaultUsername);
    }

    [Fact]
    public void ResetSettings_ResetsToDefaults()
    {
        // Arrange
        var service = new SettingsService(_mockLogger.Object);
        var customSettings = new GlobalSettings
        {
            DefaultInstallPath = @"X:\Custom\Path",
            DefaultUsername = "custom"
        };
        service.SaveSettings(customSettings);

        // Act
        service.ResetSettings();
        var settings = service.LoadSettings();

        // Assert
        Assert.Equal(@"C:\WSL", settings.DefaultInstallPath);
        Assert.Equal("root", settings.DefaultUsername);
    }
}
