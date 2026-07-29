namespace DistroNexus.Core.Models;

/// <summary>Public, modeled representation of an instance configuration document.</summary>
public sealed record InstanceConfigurationReadResult(string Name, int SchemaRevision, IReadOnlyDictionary<string, string> Document, string Fingerprint, string OutcomeCode);
public sealed record InstanceConfigurationRecoveryResult(string Name, string OfferState, string? RecoveryFingerprint, string OutcomeCode);
public sealed record InstanceConfigurationPreviewResult(string PreviewToken, DateTimeOffset ExpiresAt, string Name, IReadOnlyList<string> ChangeSummary, string OutcomeCode);
public sealed record InstanceConfigurationSaveResult(string Name, bool BackupCreated, string RecoveryAction, string OutcomeCode);
public sealed record InstallTargetPreviewResult(string PreviewToken, DateTimeOffset ExpiresAt, string DisplayName, long AvailableBytes, long RequiredBytes, bool IsEligible, string OutcomeCode);
