namespace DistroNexus.Core.Models;

/// <summary>Path-free catalog source state returned by the install resolver.</summary>
public sealed record InstallSourceResolution(string PackageId, string CacheState, bool DownloadAvailable, string ExpectedSha256, long ExpectedSizeBytes, string SourceProvenance);
public sealed record PackageAcquisitionPreview(string PreviewToken, string PackageId, DateTimeOffset ExpiresAt);
public sealed record PackageAcquisitionResult(string PackageReference, string PackageId, string Sha256, long SizeBytes, DateTimeOffset ExpiresAt, string OutcomeCode);
public sealed record InstallPreview(string PreviewToken, string InstanceName, DateTimeOffset ExpiresAt);
public sealed record VerifiedInstallResult(bool Succeeded, string Operation, string InstanceName, string OutcomeCode, LifecycleRecoveryAction RecoveryAction = LifecycleRecoveryAction.None, string? RecoveryId = null);
