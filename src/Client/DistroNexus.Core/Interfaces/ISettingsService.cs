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
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The loaded settings, or default settings if none exist.</returns>
    Task<GlobalSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the global application settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets settings to default values.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ResetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the settings file.
    /// </summary>
    /// <returns>The full path to the settings file.</returns>
    string GetSettingsPath();
}
