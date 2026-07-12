using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface ISystemdService
{
    Task<IReadOnlyList<SystemdServiceInfo>> ListAsync(string instanceName, SystemdScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemdJournalEntry>> GetJournalAsync(string instanceName, SystemdUnitName unit, SystemdScope scope, string? search = null, int lineLimit = 200, CancellationToken cancellationToken = default);
    Task<SystemdServiceDetails?> GetDetailsAsync(string instanceName, SystemdUnitName unit, SystemdScope scope, CancellationToken cancellationToken = default);
    Task<SystemdOperationPreview> PreviewAsync(string instanceName, SystemdUnitName unit, SystemdAction action, SystemdScope scope, CancellationToken cancellationToken = default);
    Task<SystemdOperationResult> ExecuteAsync(SystemdOperationPreview preview, CancellationToken cancellationToken = default);
}

public interface INetworkDiagnosticsService
{
    Task<NetworkProbeResult> ProbeAsync(NetworkProbeRequest request, CancellationToken cancellationToken = default);
    NetworkingModeGuidance GetModeGuidance(WslNetworkingMode mode, PlatformCapabilitySnapshot capabilities);
}

/// <summary>Bounded WSL-side probe boundary. Implementations use fixed helpers and never interpolate targets into a shell.</summary>
public interface IWslNetworkDiagnosticsAdapter
{
    Task<NetworkProbeResult> ProbeAsync(NetworkProbeRequest request, CancellationToken cancellationToken = default);
}

public interface INetworkConfigurationService
{
    Task<NetworkingModeGuidance> GetGuidanceAsync(WslNetworkingMode mode, CancellationToken cancellationToken = default);
    Task<NetworkModePreview> PreviewModeAsync(WslNetworkingMode mode, CancellationToken cancellationToken = default);
    Task<ConfigurationSaveResult> ApplyModeAsync(WslNetworkingMode mode, string previewToken, CancellationToken cancellationToken = default);
    Task<NetworkSettings> ReadSettingsAsync(CancellationToken cancellationToken = default);
    Task<NetworkSettingsPreview> PreviewSettingsAsync(NetworkSettings settings, CancellationToken cancellationToken = default);
    Task<ConfigurationSaveResult> ApplySettingsAsync(NetworkSettings settings, string previewToken, CancellationToken cancellationToken = default);
}

public enum FirewallStatusAvailability { Available, Unavailable }
public sealed record FirewallStatus(FirewallStatusAvailability Availability, string Detail, bool? WslIntegrationEnabled = null, bool? HyperVAvailable = null);
public sealed record PortCollisionStatus(int Port, string Protocol, bool IsCollision, string Detail);
public interface INetworkStatusAdapter
{
    Task<FirewallStatus> GetFirewallStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortCollisionStatus>> GetPortCollisionsAsync(IReadOnlyList<PortMapping> ports, CancellationToken cancellationToken = default);
}

/// <summary>Only a signed, allow-listed elevated helper may implement firewall mutation.</summary>
public interface IFirewallOperationBroker
{
    Task<IReadOnlyList<FirewallRuleInfo>> ListOwnedAsync(CancellationToken cancellationToken = default);
    Task<FirewallOperationPreview> PreviewCreateAsync(FirewallRuleRequest request, CancellationToken cancellationToken = default);
    Task<FirewallOperationResult> CreateAsync(FirewallOperationPreview preview, CancellationToken cancellationToken = default);
    Task<FirewallRemovalPreview> PreviewRemoveAsync(string ruleId, CancellationToken cancellationToken = default);
    Task<FirewallOperationResult> RemoveAsync(FirewallRemovalPreview preview, CancellationToken cancellationToken = default);
}

/// <summary>Boundary implemented by the signed short-lived elevated helper in packaged builds.</summary>
public interface IElevatedFirewallHelper
{
    Task<FirewallOperationResult> ExecuteAsync(FirewallOperationPreview preview, CancellationToken cancellationToken = default);
}
