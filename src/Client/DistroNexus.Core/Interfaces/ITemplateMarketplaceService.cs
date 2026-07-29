using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Owns source provenance, remote manifest validation, trust, and immutable template artifacts.</summary>
public interface ITemplateMarketplaceService
{
    Task<IReadOnlyList<TemplateSource>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task<TemplateSource> AddSourceAsync(string url, TemplateSourceKind kind, bool explicitlyAcceptedNonHttps, CancellationToken cancellationToken = default);
    Task SetSourceEnabledAsync(string sourceId, bool enabled, CancellationToken cancellationToken = default);
    Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<TemplateManifestV2> FetchManifestAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<TemplateMarketplaceCatalogV2> FetchCatalogAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<TemplateMarketplaceStatus> GetStatusAsync(string sourceId, CancellationToken cancellationToken = default);
    /// <summary>Returns status for one immutable catalog entry, never the source's first entry.</summary>
    Task<TemplateMarketplaceStatus> GetStatusAsync(string sourceId, string templateId, string manifestDigest, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateMarketplaceEntry>> DiscoverAsync(CancellationToken cancellationToken = default);
    /// <summary>Downloads a v2 artifact through the source boundary and stores it only after integrity verification.</summary>
    Task<TemplateArtifact> DownloadArtifactAsync(string sourceId, TemplateManifestV2 manifest, CancellationToken cancellationToken = default);
    Task<TemplateArtifact> DownloadArtifactAsync(string sourceId, string templateId, string manifestDigest, CancellationToken cancellationToken = default);
    /// <summary>Creates a one-shot Core-owned grant after binding the complete immutable review material.</summary>
    Task<TemplateReviewGrant> CreateReviewGrantAsync(string sourceId, string sha256, CancellationToken cancellationToken = default);
    /// <summary>Consumes a one-shot Core-owned review grant. It is not known-good until application succeeds.</summary>
    Task<TemplateArtifact> ApproveCandidateAsync(string reviewToken, CancellationToken cancellationToken = default);
    Task<TemplateManifestV2?> GetReviewedManifestForExecutionAsync(string sourceUrl, string templateId, CancellationToken cancellationToken = default);
    /// <summary>Returns only the exact reviewed candidate requested by a materialized template.</summary>
    Task<TemplateManifestV2?> GetAuthorizedManifestForExecutionAsync(string sourceUrl, string templateId, string manifestDigest, string artifactSha256, CancellationToken cancellationToken = default);
    Task VerifyKnownGoodForExecutionAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default);
    Task<TemplateArtifact> GetVerifiedArtifactForExecutionAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default);
    Task CompleteSuccessfulExecutionAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default);
    TemplateManifestV2 ValidateManifest(string manifestJson, bool isRemote);
    string GetManifestDigest(TemplateManifestV2 manifest);
    byte[] CanonicalizeManifestForSignature(TemplateManifestV2 manifest);
    void VerifyManifestSignature(TemplateManifestV2 manifest);
    Task<TemplateArtifact> StoreArtifactAsync(TemplateManifestV2 manifest, Stream archive, CancellationToken cancellationToken = default);
    Task<TemplateUpdateReview> ReviewUpdateAsync(TemplateManifestV2 previous, TemplateManifestV2 candidate, CancellationToken cancellationToken = default);
    Task<TemplateUpdateReview> ReviewUpdateAsync(string sourceId, string templateId, string manifestDigest, CancellationToken cancellationToken = default);
    Task<TemplateScriptDiff> ReviewScriptDiffAsync(string templateId, string candidateSha256, CancellationToken cancellationToken = default);
    Task<TemplateArtifact?> GetKnownGoodArtifactAsync(string templateId, CancellationToken cancellationToken = default);
    Task<TemplateManifestV2?> GetKnownGoodManifestAsync(string templateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateArtifactHistoryEntry>> GetArtifactHistoryAsync(string templateId, CancellationToken cancellationToken = default);
    Task RollbackAsync(string templateId, string sha256, CancellationToken cancellationToken = default);
}
