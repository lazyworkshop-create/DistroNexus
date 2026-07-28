using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class SystemdService : ISystemdService
{
    private readonly IProcessRunner _runner;
    private readonly IPlatformCapabilityService _capabilities;
    private readonly IDistributionConfigurationService? _distributionConfiguration;
    private readonly Dictionary<string, SystemdOperationPreview> _previews = new(StringComparer.Ordinal);
    public SystemdService(IProcessRunner runner, IPlatformCapabilityService capabilities, IDistributionConfigurationService? distributionConfiguration = null) => (_runner, _capabilities, _distributionConfiguration) = (runner, capabilities, distributionConfiguration);

    public async Task<IReadOnlyList<SystemdServiceInfo>> ListAsync(string instanceName, SystemdScope scope, CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(instanceName, cancellationToken).ConfigureAwait(false);
        var units = await RunAsync(instanceName, scope, ["list-units", "--all", "--type=service", "--no-legend", "--plain", "--output=json"], cancellationToken).ConfigureAwait(false);
        if (units.ExitCode != 0) return [];
        var unitFiles = await RunAsync(instanceName, scope, ["list-unit-files", "--type=service", "--no-legend", "--plain", "--output=json"], cancellationToken).ConfigureAwait(false);
        var enabled = unitFiles.ExitCode == 0 ? ParseUnitFileStates(unitFiles.StandardOutput) : new Dictionary<string, string>(StringComparer.Ordinal);
        return ParseUnitList(units.StandardOutput, enabled, scope);
    }
    public async Task<IReadOnlyList<SystemdJournalEntry>> GetJournalAsync(string instanceName, SystemdUnitName unit, SystemdScope scope, string? search = null, int lineLimit = 200, CancellationToken cancellationToken = default)
    {
        if (lineLimit is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(lineLimit));
        if (search?.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("Journal search is invalid.", nameof(search));
        await EnsureAvailableAsync(instanceName, cancellationToken).ConfigureAwait(false);
        var result = await RunAsync(instanceName, scope, ["journalctl", "--no-pager", "--output=short-iso", "--lines", lineLimit.ToString(System.Globalization.CultureInfo.InvariantCulture), "--unit", unit.Value], cancellationToken).ConfigureAwait(false);
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => search is null || x.Contains(search, StringComparison.OrdinalIgnoreCase));
        return lines.Select(x => new SystemdJournalEntry(x.Length > 25 ? x[..25] : string.Empty, Severity(x), x)).ToArray();
    }
    public async Task<SystemdServiceDetails?> GetDetailsAsync(string instanceName, SystemdUnitName unit, SystemdScope scope, CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(instanceName, cancellationToken).ConfigureAwait(false);
        var result = await RunAsync(instanceName, scope, ["show", unit.Value, "--no-page", "--property=Id,Description,ActiveState,SubState,UnitFileState,LoadState,FragmentPath,After,Requires"], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) return null;
        var properties = ParseProperties(result.StandardOutput);
        var service = ToService(properties, scope);
        return service is null ? null : new SystemdServiceDetails(service,
            Split(properties.GetValueOrDefault("After")).Concat(Split(properties.GetValueOrDefault("Requires"))).Distinct(StringComparer.Ordinal).ToArray(),
            properties.GetValueOrDefault("FragmentPath"));
    }
    public async Task<SystemdOperationPreview> PreviewAsync(string instanceName, SystemdUnitName unit, SystemdAction action, SystemdScope scope, CancellationToken cancellationToken = default)
    {
        await EnsureAvailableAsync(instanceName, cancellationToken).ConfigureAwait(false);
        var requires = scope == SystemdScope.System;
        var preview = new SystemdOperationPreview(instanceName, unit, action, scope, requires,
            [$"{action} {unit.Value} in the {scope.ToString().ToLowerInvariant()} service manager."],
            requires ? ["This action uses sudo --non-interactive. Configure passwordless sudo for the permitted systemctl action; DistroNexus never requests or stores a Linux password."] : [], Guid.NewGuid().ToString("N"));
        _previews[preview.PreviewToken] = preview;
        return preview;
    }
    public async Task<SystemdOperationResult> ExecuteAsync(SystemdOperationPreview preview, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(preview.PreviewToken) || !_previews.Remove(preview.PreviewToken, out var expected) || expected != preview) return new(false, "PreviewRequired", null, "DN-8001: Generate and confirm a current operation preview before executing.");
        await EnsureAvailableAsync(preview.InstanceName, cancellationToken).ConfigureAwait(false);
        var result = await RunAsync(preview.InstanceName, preview.Scope, [ToVerb(preview.Action), preview.Unit.Value], cancellationToken, preview.RequiresLinuxPrivilege).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var guidance = preview.RequiresLinuxPrivilege && (result.StandardError.Contains("password", StringComparison.OrdinalIgnoreCase) || result.StandardError.Contains("sudo", StringComparison.OrdinalIgnoreCase))
                ? "DN-8002: Linux privilege is required. Configure a narrowly scoped non-interactive sudo rule; no password was requested or stored." : null;
            return new(false, preview.RequiresLinuxPrivilege ? "RequiresLinuxPrivilege" : "SystemdActionFailed", null, guidance);
        }
        var details = await GetDetailsAsync(preview.InstanceName, preview.Unit, preview.Scope, cancellationToken).ConfigureAwait(false);
        var expectedState = preview.Action == SystemdAction.Stop ? "inactive" : "active";
        if (!string.Equals(details?.Service.ActiveState, expectedState, StringComparison.OrdinalIgnoreCase))
            return new(false, "PostconditionFailed", details?.Service, "DN-8003: systemd did not report the expected service state after the operation.");
        return new(true, "Succeeded", details?.Service);
    }
    private async Task EnsureAvailableAsync(string instance, CancellationToken ct)
    {
        var snapshot = await _capabilities.GetInstanceSnapshotAsync(instance, false, ct).ConfigureAwait(false);
        if (!snapshot.Capabilities.TryGetValue(CapabilityId.InstanceSystemd, out var systemd) || !systemd.IsSupported)
            throw new InvalidOperationException("DN-8001: systemd is unavailable for this instance.");
    }
    private async Task<ProcessResult> RunAsync(string instance, SystemdScope scope, IReadOnlyList<string> command, CancellationToken ct, bool privileged = false)
    {
        var args = new List<string> { "--distribution", instance };
        if (scope == SystemdScope.User) args.AddRange(["--user", await ResolveUserAsync(instance, ct).ConfigureAwait(false)]);
        args.Add("--");
        if (privileged && scope == SystemdScope.System) args.AddRange(["sudo", "--non-interactive"]);
        if (command[0] == "journalctl")
        {
            args.Add("journalctl");
            if (scope == SystemdScope.User) args.Add("--user");
            args.AddRange(command.Skip(1));
        }
        else
        {
            args.Add("systemctl");
            if (scope == SystemdScope.User) args.Add("--user");
            args.AddRange(command);
        }
        return await _runner.RunAsync(new ProcessRequest("wsl.exe", args, TimeSpan.FromSeconds(30), 256 * 1024, 128 * 1024), ct).ConfigureAwait(false);
    }
    private async Task<string> ResolveUserAsync(string instance, CancellationToken ct)
    {
        if (_distributionConfiguration is null) throw new InvalidOperationException("DN-8001: The configured distribution user is unavailable.");
        var document = await _distributionConfiguration.ReadAsync(instance, ct).ConfigureAwait(false);
        if (!document.Settings.Values.TryGetValue("user.default", out var user) || string.IsNullOrWhiteSpace(user) || !UserName.IsMatch(user))
            throw new InvalidOperationException("DN-8001: The selected distribution has no valid configured default user.");
        return user;
    }
    private static readonly Regex UserName = new("^[a-z_][a-z0-9_-]{0,31}$", RegexOptions.CultureInvariant);
    public static IReadOnlyList<SystemdServiceInfo> ParseServices(string text, SystemdScope scope) => text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
        .Select(ParseProperties).Select(x => TryToService(x, scope)).Where(x => x is not null).Cast<SystemdServiceInfo>().ToArray();
    public static IReadOnlyList<SystemdServiceInfo> ParseUnitList(string text, IReadOnlyDictionary<string, string> unitFileStates, SystemdScope scope)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            var result = new List<SystemdServiceInfo>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var id = Json(entry, "unit");
                if (id is null || !id.EndsWith(".service", StringComparison.Ordinal)) continue;
                try { result.Add(new SystemdServiceInfo(new SystemdUnitName(id), Json(entry, "description") ?? string.Empty, Json(entry, "active") ?? "unknown", Json(entry, "sub") ?? "unknown", unitFileStates.GetValueOrDefault(id, "unknown"), Json(entry, "load") ?? "unknown", scope)); }
                catch (ArgumentException) { }
            }
            return result.OrderBy(x => x.Name.Value, StringComparer.Ordinal).ToArray();
        }
        catch (JsonException) { return []; }
    }
    public static IReadOnlyDictionary<string, string> ParseUnitFileStates(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return new Dictionary<string, string>(StringComparer.Ordinal);
            return document.RootElement.EnumerateArray().Select(x => (Unit: Json(x, "unit_file"), State: Json(x, "state"))).Where(x => x.Unit is not null && x.State is not null).ToDictionary(x => x.Unit!, x => x.State!, StringComparer.Ordinal);
        }
        catch (JsonException) { return new Dictionary<string, string>(StringComparer.Ordinal); }
    }
    private static string? Json(JsonElement element, string name) => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static Dictionary<string, string> ParseProperties(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Contains('=')) .Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x[1], StringComparer.Ordinal);
    private static SystemdServiceInfo? ToService(IReadOnlyDictionary<string, string> x, SystemdScope scope) => x.TryGetValue("Id", out var id) && x.ContainsKey("ActiveState") && x.ContainsKey("LoadState") ? new(new SystemdUnitName(id), x.GetValueOrDefault("Description", string.Empty), x.GetValueOrDefault("ActiveState", "unknown"), x.GetValueOrDefault("SubState", "unknown"), x.GetValueOrDefault("UnitFileState", "unknown"), x.GetValueOrDefault("LoadState", "unknown"), scope) : null;
    private static SystemdServiceInfo? TryToService(IReadOnlyDictionary<string, string> x, SystemdScope scope)
    {
        try { return ToService(x, scope); } catch (ArgumentException) { return null; }
    }
    private static string[] Split(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static string Severity(string line) => line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("failed", StringComparison.OrdinalIgnoreCase) ? "Error" : line.Contains("warn", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Info";
    private static string ToVerb(SystemdAction action) => action.ToString().ToLowerInvariant();
}

public sealed class NetworkDiagnosticsService : INetworkDiagnosticsService
{
    private readonly IWslNetworkDiagnosticsAdapter _wsl;
    public NetworkDiagnosticsService() : this(new UnavailableWslNetworkDiagnosticsAdapter()) { }
    public NetworkDiagnosticsService(IWslNetworkDiagnosticsAdapter wsl) => _wsl = wsl;
    public async Task<NetworkProbeResult> ProbeAsync(NetworkProbeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.IndexOfAny(['\r', '\n', '\0']) >= 0) return new(request, NetworkProbeOutcome.InvalidInput, "Host is invalid.");
        var timeout = request.Timeout ?? TimeSpan.FromSeconds(5);
        if (timeout <= TimeSpan.Zero) return new(request, NetworkProbeOutcome.InvalidInput, "Timeout is invalid.");
        if (request.Kind == NetworkProbeKind.WslInstance)
            return await _wsl.ProbeAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.Kind == NetworkProbeKind.Gateway && request.Port is null) return new(request, NetworkProbeOutcome.InvalidInput, "Gateway probes require an explicit TCP port.");
        try
        {
            if (request.Kind == NetworkProbeKind.Dns)
            {
                var addresses = await Dns.GetHostAddressesAsync(request.Host, cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                return new(request, NetworkProbeOutcome.Resolved, "DNS resolved.", addresses);
            }
            if (request.Port is not (> 0 and <= 65535)) return new(request, NetworkProbeOutcome.InvalidInput, "A TCP port from 1 through 65535 is required.");
            using var client = new TcpClient();
            await client.ConnectAsync(request.Host, request.Port.Value, cancellationToken).AsTask().WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return new(request, NetworkProbeOutcome.Resolved, "TCP connection established.");
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionRefused) { return new(request, NetworkProbeOutcome.Refused, "The endpoint refused the connection."); }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostUnreachable or SocketError.NetworkUnreachable) { return new(request, NetworkProbeOutcome.RouteMissing, "No route to the endpoint."); }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AccessDenied) { return new(request, NetworkProbeOutcome.PermissionDenied, "The operating system denied the probe."); }
        catch (SocketException) { return new(request, NetworkProbeOutcome.ToolUnavailable, "The network probe could not be completed."); }
        catch (TimeoutException) { return new(request, NetworkProbeOutcome.TimedOut, "The network probe timed out."); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(request, NetworkProbeOutcome.TimedOut, "The network probe timed out."); }
    }
    public NetworkingModeGuidance GetModeGuidance(WslNetworkingMode mode, PlatformCapabilitySnapshot capabilities)
    {
        var mirrored = capabilities.Capabilities.TryGetValue(CapabilityId.MirroredNetworking, out var m) && m.IsSupported;
        return mode switch
        {
            WslNetworkingMode.Mirrored => new(mode, mirrored, true, ["Improves VPN and IPv6 compatibility.", "Review LAN exposure and firewall behavior before applying."], RestartScope.Wsl),
            WslNetworkingMode.Bridged => new(mode, false, false, ["Bridged networking is deprecated and is not recommended for new configurations."], RestartScope.Wsl),
            WslNetworkingMode.None => new(mode, true, false, ["Disables WSL networking."], RestartScope.Wsl),
            WslNetworkingMode.VirtioProxy => new(mode, true, true, ["Use when NAT is unavailable; restart WSL after changing the mode."], RestartScope.Wsl),
            _ => new(mode, true, true, ["Default NAT mode provides isolated networking."], RestartScope.Wsl)
        };
    }
}

