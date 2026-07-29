using System.Diagnostics;

namespace DistroNexus.Desktop.Services;

public interface IBrowserLauncher
{
    void LaunchDockerInstall(Uri uri);
    void LaunchUpdateRelease(Uri uri);
}

/// <summary>Opens only loopback HTTP(S) endpoints that have already passed the Core URI policy.</summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public void LaunchDockerInstall(Uri uri)
    {
        if (uri.AbsoluteUri != "https://www.docker.com/products/docker-desktop/") throw new ArgumentException("Only the Docker Desktop install target can be opened.", nameof(uri));
        Launch(uri);
    }
    public void LaunchUpdateRelease(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/", StringComparison.Ordinal) || !uri.AbsolutePath.Contains("/releases", StringComparison.Ordinal)) throw new ArgumentException("Only a GitHub release target can be opened.", nameof(uri));
        Launch(uri);
    }
    private static void Launch(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}

/// <summary>Explorer-only presentation exception for a Core-returned product log target.</summary>
public sealed class ProductLogRevealLauncher
{
    public void Reveal(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !uri.IsFile || string.IsNullOrWhiteSpace(uri.LocalPath)) throw new ArgumentException("Only a product log file target can be opened.", nameof(uri));
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
