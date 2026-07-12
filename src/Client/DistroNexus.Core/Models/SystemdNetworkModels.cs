using System.Net;
using System.Text.RegularExpressions;

namespace DistroNexus.Core.Models;

public enum SystemdScope { System, User }
public enum SystemdAction { Start, Stop, Restart, Enable, Disable, Reload }

/// <summary>Validated systemd unit identifier.  It deliberately rejects paths, whitespace and shell syntax.</summary>
public sealed record SystemdUnitName
{
    private static readonly Regex Valid = new("^[A-Za-z0-9][A-Za-z0-9_.@:-]{0,254}\\.(service|socket|target|timer|mount|path)$", RegexOptions.CultureInvariant);
    public string Value { get; }
    public SystemdUnitName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Valid.IsMatch(value))
            throw new ArgumentException("The systemd unit name is invalid.", nameof(value));
        Value = value;
    }
    public override string ToString() => Value;
}

public sealed record SystemdServiceInfo(SystemdUnitName Name, string Description, string ActiveState,
    string SubState, string EnabledState, string LoadState, SystemdScope Scope);
public sealed record SystemdJournalEntry(string Timestamp, string Severity, string Message);
public sealed record SystemdServiceDetails(SystemdServiceInfo Service, IReadOnlyList<string> Dependencies, string? UnitFilePath);
public sealed record SystemdOperationPreview(string InstanceName, SystemdUnitName Unit, SystemdAction Action,
    SystemdScope Scope, bool RequiresLinuxPrivilege, IReadOnlyList<string> Effects, IReadOnlyList<string> Preconditions,
    string PreviewToken);
public sealed record SystemdOperationResult(bool Succeeded, string OutcomeCode, SystemdServiceInfo? Service, string? Guidance = null);

public enum NetworkProbeKind { Dns, Gateway, Internet, WindowsHost, WslInstance, Localhost, TcpEndpoint }
public enum NetworkProbeOutcome { Resolved, RouteMissing, Refused, TimedOut, PermissionDenied, ToolUnavailable, InvalidInput }
public sealed record NetworkProbeRequest(NetworkProbeKind Kind, string Host, int? Port = null, TimeSpan? Timeout = null, string? DistributionName = null);
public sealed record NetworkProbeResult(NetworkProbeRequest Request, NetworkProbeOutcome Outcome, string Detail, IPAddress[]? Addresses = null);

public enum WslNetworkingMode { Nat, Mirrored, None, VirtioProxy, Bridged }
public sealed record NetworkingModeGuidance(WslNetworkingMode Mode, bool IsSupported, bool IsRecommended,
    IReadOnlyList<string> CompatibilityNotes, RestartScope RestartScope);
public sealed record NetworkModePreview(WslNetworkingMode Mode, ConfigurationPreview Configuration, NetworkingModeGuidance Guidance, string Token);
public sealed record NetworkSettings(bool? DnsTunneling = null, bool? AutoProxy = null, bool? Firewall = null, bool? HostAddressLoopback = null, bool? BestEffortDnsParsing = null, string? IgnoredPorts = null);
public sealed record NetworkSettingsPreview(NetworkSettings Settings, ConfigurationPreview Configuration, string Token);

public enum FirewallDirection { Inbound, Outbound }
public enum FirewallProtocol { Tcp, Udp }
public sealed record FirewallRuleRequest(FirewallDirection Direction, FirewallProtocol Protocol, int Port,
    IReadOnlyList<string> Profiles, string? RemoteScope = null, string? ExecutableScope = null)
{
    public void Validate()
    {
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (Profiles.Count == 0 || Profiles.Any(x => x is not ("Domain" or "Private" or "Public"))) throw new ArgumentException("At least one valid firewall profile is required.", nameof(Profiles));
        if (RemoteScope is { Length: > 0 } && !IsSafeRemoteScope(RemoteScope)) throw new ArgumentException("Remote scope must be an IP address, CIDR range, or LocalSubnet.", nameof(RemoteScope));
        if (ExecutableScope is { Length: > 0 } && (!Path.IsPathFullyQualified(ExecutableScope) || ExecutableScope.Contains('\n') || ExecutableScope.Contains('\r') || ExecutableScope.Contains('\0'))) throw new ArgumentException("Executable scope is invalid.", nameof(ExecutableScope));
    }
    private static bool IsSafeRemoteScope(string value)
    {
        if (value == "LocalSubnet") return true;
        var parts = value.Split('/', 2);
        return IPAddress.TryParse(parts[0], out _) && (parts.Length == 1 || (int.TryParse(parts[1], out var bits) && bits is >= 0 and <= 128));
    }
}
public sealed record FirewallOperationPreview(string RuleId, string Group, FirewallRuleRequest Request, IReadOnlyList<string> Effects, bool RequiresElevation);
public sealed record FirewallRuleInfo(string RuleId, string Group, FirewallDirection Direction, FirewallProtocol Protocol, int Port, IReadOnlyList<string> Profiles);
public sealed record FirewallRemovalPreview(string RuleId, string Group, IReadOnlyList<string> Effects, bool RequiresElevation, string Token);
public sealed record FirewallOperationResult(bool Succeeded, string OutcomeCode, string? Guidance = null);
