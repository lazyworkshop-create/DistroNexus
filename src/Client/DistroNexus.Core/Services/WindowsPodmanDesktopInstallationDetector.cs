using DistroNexus.Core.Interfaces;

namespace DistroNexus.Core.Services;

/// <summary>Checks fixed Windows installation locations; command-line availability is not installation evidence.</summary>
public sealed class WindowsPodmanDesktopInstallationDetector : IPodmanDesktopInstallationDetector
{
    private static readonly string[] RelativeExecutablePaths =
    [
        @"RedHat\Podman Desktop\Podman Desktop.exe",
        @"Podman Desktop\Podman Desktop.exe"
    ];

    public bool IsInstalled()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        return roots.Where(root => !string.IsNullOrWhiteSpace(root))
            .SelectMany(root => RelativeExecutablePaths.Select(relative => Path.Combine(root, relative)))
            .Any(File.Exists);
    }
}
