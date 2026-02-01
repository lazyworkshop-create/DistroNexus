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
        
        // Standard Path Strategy: Always use AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _settingsPath = Path.Combine(appFolder, "settings.json");
        _logger.LogInformation("Using Standard configuration at: {ConfigPath}", appFolder);
    }

    /// <inheritdoc/>
    public GlobalSettings LoadSettings()
    {
        // Return cached settings if available
        if (_cachedSettings != null)
        {
            _logger.LogDebug("Returning cached settings");
            return _cachedSettings;
        }

        _logger.LogInformation("Loading settings from {SettingsPath}", _settingsPath);

        try
        {
            // Check if settings file exists
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("Settings file not found, creating default settings");
                _cachedSettings = new GlobalSettings();

                // Save default settings
                SaveSettings(_cachedSettings);
                return _cachedSettings;
            }

            // Read and parse settings file
            var json = File.ReadAllText(_settingsPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Settings file is empty, using defaults");
                _cachedSettings = new GlobalSettings();
                return _cachedSettings;
            }

            _cachedSettings = JsonSerializer.Deserialize<GlobalSettings>(json) ?? new GlobalSettings();
            _logger.LogInformation("Settings loaded successfully");

            return _cachedSettings;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in settings file, backing up and using defaults");

            // Backup corrupted file
            try
            {
                var backupPath = $"{_settingsPath}.corrupted.{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(_settingsPath, backupPath, overwrite: true);
                _logger.LogInformation("Corrupted settings backed up to {BackupPath}", backupPath);
            }
            catch (Exception backupEx)
            {
                _logger.LogWarning(backupEx, "Failed to backup corrupted settings");
            }

            _cachedSettings = new GlobalSettings();
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
    public void SaveSettings(GlobalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            _logger.LogInformation("Saving settings to {SettingsPath}", _settingsPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsPath, json);

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
    public void ResetSettings()
    {
        try
        {
            _logger.LogInformation("Resetting settings to defaults");

            var defaultSettings = new GlobalSettings();
            SaveSettings(defaultSettings);

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
