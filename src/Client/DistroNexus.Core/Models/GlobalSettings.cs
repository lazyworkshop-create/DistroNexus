namespace DistroNexus.Core.Models;

/// <summary>
/// Represents the global application settings.
/// </summary>
public class GlobalSettings
{
    /// <summary>
    /// Gets or sets the default installation path for new WSL instances.
    /// </summary>
    public string DefaultInstallPath { get; set; } = @"C:\WSL";

    /// <summary>
    /// Gets or sets the path for cached distribution packages.
    /// </summary>
    public string PackageCachePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default starting path when opening a terminal.
    /// </summary>
    public string TerminalStartPath { get; set; } = "~";

    /// <summary>
    /// Gets or sets the default WSL version (1 or 2) for new installations.
    /// </summary>
    public int DefaultWslVersion { get; set; } = 2;

    /// <summary>
    /// Gets or sets the default username for new instances.
    /// </summary>
    public string DefaultUsername { get; set; } = "root";

    /// <summary>
    /// Gets or sets the ID of the default distribution for new installations.
    /// </summary>
    public string DefaultDistributionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to enable logging.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the log file path.
    /// </summary>
    public string LogPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to check for updates on startup.
    /// </summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets the URL for the distribution catalog.
    /// </summary>
    public string CatalogUrl { get; set; } = "https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/main/config/distros.json";

    /// <summary>
    /// Gets or sets the theme preference (Light, Dark, Auto).
    /// </summary>
    public string Theme { get; set; } = "Auto";

    /// <summary>
    /// Gets or sets the language/locale preference.
    /// </summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets whether to show confirmation dialogs for destructive operations.
    /// </summary>
    public bool ShowConfirmationDialogs { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent downloads.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to automatically retry failed downloads.
    /// </summary>
    public bool AutoRetryDownloads { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to automatically save settings.
    /// </summary>
    public bool AutoSaveEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-save interval in seconds.
    /// </summary>
    public int AutoSaveInterval { get; set; } = 30;
}
