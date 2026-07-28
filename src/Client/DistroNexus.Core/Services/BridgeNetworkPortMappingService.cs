using System.Text.RegularExpressions;
using System.Net;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Fixed-process network inspection used by WorkspaceBridge. It never invokes the PowerShell module.</summary>
public sealed class BridgeNetworkPortMappingService : INetworkService
{
    private static readonly Regex Listener = new("(?:LISTEN|UNCONN)\\s+\\d+\\s+\\d+\\s+(\\S+):(\\d+)\\s+\\S+(?:\\s+users:\\(\\(\\\"?([^\\\",]+)\\\"?,pid=(\\d+))?", RegexOptions.CultureInvariant);
    private static readonly Regex PortProxy = new("^\\s*(?<listenAddress>\\d{1,3}(?:\\.\\d{1,3}){3})\\s+(?<listenPort>\\d{1,5})\\s+(?<connectAddress>\\d{1,3}(?:\\.\\d{1,3}){3})\\s+(?<connectPort>\\d{1,5})\\s*$", RegexOptions.CultureInvariant);
    private readonly IProcessRunner _runner;
    private readonly INetworkStatusAdapter _status;
    public BridgeNetworkPortMappingService(IProcessRunner runner, INetworkStatusAdapter status) => (_runner, _status) = (runner, status);
    public async Task<List<PortMapping>> GetPortMappingsAsync(string instanceName, string? protocol = null, CancellationToken cancellationToken = default)
    {
        ValidateName(instanceName);
        if (protocol is not null && !protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) && !protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Protocol is invalid.", nameof(protocol));
        var mappings = new List<PortMapping>();
        foreach (var item in new[] { (Protocol: "TCP", Argument: "-tlnp"), (Protocol: "UDP", Argument: "-ulnp") })
        {
            if (protocol is not null && !protocol.Equals(item.Protocol, StringComparison.OrdinalIgnoreCase)) continue;
            var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", instanceName, "--exec", "ss", item.Argument], TimeSpan.FromSeconds(30), 128 * 1024, 32 * 1024), cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == 0) mappings.AddRange(result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => Parse(line, item.Protocol)).Where(x => x is not null)!);
        }
        var ip = await GetInstanceIpAddressAsync(instanceName, cancellationToken).ConfigureAwait(false);
        foreach (var mapping in mappings) mapping.InstanceIpAddress = ip;
        var proxyPorts = await GetPortProxyPortsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var mapping in mappings) mapping.HasWindowsProxy = mapping.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) && proxyPorts.Contains(mapping.Port);
        var collisions = await _status.GetPortCollisionsAsync(mappings, cancellationToken).ConfigureAwait(false);
        foreach (var mapping in mappings) { var c = collisions.FirstOrDefault(x => x.Port == mapping.Port && x.Protocol.Equals(mapping.Protocol, StringComparison.OrdinalIgnoreCase)); mapping.HasWindowsCollision = c?.IsCollision == true; mapping.ConflictGuidance = c?.IsCollision == true ? c.Detail : null; }
        return mappings;
    }
    internal async Task<IReadOnlySet<int>> GetPortProxyPortsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync(new ProcessRequest("netsh.exe", ["interface", "portproxy", "show", "v4tov4"], TimeSpan.FromSeconds(10), 32 * 1024, 8 * 1024), cancellationToken).ConfigureAwait(false);
            return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled ? ParsePortProxyPorts(result.StandardOutput) : new HashSet<int>();
        }
        catch (OperationCanceledException) { throw; }
        catch { return new HashSet<int>(); }
    }
    public async Task<string?> GetInstanceIpAddressAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ValidateName(instanceName);
        var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", instanceName, "--exec", "hostname", "-I"], TimeSpan.FromSeconds(15), 4096, 4096), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() : null;
    }
    private static void ValidateName(string value) { if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("Instance name is invalid.", nameof(value)); }
    internal static IReadOnlySet<int> ParsePortProxyPorts(string output)
    {
        var ports = new HashSet<int>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = PortProxy.Match(line);
            if (!match.Success || !IPAddress.TryParse(match.Groups["listenAddress"].Value, out _) || !IPAddress.TryParse(match.Groups["connectAddress"].Value, out _) || !int.TryParse(match.Groups["listenPort"].Value, out var listenPort) || !int.TryParse(match.Groups["connectPort"].Value, out var connectPort) || listenPort is < 1 or > 65535 || connectPort is < 1 or > 65535) continue;
            ports.Add(listenPort);
        }
        return ports;
    }
    private static PortMapping? Parse(string line, string protocol) { var match = Listener.Match(line); if (!match.Success || !int.TryParse(match.Groups[2].Value, out var port)) return null; var address = match.Groups[1].Value; return new PortMapping { Protocol = protocol, LocalAddress = address, Port = port, ProcessName = match.Groups[3].Success ? match.Groups[3].Value : string.Empty, Pid = match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out var pid) ? pid : 0, AddressFamily = address.Contains(':') ? "IPv6" : "IPv4" }; }
}