public sealed class UnavailableWslNetworkDiagnosticsAdapter : IWslNetworkDiagnosticsAdapter
{
    public Task<NetworkProbeResult> ProbeAsync(NetworkProbeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new NetworkProbeResult(request, NetworkProbeOutcome.ToolUnavailable, "WSL-side network probe adapter is unavailable; no Windows TCP probe was substituted."));
}

public sealed class WslNetworkDiagnosticsAdapter : IWslNetworkDiagnosticsAdapter
{
    private readonly IProcessRunner _runner;
    public WslNetworkDiagnosticsAdapter(IProcessRunner runner) => _runner = runner;
    public async Task<NetworkProbeResult> ProbeAsync(NetworkProbeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind != NetworkProbeKind.WslInstance) return new(request, NetworkProbeOutcome.ToolUnavailable, "The WSL adapter only supports WSL-instance probes.");
        if (request.Host.IndexOfAny(['\r', '\n', '\0', ' ']) >= 0) return new(request, NetworkProbeOutcome.InvalidInput, "Host is invalid.");
        if (string.IsNullOrWhiteSpace(request.DistributionName) || request.DistributionName.IndexOfAny(['\r', '\n', '\0']) >= 0) return new(request, NetworkProbeOutcome.InvalidInput, "A selected WSL distribution is required.");
        try
        {
            var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", request.DistributionName, "--exec", "getent", "ahosts", request.Host], request.Timeout ?? TimeSpan.FromSeconds(5), 16 * 1024, 8 * 1024), cancellationToken).ConfigureAwait(false);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
                ? new(request, NetworkProbeOutcome.Resolved, "WSL DNS lookup resolved.")
                : new(request, NetworkProbeOutcome.ToolUnavailable, "WSL DNS helper did not return a result.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(request, NetworkProbeOutcome.TimedOut, "The WSL probe timed out."); }
        catch (Exception) { return new(request, NetworkProbeOutcome.ToolUnavailable, "The supported WSL DNS helper is unavailable."); }
    }
}

public sealed class NetworkConfigurationService : INetworkConfigurationService
{
    private readonly IWslConfigurationService _configuration;
    private readonly IPlatformCapabilityService _capabilities;
    private readonly INetworkDiagnosticsService _diagnostics;
    private readonly Dictionary<string, (WslNetworkingMode? Mode, NetworkSettings? Settings, string Fingerprint, IReadOnlySet<string> Capabilities)> _previews = new(StringComparer.Ordinal);
    public NetworkConfigurationService(IWslConfigurationService configuration, IPlatformCapabilityService capabilities, INetworkDiagnosticsService diagnostics) => (_configuration, _capabilities, _diagnostics) = (configuration, capabilities, diagnostics);
    public async Task<NetworkingModeGuidance> GetGuidanceAsync(WslNetworkingMode mode, CancellationToken cancellationToken = default) => _diagnostics.GetModeGuidance(mode, await _capabilities.GetHostSnapshotAsync(false, cancellationToken));
    public async Task<NetworkModePreview> PreviewModeAsync(WslNetworkingMode mode, CancellationToken cancellationToken = default)
    {
        var guidance = await GetGuidanceAsync(mode, cancellationToken); if (!guidance.IsSupported) throw new ConfigurationValidationException([new(0, "config.unsupported", guidance.CompatibilityNotes.First())]);
        var doc = await _configuration.ReadAsync(cancellationToken); var caps = WslConfigurationSchema.MapCapabilities(await _capabilities.GetHostSnapshotAsync(false, cancellationToken));
        var preview = await _configuration.PreviewAsync(new Dictionary<string, string?> { ["wsl2.networkingMode"] = ToValue(mode) }, doc.Fingerprint, caps, cancellationToken);
        var token = Guid.NewGuid().ToString("N"); _previews[token] = (mode, null, doc.Fingerprint, caps);
        return new NetworkModePreview(mode, preview, guidance, token);
    }
    public async Task<ConfigurationSaveResult> ApplyModeAsync(WslNetworkingMode mode, string previewToken, CancellationToken cancellationToken = default)
    {
        if (!_previews.Remove(previewToken, out var saved) || saved.Mode != mode || saved.Settings is not null) throw new InvalidOperationException("DN-8004: A current networking-mode preview is required.");
        var guidance = await GetGuidanceAsync(mode, cancellationToken); if (!guidance.IsSupported) throw new ConfigurationValidationException([new(0, "config.unsupported", guidance.CompatibilityNotes.First())]);
        return await _configuration.SaveAsync(new Dictionary<string, string?> { ["wsl2.networkingMode"] = ToValue(mode) }, saved.Fingerprint, saved.Capabilities, cancellationToken);
    }
    public async Task<NetworkSettings> ReadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var values = (await _configuration.ReadAsync(cancellationToken).ConfigureAwait(false)).Settings.Values;
        return new NetworkSettings(ReadBool(values, "wsl2.dnsTunneling"), ReadBool(values, "wsl2.autoProxy"), ReadBool(values, "wsl2.firewall"), ReadBool(values, "wsl2.hostAddressLoopback"), ReadBool(values, "wsl2.bestEffortDnsParsing"), values.GetValueOrDefault("wsl2.ignoredPorts"));
    }
    public async Task<NetworkSettingsPreview> PreviewSettingsAsync(NetworkSettings settings, CancellationToken cancellationToken = default)
    {
        var doc = await _configuration.ReadAsync(cancellationToken); var caps = WslConfigurationSchema.MapCapabilities(await _capabilities.GetHostSnapshotAsync(false, cancellationToken));
        var preview = await _configuration.PreviewAsync(ToValues(settings), doc.Fingerprint, caps, cancellationToken);
        var token = Guid.NewGuid().ToString("N");
        _previews[token] = (null, settings, doc.Fingerprint, caps);
        return new NetworkSettingsPreview(settings, preview, token);
    }
    public async Task<ConfigurationSaveResult> ApplySettingsAsync(NetworkSettings settings, string previewToken, CancellationToken cancellationToken = default)
    {
        if (!_previews.Remove(previewToken, out var saved) || saved.Settings != settings || saved.Mode is not null)
            throw new InvalidOperationException("DN-8004: A current network-settings preview is required.");
        return await _configuration.SaveAsync(ToValues(settings), saved.Fingerprint, saved.Capabilities, cancellationToken);
    }
    private static Dictionary<string, string?> ToValues(NetworkSettings settings)
    {
        var values = new Dictionary<string, string?>();
        Add(values, "wsl2.dnsTunneling", settings.DnsTunneling); Add(values, "wsl2.autoProxy", settings.AutoProxy); Add(values, "wsl2.firewall", settings.Firewall); Add(values, "wsl2.hostAddressLoopback", settings.HostAddressLoopback); Add(values, "wsl2.bestEffortDnsParsing", settings.BestEffortDnsParsing);
        if (settings.IgnoredPorts is not null) values["wsl2.ignoredPorts"] = settings.IgnoredPorts;
        return values;
    }
    private static void Add(Dictionary<string, string?> values, string key, bool? value) { if (value.HasValue) values[key] = value.Value.ToString().ToLowerInvariant(); }
    private static bool? ReadBool(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : null;
    private static string ToValue(WslNetworkingMode mode) => mode switch { WslNetworkingMode.VirtioProxy => "virtioproxy", _ => mode.ToString().ToLowerInvariant() };
}

public static class SafeBrowserUri
{
    public static Uri? FromPortMapping(PortMapping mapping, string scheme = "http")
    {
        if (scheme is not ("http" or "https") || mapping.Port is < 1 or > 65535) return null;
        var host = mapping.LocalAddress.Trim('[', ']');
        if (host is not ("127.0.0.1" or "::1" or "localhost")) return null;
        return Uri.TryCreate($"{scheme}://{(host.Contains(':') ? $"[{host}]" : host)}:{mapping.Port}/", UriKind.Absolute, out var uri) ? uri : null;
    }
}

public sealed class WindowsNetworkStatusAdapter : INetworkStatusAdapter
{
    public Task<FirewallStatus> GetFirewallStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new FirewallStatus(FirewallStatusAvailability.Unavailable, "Windows Firewall status is unsupported on this platform."));
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            var enabled = key?.GetValue("EnableFirewall") as int?;
            var detail = (enabled == 0 ? "Windows Firewall is disabled for the standard profile." : "Windows Firewall status is available.") + " Hyper-V firewall status is unavailable; compute-service presence is not treated as firewall evidence.";
            return Task.FromResult(new FirewallStatus(FirewallStatusAvailability.Available, detail, null, null));
        }
        catch (Exception) { return Task.FromResult(new FirewallStatus(FirewallStatusAvailability.Unavailable, "Windows Firewall status could not be read.")); }
    }
    public Task<IReadOnlyList<PortCollisionStatus>> GetPortCollisionsAsync(IReadOnlyList<PortMapping> ports, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult<IReadOnlyList<PortCollisionStatus>>(ports.Select(x => new PortCollisionStatus(x.Port, x.Protocol, false, "Windows port ownership is unsupported on this platform.")).ToArray());
        try
        {
            var listeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port).ToHashSet();
            return Task.FromResult<IReadOnlyList<PortCollisionStatus>>(ports.Select(x => new PortCollisionStatus(x.Port, x.Protocol, x.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) && listeners.Contains(x.Port), listeners.Contains(x.Port) ? "A Windows TCP listener owns this port." : "No Windows TCP listener was found.")).ToArray());
        }
        catch (Exception) { return Task.FromResult<IReadOnlyList<PortCollisionStatus>>(ports.Select(x => new PortCollisionStatus(x.Port, x.Protocol, false, "Windows port ownership could not be read.")).ToArray()); }
    }
}

