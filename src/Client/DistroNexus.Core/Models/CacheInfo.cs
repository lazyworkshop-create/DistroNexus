namespace DistroNexus.Core.Models;

/// <summary>
/// Represents information about a cached package file.
/// </summary>
public class CachedPackageInfo
{
    /// <summary>Authenticated opaque authority required to delete this exact cache entry.</summary>
    public string CacheEntryId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the package ID.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the package.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets when the file was cached.
    /// </summary>
    public DateTime CachedDate { get; set; }

    /// <summary>
    /// Gets or sets when the file was last accessed.
    /// </summary>
    public DateTime LastAccessedDate { get; set; }

    /// <summary>
    /// Gets the formatted file size for display.
    /// </summary>
    public string SizeDisplay => FormatFileSize(SizeBytes);

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Represents cache usage statistics.
/// </summary>
public class CacheUsageInfo
{
    /// <summary>Gets whether eligible entries beyond <see cref="CachedPackages"/> exist.</summary>
    public bool HasMoreEntries { get; set; }
    /// <summary>
    /// Gets or sets the total cache size in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of cached packages.
    /// </summary>
    public int PackageCount { get; set; }

    /// <summary>
    /// Gets or sets the cache directory path.
    /// </summary>
    public string CachePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of cached packages.
    /// </summary>
    public List<CachedPackageInfo> CachedPackages { get; set; } = [];

    /// <summary>
    /// Gets the formatted total size for display.
    /// </summary>
    public string TotalSizeDisplay => FormatFileSize(TotalSizeBytes);

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
