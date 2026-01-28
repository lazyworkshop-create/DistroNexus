namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for checking and managing application updates.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for available updates.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Update information if an update is available, null otherwise.</returns>
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    /// <returns>The current version string.</returns>
    string GetCurrentVersion();

    /// <summary>
    /// Opens the download page for the latest release.
    /// </summary>
    /// <param name="releaseUrl">The URL to the release page.</param>
    void OpenDownloadPage(string releaseUrl);
}

/// <summary>
/// Represents information about an available update.
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// Gets or sets the latest version available.
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current installed version.
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether an update is available.
    /// </summary>
    public bool IsUpdateAvailable { get; set; }

    /// <summary>
    /// Gets or sets the release notes or description.
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL to the release page.
    /// </summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the download URL for the update.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release date.
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets whether this is a pre-release version.
    /// </summary>
    public bool IsPreRelease { get; set; }
}
