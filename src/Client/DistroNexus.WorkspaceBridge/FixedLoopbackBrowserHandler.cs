using System.Diagnostics;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.WorkspaceBridge;

public static class FixedLoopbackBrowserHandler
{
    public static Action<ProcessStartInfo> Launch { get; set; } = info => Process.Start(info);
    public static FixedExplorerResult Open(string host, int port)
    {
        if (host is not ("localhost" or "127.0.0.1" or "::1") || port is < 1 or > 65535) throw new ArgumentException("Only a fixed loopback HTTP endpoint may be opened.");
        var safeHost = host == "::1" ? "[::1]" : host;
        Launch(new ProcessStartInfo(new Uri($"http://{safeHost}:{port}/", UriKind.Absolute).AbsoluteUri) { UseShellExecute = true });
        return new(true, "Browser.Opened");
    }
}
