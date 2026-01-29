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
        // Return cached settings if available (fast path)
        if (_cachedSettings != null)
        {
            _logger.LogDebug("Returning cached settings");
            return _cachedSettings;
        }

        _logger.LogInformation("LoadSettingsAsync called - returning default settings immediately (lazy load mode)");

        // STARTUP OPTIMIZATION: Return default settings immediately without file I/O
        // This prevents any blocking during application startup
        _cachedSettings = new GlobalSettings();

        // Schedule background loading (fire-and-forget)
        // This will load the actual settings file after startup completes
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to ensure UI is fully initialized
                await Task.Delay(1000);

                _logger.LogDebug("Background settings load starting...");

                // Check if settings file exists
                if (!File.Exists(_settingsPath))
                {
                    _logger.LogInformation("Settings file not found at {SettingsPath}, using defaults", _settingsPath);

                    // Try to save default settings in background
                    try
                    {
                        await SaveSettingsInternalAsync(_cachedSettings, CancellationToken.None);
                        _logger.LogDebug("Default settings saved successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save default settings");
                    }

                    return;
                }

                // Load settings from file in background
                var loadedSettings = await LoadSettingsFromFileAsync(CancellationToken.None);

                if (loadedSettings != null)
                {
                    _cachedSettings = loadedSettings;
                    _logger.LogInformation("Settings loaded successfully in background from {SettingsPath}", _settingsPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background settings load failed, keeping defaults");
            }
        });

        return _cachedSettings;
    }

    /// <summary>
    /// Internal method to load settings from file with timeout protection.
    /// </summary>
    private async Task<GlobalSettings?> LoadSettingsFromFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Loading settings from {SettingsPath}", _settingsPath);

            var loadTask = Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Starting synchronous file read...");

                    var fileInfo = new FileInfo(_settingsPath);
                    _logger.LogDebug("Settings file size: {FileSize} bytes", fileInfo.Length);

                    if (fileInfo.Length > 10 * 1024 * 1024) // 10 MB
                    {
                        _logger.LogWarning("Settings file is unusually large: {FileSize} bytes", fileInfo.Length);
                    }

                    string json = File.ReadAllText(_settingsPath);
                    _logger.LogDebug("Settings file read successfully, length: {JsonLength} characters", json.Length);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogWarning("Settings file is empty");
                        return null;
                    }

                    _logger.LogDebug("Deserializing JSON...");
                    var settings = JsonSerializer.Deserialize<GlobalSettings>(json);
                    _logger.LogDebug("JSON deserialization completed");

                    return settings;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON format in settings file");

                    // Backup corrupted file
                    try
                    {
                        var backupPath = _settingsPath + ".corrupted." + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(_settingsPath, backupPath, true);
                        _logger.LogInformation("Corrupted settings file backed up to {BackupPath}", backupPath);
                    }
                    catch (Exception backupEx)
                    {
                        _logger.LogWarning(backupEx, "Failed to backup corrupted settings file");
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading/parsing settings file");
                    return null;
                }
            }, cancellationToken);

            // Apply timeout
            try
            {
                return await loadTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout loading settings after 3 seconds");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {SettingsPath}", _settingsPath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default)
    {
        await SaveSettingsInternalAsync(settings, cancellationToken);
    }

    /// <summary>
    /// Internal method to save settings to file with timeout protection.
    /// </summary>
    private async Task SaveSettingsInternalAsync(GlobalSettings settings, CancellationToken cancellationToken)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        try
        {
            _logger.LogInformation("Saving settings to {SettingsPath}", _settingsPath);

            var saveTask = Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Serializing settings to JSON...");

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    string json = JsonSerializer.Serialize(settings, options);
                    _logger.LogDebug("JSON serialization completed, length: {JsonLength} characters", json.Length);

                    _logger.LogDebug("Writing settings to file...");
                    File.WriteAllText(_settingsPath, json);
                    _logger.LogDebug("Settings file written successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error serializing/writing settings file");
                    throw;
                }
            }, cancellationToken);

            try
            {
                await saveTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
                _cachedSettings = settings;
                _logger.LogInformation("Settings saved successfully to {SettingsPath}", _settingsPath);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Timeout saving settings after 3 seconds");
                throw new TimeoutException($"Failed to save settings file within 3 seconds: {_settingsPath}");
            }
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {SettingsPath}", _settingsPath);
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
