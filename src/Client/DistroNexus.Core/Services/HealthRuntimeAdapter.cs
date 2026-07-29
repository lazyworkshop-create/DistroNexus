using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DistroNexus.Core.Services;

/// <summary>Bounded read-only probes; no shell is built from user input.</summary>
public sealed class HealthRuntimeAdapter : IHealthRuntimeAdapter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private readonly IProcessRunner _runner;
    private readonly IWslConfigurationService? _configuration;
    private readonly ILocalhostForwardingEndpointStrategy _localhostEndpoint;
    public HealthRuntimeAdapter(IProcessRunner runner, IWslConfigurationService? configuration = null, ILocalhostForwardingEndpointStrategy? localhostEndpoint = null)
        => (_runner, _configuration, _localhostEndpoint) = (runner, configuration, localhostEndpoint ?? new NoLocalhostForwardingEndpointStrategy());

    public async Task<IReadOnlyDictionary<string, HealthProbeState>> ProbeNetworkAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HealthProbeState>(StringComparer.Ordinal);
        var instance = context.Instances.FirstOrDefault(x => x.IsRunning);
        if (instance is null)
        {
            results["dns"] = Unavailable("No running distribution was available for this probe.");
            results["wsl-to-windows"] = Unavailable("No running distribution was available for this probe.");
            return results;
        }
        var networkConfiguration = await ReadNetworkConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var mode = networkConfiguration.Mode;
        results["mode"] = new HealthProbeState("healthy", "Global WSL networking mode: " + mode + ".", new Dictionary<string, string> { ["networkingMode"] = mode });
        if (!mode.Equals("none", StringComparison.OrdinalIgnoreCase))
            results["dns"] = await ProbeAsync(instance.Name, "getent hosts microsoft.com >/dev/null", "DNS resolution", cancellationToken).ConfigureAwait(false);
        else
            results["dns"] = Unavailable("DNS probe is intentionally unavailable because networkingMode is none.");
        // The default NAT gateway is meaningful only for NAT. Mirrored and virtioproxy do not
        // promise a guest default gateway that represents the Windows host; probing it creates a
        // false warning on healthy machines.
        if (mode.Equals("nat", StringComparison.OrdinalIgnoreCase))
            results["wsl-to-windows"] = await ProbeAsync(instance.Name, "gateway=$(ip route | awk '/default/ {print $3; exit}'); test -n \"$gateway\" && ping -c 1 -W 2 \"$gateway\" >/dev/null", "WSL-to-Windows NAT gateway connectivity", cancellationToken).ConfigureAwait(false);
        else if (mode.Equals("none", StringComparison.OrdinalIgnoreCase))
            results["wsl-to-windows"] = Unavailable("WSL-to-Windows forwarding is intentionally disabled because networkingMode is none.");
        else
            results["wsl-to-windows"] = await ProbeAsync(instance.Name, "ip route | grep -q '^default ' && ip addr | grep -q 'inet '", "WSL route and interface availability for " + mode, cancellationToken).ConfigureAwait(false);
        results["localhost"] = await ProbeAsync(instance.Name, "getent hosts localhost >/dev/null", "Localhost resolution", cancellationToken).ConfigureAwait(false);
        var localhostSetting = networkConfiguration.LocalhostForwarding;
        results["localhost-forwarding-setting"] = localhostSetting switch
        {
            true => new HealthProbeState("healthy", "The global localhostForwarding setting is explicitly enabled.", new Dictionary<string, string> { ["localhostForwarding"] = "enabled" }),
            false => new HealthProbeState("information", "The global localhostForwarding setting is explicitly disabled; no forwarding endpoint will be probed.", new Dictionary<string, string> { ["localhostForwarding"] = "disabled" }),
            null => new HealthProbeState("information", "The global localhostForwarding setting is not explicitly configured; Health Center does not assume an effective value.", new Dictionary<string, string> { ["localhostForwarding"] = "not-configured" })
        };
        var endpoint = _localhostEndpoint.GetEndpoint(context, mode);
        results["localhost-forwarding"] = endpoint is null
            ? new HealthProbeState("information", "No explicit safe loopback endpoint is configured for localhost-forwarding; no application port was guessed. Configure an explicit integration endpoint to enable a TCP probe.", new Dictionary<string, string>
            {
                ["endpoint"] = "required",
                ["probe"] = "not-run",
                ["localhostForwarding"] = localhostSetting switch { true => "enabled", false => "disabled", _ => "not-configured" }
            })
            : !endpoint.IsValid
                ? new HealthProbeState("unavailable", "The configured localhost-forwarding endpoint is outside the safe loopback allow-list.")
                : await ProbeLocalhostEndpointAsync(endpoint, cancellationToken).ConfigureAwait(false);
        results["proxy"] = await ProbeProxyAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        if (mode.Equals("mirrored", StringComparison.OrdinalIgnoreCase))
        {
            // Mirrored mode is compatible with VPNs, but a VPN must not be inferred merely from
            // a proxy setting or an arbitrary Linux route.  Report the limitation explicitly so
            // it cannot become a false-positive connectivity failure.
            results["vpn-mirrored"] = new HealthProbeState("information", "Mirrored networking is enabled. VPN compatibility requires a runtime UAT probe; no VPN conflict was inferred from this read-only scan.");
        }
        if (!mode.Equals("none", StringComparison.OrdinalIgnoreCase))
            results["windows-to-wsl"] = await ProbeWindowsToWslAsync(instance.Name, cancellationToken).ConfigureAwait(false);
        else
            results["windows-to-wsl"] = Unavailable("Windows-to-WSL forwarding is intentionally unavailable because networkingMode is none.");
        // Do not probe arbitrary service ports.  A closed SSH, DNS, or application port is
        // normal on a healthy development machine and is not a networking failure.  Endpoint
        // TCP checks belong to an explicit, user-configured integration health check.
        return results;
    }

    private async Task<NetworkConfiguration> ReadNetworkConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_configuration is null) return new("nat", null);
        try
        {
            var document = await _configuration.ReadAsync(cancellationToken).ConfigureAwait(false);
            var mode = document.Settings.Values.TryGetValue("wsl2.networkingMode", out var configuredMode) && !string.IsNullOrWhiteSpace(configuredMode)
                ? configuredMode.Trim().ToLowerInvariant() : "nat";
            bool? localhostForwarding = document.Settings.Values.TryGetValue("wsl2.localhostForwarding", out var configuredForwarding) && bool.TryParse(configuredForwarding, out var enabled)
                ? enabled : null;
            return new(mode, localhostForwarding);
        }
        catch (OperationCanceledException) { throw; }
        catch { return new("nat", null); }
    }

    private sealed record NetworkConfiguration(string Mode, bool? LocalhostForwarding);

    public async Task<IReadOnlyDictionary<string, HealthProbeState>> ProbeSystemdAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HealthProbeState>(StringComparer.Ordinal);
        foreach (var instance in context.Instances.Where(x => x.IsRunning))
        {
            var result = await RunAsync(instance.Name, "systemctl --failed --no-legend --no-pager", cancellationToken).ConfigureAwait(false);
            results[instance.Name] = result.ExitCode == 0 && string.IsNullOrWhiteSpace(result.StandardOutput)
                ? new HealthProbeState("healthy", "No failed systemd units were reported.")
                : result.ExitCode == 0 ? new HealthProbeState("failed", "Failed systemd units were reported.", new Dictionary<string, string> { ["units"] = SensitiveDataRedactor.Redact(Trim(result.StandardOutput, 2048)) })
                : Unavailable("systemd is not running or does not permit this probe.", result);
        }
        foreach (var instance in context.Instances.Where(x => !x.IsRunning))
        {
            var state = string.IsNullOrWhiteSpace(instance.State) ? "unknown" : instance.State;
            results["startup:" + instance.Name] = state.Equals("Stopped", StringComparison.OrdinalIgnoreCase)
                ? Unavailable("Distribution is stopped; systemd startup cannot be inspected until it is started.")
                : new HealthProbeState("warning", "Distribution startup state is " + SensitiveDataRedactor.Redact(state) + ".");
        }
        return results;
    }

    public async Task<IReadOnlyDictionary<string, StorageHealthState>> ProbeStorageAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, StorageHealthState>(StringComparer.Ordinal);
        foreach (var drive in DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == DriveType.Fixed))
            results[drive.Name] = new(drive.AvailableFreeSpace, drive.TotalSize, null, null, null, "Host disk capacity was measured; VHDX and reclaimable-space measurements are not available from this read-only probe.");
        foreach (var instance in context.Instances)
        {
            if (!instance.IsRunning)
            {
                results["linux:" + instance.Name] = new(null, null, instance.Size > 0 ? instance.Size : null, null, null, "Linux filesystem probe is unavailable because the distribution is not running.");
                continue;
            }
            var result = await RunAsync(instance.Name, "df -Pk / | tail -n 1", cancellationToken).ConfigureAwait(false);
            var fields = result.StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            long? available = fields.Length >= 4 && long.TryParse(fields[^3], out var availableBlocks) ? availableBlocks * 1024 : null;
            long? used = fields.Length >= 5 && long.TryParse(fields[^4], out var usedBlocks) ? usedBlocks * 1024 : null;
            long? vhdx = instance.Size > 0 ? instance.Size : null;
            // This is an intentionally conservative estimate: host allocation in excess of guest
            // used blocks may be reclaimable after a trim/compact operation, but is never reported
            // as guaranteed reclaimable space.
            long? reclaimable = vhdx is > 0 && used is >= 0 && vhdx > used ? vhdx - used : null;
            var detail = available is null
                ? "Linux filesystem usage probe was unavailable."
                : reclaimable is null
                    ? "Linux root filesystem available space was measured; VHDX allocation is unavailable."
                    : "Linux root filesystem and VHDX allocation were measured; reclaimable space is an estimate.";
            results["linux:" + instance.Name] = new(null, null, vhdx, available, reclaimable, detail);
        }
        return results;
    }

    private async Task<HealthProbeState> ProbeAsync(string instance, string script, string title, CancellationToken token)
    {
        var result = await RunAsync(instance, script, token).ConfigureAwait(false);
        if (result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None) return new("healthy", title + " succeeded.");
        return result.ExitCode is null || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None
            ? Unavailable(title + " probe was unavailable.", result)
            : new("warning", title + " failed.", Evidence(result));
    }

    private async Task<HealthProbeState> ProbeProxyAsync(string instance, CancellationToken token)
    {
        var result = await RunAsync(instance, "if env | grep -qiE '^(http|https|all)_proxy='; then echo configured; else echo not-configured; fi", token).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None)
            return Unavailable("Proxy environment inspection was unavailable.", result);
        var configured = result.StandardOutput.Trim().Equals("configured", StringComparison.OrdinalIgnoreCase);
        return configured
            ? new HealthProbeState("configured", "A proxy environment variable is configured in the distribution.", new Dictionary<string, string> { ["proxy"] = "configured" })
            : new HealthProbeState("not-configured", "No proxy environment variable is configured in the distribution.", new Dictionary<string, string> { ["proxy"] = "not-configured" });
    }

    private async Task<HealthProbeState> ProbeWindowsToWslAsync(string instance, CancellationToken token)
    {
        var addressResult = await RunAsync(instance, "hostname -I | awk '{print $1}'", token).ConfigureAwait(false);
        if (addressResult.ExitCode != 0 || !IPAddress.TryParse(addressResult.StandardOutput.Trim(), out var address))
            return Unavailable("Windows-to-WSL connectivity is unavailable because the distribution did not provide a valid IP address.", addressResult);
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, (int)Timeout.TotalMilliseconds).WaitAsync(token).ConfigureAwait(false);
            return reply.Status == IPStatus.Success
                ? new HealthProbeState("healthy", "Windows-to-WSL ICMP connectivity succeeded.", new Dictionary<string, string> { ["addressFamily"] = address.AddressFamily.ToString() })
                : new HealthProbeState("warning", "Windows-to-WSL ICMP connectivity returned " + reply.Status + ".", new Dictionary<string, string> { ["addressFamily"] = address.AddressFamily.ToString() });
        }
        catch (OperationCanceledException) { throw; }
        catch (PingException ex) { return Unavailable("Windows-to-WSL ICMP connectivity probe was unavailable.", new ProcessResult(null, "", ex.Message, TimeSpan.Zero, false, false, false, null, ProcessFailureKind.StartFailed)); }
    }
    private static async Task<HealthProbeState> ProbeLocalhostEndpointAsync(HealthTcpEndpoint endpoint, CancellationToken token)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, token).AsTask().WaitAsync(Timeout, token).ConfigureAwait(false);
            return new HealthProbeState("healthy", "Configured localhost-forwarding endpoint accepted a TCP connection.", new Dictionary<string, string> { ["host"] = endpoint.Host, ["port"] = endpoint.Port.ToString() });
        }
        catch (OperationCanceledException) { throw; }
        catch (TimeoutException) { return new HealthProbeState("timeout", "Configured localhost-forwarding endpoint timed out.", new Dictionary<string, string> { ["port"] = endpoint.Port.ToString() }); }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused) { return new HealthProbeState("refused", "Configured localhost-forwarding endpoint refused the connection.", new Dictionary<string, string> { ["port"] = endpoint.Port.ToString() }); }
        catch (SocketException ex) { return new HealthProbeState("unavailable", "Configured localhost-forwarding endpoint could not be observed: " + ex.SocketErrorCode + ".", new Dictionary<string, string> { ["port"] = endpoint.Port.ToString() }); }
    }
    private async Task<HealthProbeState> ProbeWindowsToWslTcpAsync(string instance, CancellationToken token)
    {
        var addressResult = await RunAsync(instance, "hostname -I | awk '{print $1}'", token).ConfigureAwait(false);
        if (addressResult.ExitCode != 0 || !IPAddress.TryParse(addressResult.StandardOutput.Trim(), out var address))
            return Unavailable("Windows-to-WSL TCP probe is unavailable because the distribution did not provide a valid IP address.", addressResult);
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, 22, token).AsTask().WaitAsync(Timeout, token).ConfigureAwait(false);
            return new HealthProbeState("healthy", "Windows-to-WSL TCP port 22 accepted a connection.", new Dictionary<string, string> { ["port"] = "22", ["addressFamily"] = address.AddressFamily.ToString() });
        }
        catch (OperationCanceledException) { throw; }
        catch (TimeoutException) { return new HealthProbeState("timeout", "Windows-to-WSL TCP port 22 timed out.", new Dictionary<string, string> { ["port"] = "22" }); }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused) { return new HealthProbeState("refused", "Windows-to-WSL TCP port 22 refused the connection.", new Dictionary<string, string> { ["port"] = "22" }); }
        catch (SocketException ex) { return Unavailable("Windows-to-WSL TCP probe was unavailable: " + ex.SocketErrorCode + "."); }
    }
    private async Task<HealthProbeState> ProbeTcpFromWslAsync(string instance, string hostExpression, int port, string title, CancellationToken token)
    {
        // bash /dev/tcp is a direct TCP connect attempt. A refused connection is useful evidence
        // that routing worked; a runner timeout is reported distinctly from refusal.
        var script = $"host={hostExpression}; test -n \"$host\" && timeout 3 bash -c \"</dev/tcp/$host/{port}\"";
        var result = await RunAsync(instance, script, token).ConfigureAwait(false);
        if (result.ExitCode == 0) return new HealthProbeState("healthy", title + " accepted a connection.", new Dictionary<string, string> { ["port"] = port.ToString() });
        if (result.TimedOut) return new HealthProbeState("timeout", title + " timed out.", new Dictionary<string, string> { ["port"] = port.ToString() });
        if (result.ExitCode is not null) return new HealthProbeState("refused", title + " did not accept the connection.", new Dictionary<string, string> { ["port"] = port.ToString() });
        return Unavailable(title + " probe was unavailable.", result);
    }
    private Task<ProcessResult> RunAsync(string instance, string script, CancellationToken token) => _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", instance, "--", "sh", "-lc", script], Timeout, 16 * 1024, 4 * 1024), token);
    private static HealthProbeState Unavailable(string detail, ProcessResult? result = null) => new("unavailable", detail, result is null ? null : Evidence(result));
    private static IReadOnlyDictionary<string, string> Evidence(ProcessResult result) => new Dictionary<string, string> { ["exitCode"] = result.ExitCode?.ToString() ?? "none", ["timedOut"] = result.TimedOut.ToString(), ["error"] = SensitiveDataRedactor.Redact(Trim(result.StandardError, 512)) };
    private static string Trim(string value, int limit) => value.Length <= limit ? value : value[..limit];
}
