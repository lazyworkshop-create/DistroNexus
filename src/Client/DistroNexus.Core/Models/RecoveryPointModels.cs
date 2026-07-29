namespace DistroNexus.Core.Models;

public enum RecoveryPointFormat { Tar, Vhdx }
public enum RecoveryPointVerification { Unknown, Verified, Corrupt, Missing }

public sealed record RecoveryPointManifest(
    int SchemaVersion,
    Guid Id,
    string Name,
    string SourceInstance,
    int SourceWslVersion,
    RecoveryPointFormat Format,
    DateTimeOffset CreatedAt,
    string PayloadFile,
    long SizeBytes,
    string Sha256,
    string ApplicationVersion,
    IReadOnlyList<string> Tags,
    string Description,
    bool Pinned = false);

public sealed record RecoveryPointSummary(RecoveryPointManifest Manifest, string DirectoryPath, RecoveryPointVerification Verification);

public sealed record RecoveryPointCreateRequest(string SourceInstance, string Name, string DestinationRoot,
    RecoveryPointFormat Format = RecoveryPointFormat.Tar, string Description = "", IReadOnlyList<string>? Tags = null,
    bool RestartAfterExport = false);

public sealed record RecoveryRestoreRequest(Guid RecoveryPointId, string TargetInstance, string TargetDirectory,
    bool VerifyChecksum = true, bool ImportInPlace = false);

public sealed record RecoveryCloneRequest(RecoveryPointCreateRequest Snapshot, string TargetInstance, string TargetDirectory, bool ImportInPlace = false);

public sealed record RecoveryOperationPreview(string Token, string Operation, Guid? RecoveryPointId, string SourceInstance,
    string TargetInstance, string TargetDirectory, RecoveryPointFormat Format, bool RequiresStop, bool RequiresCapability,
    IReadOnlyList<string> Warnings, long? EstimatedBytes = null, string RequestFingerprint = "");

public sealed record RecoveryHistoryEntry(string Id, string InstanceName, DateTimeOffset CreatedAt, string Kind, string Status,
    string? Location = null, Guid? RecoveryPointId = null);

public sealed record RecoveryRetentionPreview(string Token, string SourceInstance, int Maximum, int? CurrentMaximum,
    int CandidateDeletionCount, string RequestFingerprint);

/// <summary>Non-sensitive status emitted by a recovery operation for the UI.</summary>
public sealed record RecoveryOperationProgress(string Operation, string Stage, int? Percent = null);
