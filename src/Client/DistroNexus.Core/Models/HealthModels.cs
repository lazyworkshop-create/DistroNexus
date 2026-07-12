namespace DistroNexus.Core.Models;

public enum HealthSeverity { Healthy, Information, Warning, Critical }
public enum HealthScope { Host, Instance }
public enum RepairSafety { Safe, RequiresConfirmation, PrivilegedOrDisruptive }
public enum RepairIdempotency { Idempotent, NonIdempotent }

public sealed record HealthFinding(
    string Id, HealthSeverity Severity, HealthScope Scope, string Title, string Detail,
    string? InstanceName = null, string? RepairId = null,
    IReadOnlyDictionary<string, string>? Evidence = null);

/// <summary>Normalized, injectable observations.  Adapters own all host/WSL I/O so checks stay deterministic.</summary>
public sealed record HealthProbeSnapshot(
    IReadOnlyDictionary<string, HealthProbeState> Network,
    IReadOnlyDictionary<string, HealthProbeState> SystemdUnits,
    IReadOnlyDictionary<string, BackupHealthState> Backups,
    IReadOnlyDictionary<string, TemplateHealthState> Templates,
    IReadOnlyDictionary<string, StorageHealthState> Storage);
public sealed record HealthProbeState(string State, string Detail, IReadOnlyDictionary<string, string>? Evidence = null);
public sealed record WindowsOptionalFeatureState(string FeatureName, bool? Enabled, string Detail);
public sealed record WindowsPrerequisiteSnapshot(IReadOnlyList<WindowsOptionalFeatureState> OptionalFeatures, bool? VirtualizationEnabled, string VirtualizationDetail);
public sealed record BackupHealthState(bool DestinationExists, long? FreeBytes, int ConsecutiveFailures, string Detail, long? WarningThresholdBytes = null);
public sealed record BackupHealthRecord(string InstanceName, DateTimeOffset CompletedAt, bool Succeeded, string? ErrorCode = null, string? Detail = null);
public sealed record StructuredErrorRecord(DateTimeOffset OccurredAt, string Code, string Operation, string Message);
public sealed record TemplateHealthState(string DeclaredState, string Detail);
public sealed record StorageHealthState(long? HostFreeBytes, long? HostTotalBytes, long? VhdxBytes, long? LinuxFilesystemFreeBytes, long? ReclaimableBytes, string Detail);

public sealed record HealthCheckDescriptor(string Id, HealthScope Scope, IReadOnlyCollection<CapabilityId> Prerequisites);
public sealed record HealthCheckResult(string CheckId, IReadOnlyList<HealthFinding> Findings, DateTimeOffset CompletedAt);
public sealed record HealthScanResult(Guid ScanId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt,
    IReadOnlyList<HealthFinding> Findings, bool WasCancelled = false);
public sealed record HealthHistoryEntry(DateTimeOffset CompletedAt, int Healthy, int Information, int Warning, int Critical);

public sealed record RepairPreview(string RepairId, string Title, RepairSafety Safety, RepairIdempotency Idempotency,
    IReadOnlyList<string> Changes, IReadOnlyList<string> Commands, string? BackupPath = null, string? PreviewToken = null, IReadOnlyList<string>? Preconditions = null,
    string? Reversibility = null, IReadOnlyList<string>? UndoSteps = null);
public sealed record RepairResult(string RepairId, bool Succeeded, IReadOnlyList<string> Results,
    string? BackupPath = null, string? Error = null, bool PostconditionSatisfied = false,
    RepairIdempotency? Idempotency = null, IReadOnlyList<string>? NextSteps = null);
public sealed record RepairExecutionRequest(string PreviewToken, bool Confirmed);

public enum DiagnosticReportFormat { Markdown, Json }
/// <summary>The preview token pins an exact, already-redacted report snapshot for export.</summary>
public sealed record DiagnosticReportRequest(DiagnosticReportFormat Format, bool Redact, IReadOnlyList<string>? SelectedLogs = null, string? PreviewToken = null);
public sealed record DiagnosticReportPreview(DiagnosticReportFormat Format, string Content, IReadOnlyList<string> IncludedSections, string SnapshotToken);