public sealed class GuardedFirewallOperationBroker : IFirewallOperationBroker
{
    private const string Group = "DistroNexus v2.3.0";
    public Task<FirewallOperationPreview> PreviewCreateAsync(FirewallRuleRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        var semantic = $"{request.Direction}|{request.Protocol}|{request.Port}|{string.Join(',', request.Profiles.Order())}|{request.RemoteScope}|{request.ExecutableScope}";
        var id = "DistroNexus-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semantic)))[..16];
        var preview = new FirewallOperationPreview(id, Group, request,
            [$"{request.Direction} {request.Protocol} port {request.Port} profiles: {string.Join(", ", request.Profiles)}.", $"Remote scope: {request.RemoteScope ?? "Any"}.", $"Executable scope: {request.ExecutableScope ?? "Any"}."], true);
        _previews[id] = preview;
        return Task.FromResult(preview);
    }
    private readonly Dictionary<string, FirewallOperationPreview> _previews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FirewallRemovalPreview> _removePreviews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FirewallRuleInfo> _ownedRules = new(StringComparer.Ordinal);
    public Task<IReadOnlyList<FirewallRuleInfo>> ListOwnedAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FirewallRuleInfo>>(_ownedRules.Values.OrderBy(x => x.RuleId, StringComparer.Ordinal).ToArray());
    public Task<FirewallOperationResult> CreateAsync(FirewallOperationPreview preview, CancellationToken cancellationToken = default)
    {
        preview.Request.Validate();
        if (!_previews.Remove(preview.RuleId, out var expected) || expected != preview) return Task.FromResult(new FirewallOperationResult(false, "PreviewRequired", "DN-8003: Generate and explicitly confirm a current firewall preview before execution."));
        return Task.FromResult(new FirewallOperationResult(false, "ElevatedHelperUnavailable", "DN-8003: Firewall rules require the signed DistroNexus elevated helper and explicit confirmation."));
    }
    /// <summary>Consumes a Core-issued create preview by its deterministic identity.</summary>
    public Task<FirewallOperationResult> CreateAsync(string previewRuleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewRuleId) || !_previews.TryGetValue(previewRuleId, out var preview))
            return Task.FromResult(new FirewallOperationResult(false, "PreviewRequired", "DN-8003: Generate and explicitly confirm a current firewall preview before execution."));
        return CreateAsync(preview, cancellationToken);
    }
    public Task<FirewallRemovalPreview> PreviewRemoveAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        if (!_ownedRules.TryGetValue(ruleId, out var rule)) throw new InvalidOperationException("DN-8003: Only DistroNexus-owned firewall rules may be removed.");
        var preview = new FirewallRemovalPreview(rule.RuleId, Group, [$"Remove DistroNexus-owned {rule.Protocol} {rule.Direction} firewall rule for port {rule.Port}.", "The signed elevated helper must confirm and execute this removal."], true, Guid.NewGuid().ToString("N"));
        _removePreviews[preview.Token] = preview;
        return Task.FromResult(preview);
    }
    public Task<FirewallOperationResult> RemoveAsync(FirewallRemovalPreview preview, CancellationToken cancellationToken = default)
    {
        if (!_removePreviews.Remove(preview.Token, out var expected) || expected != preview || !_ownedRules.ContainsKey(preview.RuleId)) return Task.FromResult(new FirewallOperationResult(false, "PreviewRequired", "DN-8003: Generate and explicitly confirm a current firewall removal preview before execution."));
        return Task.FromResult(new FirewallOperationResult(false, "ElevatedHelperUnavailable", "DN-8003: Firewall rule removal requires the signed DistroNexus elevated helper and explicit confirmation."));
    }
    /// <summary>Consumes a Core-issued removal preview by its one-time token.</summary>
    public Task<FirewallOperationResult> RemoveAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken) || !_removePreviews.TryGetValue(previewToken, out var preview))
            return Task.FromResult(new FirewallOperationResult(false, "PreviewRequired", "DN-8003: Generate and explicitly confirm a current firewall removal preview before execution."));
        return RemoveAsync(preview, cancellationToken);
    }
}
