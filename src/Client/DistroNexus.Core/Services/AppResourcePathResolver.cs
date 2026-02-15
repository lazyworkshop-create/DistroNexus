namespace DistroNexus.Core.Services;

/// <summary>
/// Resolves application resource paths across packaged and development layouts.
/// </summary>
public static class AppResourcePathResolver
{
    /// <summary>
    /// Finds a file in the current base directory or its parent directories.
    /// </summary>
    public static string FindFileInBaseOrParents(string baseDirectory, string relativePath, int maxParentLevels = 6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var fullBaseDirectory = Path.GetFullPath(baseDirectory);
        var currentDirectory = fullBaseDirectory;

        for (var level = 0; level <= maxParentLevels; level++)
        {
            var candidate = Path.GetFullPath(Path.Combine(currentDirectory, relativePath));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(currentDirectory);
            if (parent == null)
            {
                break;
            }

            currentDirectory = parent.FullName;
        }

        return Path.GetFullPath(Path.Combine(fullBaseDirectory, relativePath));
    }

    /// <summary>
    /// Finds a directory in the current base directory or its parent directories that contains a specific file.
    /// </summary>
    public static string? FindDirectoryWithFileInBaseOrParents(
        string baseDirectory,
        string relativeDirectory,
        string requiredFileName,
        int maxParentLevels = 6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredFileName);

        var fullBaseDirectory = Path.GetFullPath(baseDirectory);
        var currentDirectory = fullBaseDirectory;

        for (var level = 0; level <= maxParentLevels; level++)
        {
            var directoryCandidate = Path.GetFullPath(Path.Combine(currentDirectory, relativeDirectory));
            var fileCandidate = Path.Combine(directoryCandidate, requiredFileName);

            if (File.Exists(fileCandidate))
            {
                return directoryCandidate;
            }

            var parent = Directory.GetParent(currentDirectory);
            if (parent == null)
            {
                break;
            }

            currentDirectory = parent.FullName;
        }

        return null;
    }
}