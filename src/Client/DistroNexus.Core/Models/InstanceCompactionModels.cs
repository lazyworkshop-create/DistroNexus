namespace DistroNexus.Core.Models;

/// <summary>Truthful, read-only information used to review a compaction request.</summary>
public sealed record InstanceCompactionPreview(
    string PreviewToken,
    string InstanceName,
    long CurrentSizeBytes,
    string EstimateKind,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> Warnings,
    DateTimeOffset ExpiresAt);

/// <summary>Path-free result of a reviewed compaction execution.</summary>
public sealed record InstanceCompactionResult(
    bool Succeeded,
    string InstanceName,
    string OutcomeCode,
    long? BeforeBytes,
    long? AfterBytes,
    long? SavedBytes,
    string Method,
    bool Restarted,
    string RecoveryAction = "None");

/// <summary>Opaque registered state used exclusively by the fixed compaction adapter.</summary>
public sealed record RegisteredInstanceCompactionState(
    string Name,
    string Identity,
    string VhdxIdentity,
    bool IsRunning,
    long CurrentSizeBytes,
    string Method,
    string PrerequisiteOutcome);
