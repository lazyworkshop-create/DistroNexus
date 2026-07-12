using System.Diagnostics;

namespace DistroNexus.Desktop.Services;

public interface IBrowserLauncher
{
    void Open(Uri uri);
}

/// <summary>Opens only loopback HTTP(S) endpoints that have already passed the Core URI policy.</summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public void Open(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https") || uri.Host is not ("127.0.0.1" or "::1" or "localhost"))
            throw new ArgumentException("Only loopback HTTP(S) endpoints can be opened.", nameof(uri));

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
