using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for loading and saving application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the global application settings.
    /// </summary>
    /// <returns>The loaded settings, or default settings if none exist.</returns>
    GlobalSettings LoadSettings();

    /// <summary>
    /// Saves the global application settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    void SaveSettings(GlobalSettings settings);

    /// <summary>
    /// Resets settings to default values.
    /// </summary>
    void ResetSettings();

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    /// <returns>The full path to the settings file.</returns>
    string GetSettingsPath();
}
