using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IHealthCheck
{
    HealthCheckDescriptor Descriptor { get; }
    Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken);
}

public sealed record HealthCheckContext(PlatformCapabilitySnapshot Host, IReadOnlyList<WslInstance> Instances);

public interface IHealthProbe
{
    Task<HealthProbeSnapshot> ProbeAsync(HealthCheckContext context, CancellationToken cancellationToken = default);
}

/// <summary>Bounded read-only platform probes used by the Health Center.</summary>
public interface IHealthRuntimeAdapter
{
    Task<IReadOnlyDictionary<string, HealthProbeState>> ProbeNetworkAsync(HealthCheckContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, HealthProbeState>> ProbeSystemdAsync(HealthCheckContext context, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, StorageHealthState>> ProbeStorageAsync(HealthCheckContext context, CancellationToken cancellationToken = default);
}

/// <summary>Supplies an explicitly configured, bounded TCP endpoint for localhost-forwarding
/// diagnostics. Returning null deliberately disables the connect probe; Health Center never
/// guesses an application port such as SSH.</summary>
public interface ILocalhostForwardingEndpointStrategy
{
    HealthTcpEndpoint? GetEndpoint(HealthCheckContext context, string networkingMode);
}

public sealed record HealthTcpEndpoint(string Host, int Port)
{
    public bool IsValid => (Host.Equals("127.0.0.1", StringComparison.Ordinal) || Host.Equals("::1", StringComparison.OrdinalIgnoreCase) || Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) && Port is > 0 and <= 65535;
}

/// <summary>Evaluates only safe, install-time template preflight contracts against the recorded instance.</summary>
public interface ITemplateRuntimePreflightEvaluator
{
    Task<IReadOnlyList<TemplateRuntimePreflightResult>> EvaluateAsync(TemplateApplicationRecord record, CancellationToken cancellationToken = default);
}

public sealed record TemplateRuntimePreflightResult(string Id, bool Required, string State, string Detail);

/// <summary>Read-only Windows prerequisite observations used by Health Center.  The probe is
/// injectable so feature and firmware states can be covered without changing the host.</summary>
public interface IWindowsPrerequisiteProbe
{
    Task<WindowsPrerequisiteSnapshot> ProbeAsync(CancellationToken cancellationToken = default);
}

public interface IDiagnosticLogProvider
{
    IReadOnlyCollection<string> AllowedLogIds { get; }
    Task<string> ReadAsync(string logId, int maximumCharacters, CancellationToken cancellationToken = default);
}

/// <summary>Provides persisted, typed backup execution history without exposing backup artifacts.</summary>
public interface IBackupHealthSource
{
    Task<IReadOnlyDictionary<string, BackupHealthState>> GetHealthAsync(CancellationToken cancellationToken = default);
    Task RecordAsync(BackupHealthRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Returns a bounded, redacted projection of application errors for diagnostic export.</summary>
public interface IStructuredErrorProvider
{
    Task<IReadOnlyList<StructuredErrorRecord>> GetRecentAsync(int maximumEntries, CancellationToken cancellationToken = default);
}

public interface IHealthOrchestrator
{
    Task<HealthScanResult> ScanAsync(IProgress<HealthFinding>? progress = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IRepairAction
{
    string Id { get; }
    Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default);
    Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default);
}

public interface IHealthRepairService
{
    Task<RecoveryOffer> GetRecoveryOfferAsync(HealthFinding finding, CancellationToken cancellationToken = default) => Task.FromResult(new RecoveryOffer(false, finding.InstanceName ?? "", RecoveryOfferReason.DestructiveRepair, "RecoveryOffer.Unavailable"));
    Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default);
    Task<RepairResult> ExecuteAsync(HealthFinding finding, RepairExecutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Starts an explicitly consented elevated Windows feature flow. Implementations must
/// never enable a feature merely because a health scan ran.</summary>
public interface IWindowsFeatureRepairBroker
{
    Task<RepairResult> StartAsync(HealthFinding finding, CancellationToken cancellationToken = default);
}

/// <summary>Publishes an in-app navigation request without coupling Core to WPF.</summary>
public interface IHealthNavigationBroker
{
    void Request(string target, HealthFinding finding);
}

public interface IDiagnosticReportService
{
    Task<DiagnosticReportPreview> PreviewAsync(DiagnosticReportRequest request, CancellationToken cancellationToken = default);
    Task<string> ExportAsync(DiagnosticReportRequest request, string path, CancellationToken cancellationToken = default);
}
