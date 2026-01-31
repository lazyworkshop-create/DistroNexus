namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a WSL distribution instance installed on the system.
/// </summary>
public class WslInstance
{
    /// <summary>
    /// Gets or sets the name of the WSL instance.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state of the instance (Running, Stopped, Installing, etc.).
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the WSL version (1 or 2).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the installation path of the instance.
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is the default instance.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the size of the instance in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the distribution name (e.g., "Ubuntu", "Debian").
    /// </summary>
    public string Distribution { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last access timestamp.
    /// </summary>
    public DateTime? LastAccessed { get; set; }

    /// <summary>
    /// Gets a value indicating whether the instance is currently running.
    /// </summary>
    public bool IsRunning => State?.Equals("Running", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Normalizes a Windows path by removing the long path prefix (\\?\) if present.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    public static string NormalizeWindowsPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Remove the \\?\ prefix used for long paths in Windows
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return path.Substring(4);
        }

        return path;
    }
}
