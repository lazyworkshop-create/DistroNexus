using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for managing application settings.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private GlobalSettings? _cachedSettings;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    /// <inheritdoc/>
    public async Task<GlobalSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("Settings file not found, creating default settings");
                _cachedSettings = new GlobalSettings();
                await SaveSettingsAsync(_cachedSettings, cancellationToken);
                return _cachedSettings;
            }

            _logger.LogInformation("Loading settings from {SettingsPath}", _settingsPath);

            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
            _cachedSettings = JsonSerializer.Deserialize<GlobalSettings>(json) ?? new GlobalSettings();

            _logger.LogInformation("Settings loaded successfully");
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            _cachedSettings = new GlobalSettings();
            return _cachedSettings;
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        try
        {
            _logger.LogInformation("Saving settings to {SettingsPath}", _settingsPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsPath, json, cancellationToken);

            _cachedSettings = settings;
            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ResetSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resetting settings to defaults");

            var defaultSettings = new GlobalSettings();
            await SaveSettingsAsync(defaultSettings, cancellationToken);

            _logger.LogInformation("Settings reset successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            throw;
        }
    }

    /// <inheritdoc/>
    public string GetSettingsPath()
    {
        return _settingsPath;
    }
}
