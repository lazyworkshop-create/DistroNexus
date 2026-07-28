namespace DistroNexus.Core.Models;

/// <summary>Presentation-safe template metadata. Script bodies and Core paths never cross the module boundary.</summary>
public sealed record TemplateDisplay(
    string Id,
    string Name,
    string Description,
    string Category,
    string Version,
    string Author,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> CompatibleDistros,
    int EstimatedDurationMinutes,
    long EstimatedDiskSpaceMB,
    bool IsOfficial,
    bool IsCustom,
    TemplateTrustState TrustState,
    IReadOnlyList<TemplateCapability> Capabilities);

public sealed record TemplateSourceDisplay(
    string Id,
    string Url,
    TemplateSourceKind Kind,
    string? PublisherFingerprint,
    bool IsEnabled,
    DateTimeOffset? LastFetchedAt);

public sealed record TemplateMarketplaceEntryDisplay(
    string SourceId,
    string TemplateId,
    string Name,
    string Version,
    string ManifestDigest,
    TemplateTrustState TrustState,
    bool CanExecute,
    string ExecutionReason,
    IReadOnlyList<TemplateCapability> Capabilities);

public sealed record TemplateMarketplaceStatusDisplay(
    string SourceId,
    string TemplateId,
    string ManifestDigest,
    TemplateSignatureStatus SignatureStatus,
    TemplateTrustState TrustState,
    bool HasEffectiveReviewAuthorization,
    bool CanExecute,
    string Reason);

public sealed record TemplateReviewDisplay(
    string ReviewToken,
    string SourceId,
    string TemplateId,
    string TemplateVersion,
    string ManifestDigest,
    string NormalizedSourceIdentity,
    string ArtifactSha256,
    string ScriptDiffDigest,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<TemplateCapability> Capabilities,
    IReadOnlyList<string> ChangedScriptIdentifiers,
    int AddedScriptCount,
    int RemovedScriptCount,
    int ChangedScriptCount,
    bool IsTruncated);

public sealed record TemplateArtifactDisplay(string TemplateId, string? Version, string Sha256, DateTimeOffset CachedAt);
public sealed record TemplateArtifactHistoryDisplay(string TemplateId, string Version, string ArtifactSha256, DateTimeOffset RecordedAt, string? SourceUrl);
public sealed record TemplateLocalPreview(string PreviewToken, string Operation, string TemplateId, DateTimeOffset ExpiresAt);
public sealed record TemplateLocalMutationResult(TemplateDisplay Template);
public sealed record TemplateExportResult(string Content);
