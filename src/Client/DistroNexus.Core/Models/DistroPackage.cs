using CommunityToolkit.Mvvm.ComponentModel;

namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a WSL distribution package available for installation.
/// </summary>
public partial class DistroPackage : ObservableObject
{
    /// <summary>
    /// Gets or sets the unique identifier for the distribution.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the distribution.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the distribution.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default name of the distribution (used for internal identification).
    /// </summary>
    public string DefaultName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the distribution.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the download URL for the distribution package.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    [ObservableProperty]
    private long _fileSize;

    /// <summary>
    /// Gets or sets the SHA256 checksum for verification.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category or family of the distribution (e.g., "Debian-based", "Enterprise").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon URL for the distribution.
    /// </summary>
    public string IconUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this distribution is officially supported.
    /// </summary>
    public bool IsOfficial { get; set; }

    /// <summary>
    /// Gets or sets whether this package is cached locally.
    /// </summary>
    [ObservableProperty]
    private bool _isCached;

    /// <summary>
    /// Gets or sets whether this package is from a custom source.
    /// </summary>
    public bool IsCustomSource { get; set; }

    /// <summary>
    /// Gets or sets the local file path if the package is cached.
    /// </summary>
    [ObservableProperty]
    private string _localPath = string.Empty;

    /// <summary>
    /// Gets or sets whether this package is currently downloading.
    /// </summary>
    [ObservableProperty]
    private bool _isDownloading;

    /// <summary>
    /// Gets or sets the download progress (0-100).
    /// </summary>
    [ObservableProperty]
    private double _downloadProgress;

    /// <summary>
    /// Gets or sets the formatted download speed string (e.g., "1.5 MB/s").
    /// </summary>
    [ObservableProperty]
    private string _downloadSpeed = string.Empty;

    /// <summary>
    /// Gets or sets the formatted download status text (e.g., "150 MB / 1.2 GB").
    /// </summary>
    [ObservableProperty]
    private string _downloadStatusText = string.Empty;

    /// <summary>
    /// Gets or sets whether this package represents a merged same-file variant group.
    /// </summary>
    [ObservableProperty]
    private bool _isSameFileMerged;

    /// <summary>
    /// Gets or sets the label displayed for merged same-file variant groups.
    /// </summary>
    [ObservableProperty]
    private string _sameFileTagText = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata as key-value pairs.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
