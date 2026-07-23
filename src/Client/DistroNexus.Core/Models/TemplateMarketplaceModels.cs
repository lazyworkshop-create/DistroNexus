namespace DistroNexus.Core.Models;

/// <summary>Capabilities declared by a template and reviewed before it is trusted.</summary>
public enum TemplateCapability { NetworkAccess, PackageManager, Root, WindowsInterop, FilesystemPaths, ServiceChanges, ContainerAccess }
public enum TemplateSourceKind { BuiltIn, UserLocal, Remote }
public enum TemplateTrustState { BuiltIn, Untrusted, Trusted, ReviewRequired, Revoked }
public enum TemplateSignatureStatus { NotPresent, Verified, Invalid }

public sealed record TemplateSource(string Id, string Url, TemplateSourceKind Kind, string? PublisherFingerprint = null, bool IsEnabled = true, DateTimeOffset? LastFetchedAt = null);

/// <summary>Version 2 remote manifest. Remote version 1 content is intentionally never executable.</summary>
public sealed record class TemplateManifestV2
{
    public int SchemaVersion { get; init; } = 2;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string ArtifactUrl { get; init; } = string.Empty;
    public string ArtifactSha256 { get; init; } = string.Empty;
    public string PublisherFingerprint { get; init; } = string.Empty;
    /// <summary>Base64 Ed25519 public key. Required whenever a detached signature is supplied.</summary>
    public string? PublisherPublicKey { get; init; }
    public string? PublisherSignature { get; init; }
    public IReadOnlyList<TemplateCapability> Capabilities { get; init; } = [];
    public IReadOnlyList<string> ScriptHashes { get; init; } = [];
    /// <summary>Every file-backed executable path that template.json may reference, bound to exact content.</summary>
    public IReadOnlyList<TemplateExecutableFile> ExecutableFiles { get; init; } = [];
    public IReadOnlyList<string> HealthChecks { get; init; } = [];
    public string Compatibility { get; init; } = string.Empty;
}
public sealed record TemplateExecutableFile(string Path, string Sha256);
/// <summary>Versioned remote catalog. A single manifest is accepted only as legacy compatibility input.</summary>
public sealed record class TemplateMarketplaceCatalogV2
{
    public int SchemaVersion { get; init; } = 2;
    public string? CatalogSha256 { get; init; }
    public string? PublisherPublicKey { get; init; }
    public string? PublisherFingerprint { get; init; }
    public string? CatalogSignature { get; init; }
    public IReadOnlyList<TemplateManifestV2> Templates { get; init; } = [];
}

public sealed record TemplateArtifact(string Sha256, string RootPath, DateTimeOffset CachedAt, string? TemplateId = null, string? Version = null);
/// <summary>Immutable, source-bound record retained for review, execution and rollback.</summary>
public sealed record TemplateArtifactHistoryEntry(TemplateManifestV2 Manifest, TemplateArtifact Artifact, DateTimeOffset RecordedAt, string? SourceUrl = null);
/// <summary>Immutable review evidence. It permits one exact candidate execution, never broad source trust.</summary>
public sealed record TemplateReviewAuthorization(string SourceUrl, string PublisherFingerprint, string? PublisherPublicKey, TemplateManifestV2 Manifest, TemplateArtifact Artifact, DateTimeOffset ApprovedAt, string ManifestDigest = "");
/// <summary>Bounded normalized review material; no raw archive paths or unbounded script content is retained.</summary>
public sealed record TemplateScriptDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed, IReadOnlyList<TemplateScriptTextChange>? TextChanges = null, bool IsTruncated = false);
public sealed record TemplateScriptTextChange(string ScriptId, string PreviousText, string CandidateText, bool IsTruncated);
/// <summary>One-shot Core-issued review grant, bound to all material candidate declarations.</summary>
public sealed record TemplateReviewGrant(string Token, string SourceId, string NormalizedSourceUrl, TemplateManifestV2 Manifest, TemplateArtifact Artifact, TemplateScriptDiff ScriptDiff, DateTimeOffset ExpiresAt, string ManifestDigest = "");
public sealed record TemplateUpdateReview(string TemplateId, string PreviousSha256, string CandidateSha256, IReadOnlyList<TemplateCapability> NewlyRequestedCapabilities, bool ScriptsChanged, bool PublisherChanged, bool RequiresReview);
public sealed record TemplateMarketplaceEntry(TemplateManifestV2 Manifest, TemplateSource Source, TemplateTrustState TrustState, TemplateArtifact? KnownGoodArtifact, bool CanExecute, string ExecutionReason, string ManifestDigest = "");
/// <summary>Core-derived display-safe status; callers must not infer signature or authorization from manifest fields.</summary>
public sealed record TemplateMarketplaceStatus(TemplateManifestV2? Manifest, TemplateSignatureStatus SignatureStatus, TemplateTrustState TrustState, bool HasEffectiveReviewAuthorization, bool CanExecute, string Reason);
