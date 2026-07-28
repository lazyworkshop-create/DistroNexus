namespace DistroNexus.Core.Models;

public enum LifecyclePathOperation { Remove, Move, Rename, Export, Import }
public enum LifecycleRecoveryAction { None, RetryRecovery, ManualRecoveryRequired }

/// <summary>Sanitized, authority-free preview returned for a path-bearing lifecycle request.</summary>
public sealed record LifecycleOperationPreview(string PreviewToken, LifecyclePathOperation Operation, string InstanceName, DateTimeOffset ExpiresAt);
/// <summary>The only public result shape for a path-bearing lifecycle execution.</summary>
public sealed record LifecycleOperationResult(bool Succeeded, LifecyclePathOperation Operation, string InstanceName, string OutcomeCode, LifecycleRecoveryAction RecoveryAction = LifecycleRecoveryAction.None, string? RecoveryId = null);
internal sealed record LifecycleOperationGrant(string Sid, LifecyclePathOperation Operation, string InstanceName, string? NewName, bool KeepFiles, bool StopRunning, string? Source, string? Target, string Fingerprint, DateTimeOffset ExpiresAt, string? ReservationId);
internal sealed record LifecycleRecoveryRecord(string Id, LifecyclePathOperation Operation, string InstanceName, string Checkpoint, DateTimeOffset CreatedAt, string OutcomeCode);
