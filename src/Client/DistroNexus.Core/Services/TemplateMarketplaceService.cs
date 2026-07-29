using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Concurrent;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace DistroNexus.Core.Services;

/// <summary>Verifies and retains immutable template artifacts; trust is scoped to URL and key fingerprint.</summary>
public sealed class TemplateMarketplaceService : ITemplateMarketplaceService
{
    private static readonly TemplateSource BuiltInSource = new("builtin", "distronexus://built-in", TemplateSourceKind.BuiltIn, "DistroNexus", true);
    private const long MaximumCatalogBytes = 1L * 1024 * 1024;
    private const long MaximumArchiveBytes = 64L * 1024 * 1024;
    private const long MaximumExpandedBytes = 256L * 1024 * 1024;
    private readonly string _root, _sourcesPath, _trustPath, _knownGoodPath, _knownGoodRetentionPath, _artifactHistoryPath, _authorizationPath, _catalogCachePath;
    // Marketplace state predates the common persistence contract.  Keep the original file
    // names for migration compatibility, but route every read/write through the versioned
    // atomic store (schema/revision/backup/newer-schema behavior is then consistent with
    // the rest of the application).
    private readonly VersionedJsonStore<List<TemplateSource>> _sources;
    private readonly VersionedJsonStore<Dictionary<string, TemplateTrustState>> _trust;
    private readonly VersionedJsonStore<Dictionary<string, TemplateArtifact>> _knownGood;
    private readonly VersionedJsonStore<Dictionary<string, List<string>>> _knownGoodRetention;
    private readonly VersionedJsonStore<Dictionary<string, List<TemplateArtifactHistoryEntry>>> _history;
    private readonly VersionedJsonStore<Dictionary<string, TemplateReviewAuthorization>> _authorizations;
    private readonly VersionedJsonStore<Dictionary<string, TemplateMarketplaceCatalogV2>> _catalogCache;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TemplateMarketplaceReviewGrantStore _reviewGrants;
    private static readonly TimeSpan ReviewGrantLifetime = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public TemplateMarketplaceService(string? appDataDirectory = null, HttpClient? httpClient = null)
    {
        _root = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _sourcesPath = Path.Combine(_root, "template-sources.json"); _trustPath = Path.Combine(_root, "template-trust.json"); _knownGoodPath = Path.Combine(_root, "template-known-good.json"); _knownGoodRetentionPath = Path.Combine(_root, "template-known-good-retention.json"); _artifactHistoryPath = Path.Combine(_root, "template-artifact-history.json"); _authorizationPath = Path.Combine(_root, "template-review-authorizations.json"); _catalogCachePath = Path.Combine(_root, "template-catalog-cache.json");
        _sources = new(_sourcesPath, 2, n => n.Deserialize<List<TemplateSource>>(Json) ?? [], new Dictionary<int, Func<List<TemplateSource>, List<TemplateSource>>> { [1] = value => value });
        _trust = new(_trustPath, 2, n => n.Deserialize<Dictionary<string, TemplateTrustState>>(Json) ?? [], new Dictionary<int, Func<Dictionary<string, TemplateTrustState>, Dictionary<string, TemplateTrustState>>> { [1] = value => value });
        _knownGood = new(_knownGoodPath, 2, n => n.Deserialize<Dictionary<string, TemplateArtifact>>(Json) ?? [], new Dictionary<int, Func<Dictionary<string, TemplateArtifact>, Dictionary<string, TemplateArtifact>>> { [1] = value => value });
        _knownGoodRetention = new(_knownGoodRetentionPath, 1, n => n.Deserialize<Dictionary<string, List<string>>>(Json) ?? []);
        _history = new(_artifactHistoryPath, 2, n => n.Deserialize<Dictionary<string, List<TemplateArtifactHistoryEntry>>>(Json) ?? [], new Dictionary<int, Func<Dictionary<string, List<TemplateArtifactHistoryEntry>>, Dictionary<string, List<TemplateArtifactHistoryEntry>>>> { [1] = value => value });
        _authorizations = new(_authorizationPath, 1, n => n.Deserialize<Dictionary<string, TemplateReviewAuthorization>>(Json) ?? []);
        _catalogCache = new(_catalogCachePath, 1, n => n.Deserialize<Dictionary<string, TemplateMarketplaceCatalogV2>>(Json) ?? []);
        _reviewGrants = new TemplateMarketplaceReviewGrantStore(_root);
    }
    public async Task<IReadOnlyList<TemplateSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _sources.ReadAsync(cancellationToken).ConfigureAwait(false);
        // A newer client owns the on-disk format. This client exposes an empty, non-actionable
        // view instead of corrupting or downgrading it; all mutation paths reject the schema.
        if (result.Error == StoreErrorKind.NewerSchema) return [BuiltInSource];
        if (result.Error == StoreErrorKind.NotFound) return [BuiltInSource];
        if (!result.Succeeded || result.Value is null) throw new WslOperationFailedException("Marketplace state could not be read.", DistroNexusErrorCode.ValidationFailed, "ReadTemplateMarketplaceState");
        return [BuiltInSource, .. result.Value.Value];
    }
    public async Task<TemplateSource> AddSourceAsync(string url, TemplateSourceKind kind, bool explicitlyAcceptedNonHttps, CancellationToken cancellationToken = default)
    {
        if (kind == TemplateSourceKind.BuiltIn) throw new WslOperationFailedException("Built-in template sources are application-owned.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource");
        var normalized = NormalizeSourceUrl(url, kind, explicitlyAcceptedNonHttps); await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { if (kind == TemplateSourceKind.BuiltIn) throw new WslOperationFailedException("Built-in template sources are application-owned.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource"); var sources = (await ReadStoreAsync(_sources, cancellationToken).ConfigureAwait(false)).ToList(); if (sources.Any(x => string.Equals(x.Url, normalized, StringComparison.Ordinal))) throw new WslOperationFailedException("Template source already exists.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource"); var source = new TemplateSource(Guid.NewGuid().ToString("N"), normalized, kind); await WriteStoreAsync(_sources, sources.Append(source).ToList(), cancellationToken).ConfigureAwait(false); return source; } finally { _gate.Release(); }
    }
    public async Task SetSourceEnabledAsync(string sourceId, bool enabled, CancellationToken cancellationToken = default)
    {
        if (string.Equals(sourceId, BuiltInSource.Id, StringComparison.Ordinal)) throw new WslOperationFailedException("Built-in template sources cannot be disabled.", DistroNexusErrorCode.ValidationFailed, "SetTemplateSourceEnabled");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sources = (await ReadStoreAsync(_sources, cancellationToken).ConfigureAwait(false)).ToList();
            var source = sources.SingleOrDefault(x => x.Id == sourceId);
            if (source is null) throw new WslOperationFailedException("Template source was not found.", DistroNexusErrorCode.TemplateNotFound, "SetTemplateSourceEnabled");
            if (source.Kind == TemplateSourceKind.BuiltIn) throw new WslOperationFailedException("Built-in template sources cannot be disabled.", DistroNexusErrorCode.ValidationFailed, "SetTemplateSourceEnabled");
            await WriteStoreAsync(_sources, sources.Select(x => x.Id == sourceId ? x with { IsEnabled = enabled } : x).ToList(), cancellationToken).ConfigureAwait(false);
            if (!enabled)
            {
                var normalized = NormalizeSourceUrl(source.Url, source.Kind, true);
                var authorizations = await ReadStoreAsync(_authorizations, cancellationToken).ConfigureAwait(false);
                foreach (var key in authorizations.Where(x => string.Equals(NormalizeSourceUrl(x.Value.SourceUrl, TemplateSourceKind.Remote, true), normalized, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToArray()) authorizations.Remove(key);
                await WriteStoreAsync(_authorizations, authorizations, cancellationToken).ConfigureAwait(false);
                await _reviewGrants.RevokeSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
                var trust = await ReadStoreAsync(_trust, cancellationToken).ConfigureAwait(false);
                foreach (var key in trust.Keys.Where(x => x.StartsWith(normalized.ToLowerInvariant() + "|", StringComparison.Ordinal)).ToArray()) trust.Remove(key);
                await WriteStoreAsync(_trust, trust, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { _gate.Release(); }
    }
    public async Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(sourceId, BuiltInSource.Id, StringComparison.Ordinal)) throw new WslOperationFailedException("Built-in template sources cannot be removed.", DistroNexusErrorCode.ValidationFailed, "RemoveTemplateSource");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sources = (await ReadStoreAsync(_sources, cancellationToken).ConfigureAwait(false)).ToList();
            var source = sources.SingleOrDefault(x => x.Id == sourceId);
            if (source is null) return;
            if (source.Kind == TemplateSourceKind.BuiltIn) throw new WslOperationFailedException("Built-in template sources cannot be removed.", DistroNexusErrorCode.ValidationFailed, "RemoveTemplateSource");
            await WriteStoreAsync(_sources, sources.Where(x => x.Id != sourceId).ToList(), cancellationToken).ConfigureAwait(false);
            var normalized = NormalizeSourceUrl(source.Url, source.Kind, true);
            var authorizations = await ReadStoreAsync(_authorizations, cancellationToken).ConfigureAwait(false);
            foreach (var key in authorizations.Where(x => string.Equals(NormalizeSourceUrl(x.Value.SourceUrl, TemplateSourceKind.Remote, true), normalized, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToArray()) authorizations.Remove(key);
            await WriteStoreAsync(_authorizations, authorizations, cancellationToken).ConfigureAwait(false);
            await _reviewGrants.RevokeSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
            var trust = await ReadStoreAsync(_trust, cancellationToken).ConfigureAwait(false);
            foreach (var key in trust.Keys.Where(x => x.StartsWith(normalized.ToLowerInvariant() + "|", StringComparison.Ordinal)).ToArray()) trust.Remove(key);
            await WriteStoreAsync(_trust, trust, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async Task<IReadOnlyList<TemplateMarketplaceEntry>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<TemplateMarketplaceEntry>();
        foreach (var source in (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).Where(x => x.IsEnabled && x.Kind != TemplateSourceKind.BuiltIn))
        {
            try
            {
                var catalog = await FetchCatalogAsync(source.Id, cancellationToken).ConfigureAwait(false);
                foreach (var manifest in catalog.Templates)
                {
                    var reviewed = await ResolveEnabledAuthorizationAsync(source.Url, manifest.Id, manifest, cancellationToken).ConfigureAwait(false);
                    var artifact = reviewed?.Artifact;
                    var canExecute = artifact is not null;
                    entries.Add(new TemplateMarketplaceEntry(manifest, source, source.Kind == TemplateSourceKind.BuiltIn ? TemplateTrustState.BuiltIn : canExecute ? TemplateTrustState.Trusted : TemplateTrustState.Untrusted, artifact, canExecute, canExecute ? "Marketplace.Ready" : "Marketplace.ReviewRequired", ManifestDigest(manifest)));
                }
            }
            catch (Exception) { entries.Add(new TemplateMarketplaceEntry(new TemplateManifestV2 { Id = source.Id, Name = source.Url }, source, TemplateTrustState.Untrusted, null, false, "Marketplace.SourceUnavailable")); }
        }
        return entries;
    }
    public async Task<TemplateArtifact> DownloadArtifactAsync(string sourceId, TemplateManifestV2 manifest, CancellationToken cancellationToken = default)
    {
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == sourceId && x.IsEnabled && x.Kind != TemplateSourceKind.BuiltIn);
        if (source is null) throw new WslOperationFailedException("Template source is unavailable.", DistroNexusErrorCode.TemplateNotFound, "DownloadTemplateArtifact");
        // Re-fetch the catalog before following its artifact URL, preventing callers from pairing
        // a manifest with an arbitrary artifact URL or publisher identity.
        var current = (await FetchCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false)).Templates.SingleOrDefault(x => string.Equals(x.Id, manifest.Id, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version, manifest.Version, StringComparison.Ordinal));
        if (current is null) throw new WslOperationFailedException("Template manifest is no longer current for this source.", DistroNexusErrorCode.ValidationFailed, "DownloadTemplateArtifact");
        if (!string.Equals(current.Id, manifest.Id, StringComparison.Ordinal) || !string.Equals(current.ArtifactSha256, manifest.ArtifactSha256, StringComparison.OrdinalIgnoreCase) || !string.Equals(current.PublisherFingerprint, manifest.PublisherFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new WslOperationFailedException("Template manifest is no longer current for this source.", DistroNexusErrorCode.ValidationFailed, "DownloadTemplateArtifact");
        await using var stream = await OpenArtifactAsync(source, current.ArtifactUrl, cancellationToken).ConfigureAwait(false);
        var artifact = await StoreArtifactAsync(current, stream, cancellationToken).ConfigureAwait(false);
        await BindArtifactSourceAsync(current.Id, artifact.Sha256, source.Url, cancellationToken).ConfigureAwait(false);
        return artifact;
    }
    public async Task<TemplateArtifact> DownloadArtifactAsync(string sourceId, string templateId, string manifestDigest, CancellationToken cancellationToken = default)
    {
        var manifest = (await FetchCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false)).Templates.SingleOrDefault(x => string.Equals(x.Id, templateId, StringComparison.OrdinalIgnoreCase) && string.Equals(ManifestDigest(x), manifestDigest, StringComparison.Ordinal));
        if (manifest is null) throw new WslOperationFailedException("Exact catalog entry is unavailable.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "DownloadTemplateArtifact");
        return await DownloadArtifactAsync(sourceId, manifest, cancellationToken).ConfigureAwait(false);
    }
    public async Task<TemplateReviewGrant> CreateReviewGrantAsync(string sourceId, string sha256, CancellationToken cancellationToken = default)
    {
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == sourceId && x.IsEnabled);
        if (source is null || source.Kind == TemplateSourceKind.BuiltIn) throw new WslOperationFailedException("Template source is unavailable.", DistroNexusErrorCode.TemplateTrustRequired, "CreateTemplateReviewGrant");
        // The archive checksum identifies a previously downloaded immutable candidate.  Do not
        // infer its identity from the first catalog item: a catalog can contain many templates.
        var candidate = (await GetArtifactHistoryAsyncForChecksumAsync(sha256, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        if (candidate is null) throw new WslOperationFailedException("Reviewed candidate is unavailable.", DistroNexusErrorCode.TemplateNotFound, "CreateTemplateReviewGrant");
        var current = (await FetchCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false)).Templates.SingleOrDefault(x =>
            string.Equals(x.Id, candidate.Manifest.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ManifestDigest(x), ManifestDigest(candidate.Manifest), StringComparison.Ordinal));
        if (current is null || !string.Equals(current.ArtifactSha256, sha256, StringComparison.OrdinalIgnoreCase))
            throw new WslOperationFailedException("Candidate changed after review.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "CreateTemplateReviewGrant");
        await VerifyArtifactAsync(candidate.Manifest, candidate.Artifact, cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeSourceUrl(source.Url, source.Kind, true);
        if (!string.Equals(candidate.SourceUrl, normalized, StringComparison.OrdinalIgnoreCase) || !ManifestEquals(candidate.Manifest, current)) throw new WslOperationFailedException("Candidate provenance does not match the reviewed source.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "CreateTemplateReviewGrant");
        var diff = await ReviewScriptDiffAsync(current.Id, sha256, cancellationToken).ConfigureAwait(false);
        var canonicalManifest = Convert.ToBase64String(CanonicalizeFullManifest(current));
        var grant = new TemplateReviewGrant(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(), source.Id, normalized, current, candidate.Artifact, diff,
            DateTimeOffset.UtcNow.Add(ReviewGrantLifetime), ManifestDigest(current), canonicalManifest,
            ExecutableFilesDigest(current.ExecutableFiles), ScriptDiffDigest(diff));
        await _reviewGrants.IssueAsync(grant, cancellationToken).ConfigureAwait(false);
        return grant;
    }
    public async Task<TemplateArtifact> ApproveCandidateAsync(string reviewToken, CancellationToken cancellationToken = default)
    {
        TemplateReviewGrant grant;
        try { grant = await _reviewGrants.ConsumeAsync(reviewToken, cancellationToken).ConfigureAwait(false); }
        catch (InvalidOperationException ex) { throw new WslOperationFailedException(ex.Message, DistroNexusErrorCode.TemplateTrustRequired, "ApproveTemplateArtifact"); }
        if (!IsReviewGrantProvenanceValid(grant)) throw new WslOperationFailedException("Reviewed candidate provenance is invalid.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ApproveTemplateArtifact");
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == grant.SourceId && x.IsEnabled && string.Equals(NormalizeSourceUrl(x.Url, x.Kind, true), grant.NormalizedSourceUrl, StringComparison.OrdinalIgnoreCase));
        if (source is null) throw new WslOperationFailedException("Template source is unavailable.", DistroNexusErrorCode.TemplateTrustRequired, "ApproveTemplateArtifact");
        var current = (await FetchCatalogAsync(source.Id, cancellationToken).ConfigureAwait(false)).Templates.SingleOrDefault(x =>
            string.Equals(x.Id, grant.Manifest.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(ManifestDigest(x), grant.ManifestDigest, StringComparison.Ordinal));
        if (current is null) throw new WslOperationFailedException("Candidate changed after review.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ApproveTemplateArtifact");
        if (!ManifestEquals(current, grant.Manifest) || !string.Equals(ManifestDigest(current), grant.ManifestDigest, StringComparison.Ordinal)) throw new WslOperationFailedException("Candidate changed after review.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ApproveTemplateArtifact");
        await VerifyArtifactAsync(grant.Manifest, grant.Artifact, cancellationToken).ConfigureAwait(false);
        await SaveAuthorizationAsync(new TemplateReviewAuthorization(grant.NormalizedSourceUrl, grant.Manifest.PublisherFingerprint, grant.Manifest.PublisherPublicKey, grant.Manifest, grant.Artifact, DateTimeOffset.UtcNow, grant.ManifestDigest), cancellationToken).ConfigureAwait(false);
        return grant.Artifact;
    }
    public async Task<TemplateManifestV2> FetchManifestAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var catalog = await FetchCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false);
        return catalog.Templates.FirstOrDefault() ?? throw new WslOperationFailedException("Template catalog contains no manifests.", DistroNexusErrorCode.TemplateManifestInvalid, "FetchTemplateManifest");
    }
    public async Task<TemplateMarketplaceCatalogV2> FetchCatalogAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var sources = await GetSourcesAsync(cancellationToken).ConfigureAwait(false);
        var source = sources.SingleOrDefault(x => string.Equals(x.Id, sourceId, StringComparison.Ordinal));
        if (source is null || !source.IsEnabled) throw new WslOperationFailedException("Template source is unavailable.", DistroNexusErrorCode.TemplateNotFound, "FetchTemplateManifest");
        if (source.Kind == TemplateSourceKind.BuiltIn) throw new WslOperationFailedException("Built-in templates are discovered from the application catalog.", DistroNexusErrorCode.ValidationFailed, "FetchTemplateManifest");
        try
        {
            await using var stream = await OpenManifestAsync(source, cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(new LimitedReadStream(stream, MaximumCatalogBytes));
            var raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            TemplateMarketplaceCatalogV2? catalog;
            try { catalog = JsonSerializer.Deserialize<TemplateMarketplaceCatalogV2>(raw, Json); } catch (JsonException) { catalog = null; }
            if (catalog is null || catalog.Templates.Count == 0) catalog = new TemplateMarketplaceCatalogV2 { Templates = [ValidateManifest(raw, source.Kind == TemplateSourceKind.Remote)] };
            if (catalog.SchemaVersion != 2 || catalog.Templates.Count == 0 || catalog.Templates.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != catalog.Templates.Count || catalog.Templates.Any(x => !string.Equals(x.PublisherFingerprint, catalog.PublisherFingerprint, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(catalog.PublisherFingerprint))) throw new WslOperationFailedException("Template catalog membership or metadata is invalid.", DistroNexusErrorCode.TemplateManifestInvalid, "FetchTemplateCatalog");
            foreach (var manifest in catalog.Templates) ValidateManifest(JsonSerializer.Serialize(manifest, Json), source.Kind == TemplateSourceKind.Remote);
            if (!string.IsNullOrWhiteSpace(catalog.CatalogSha256) && !string.Equals(catalog.CatalogSha256, CatalogDigest(catalog), StringComparison.OrdinalIgnoreCase)) throw new WslOperationFailedException("Template catalog checksum is invalid.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "FetchTemplateCatalog");
            if (!string.IsNullOrWhiteSpace(catalog.CatalogSignature)) VerifyCatalogSignature(catalog);
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { var replacement = source with { LastFetchedAt = DateTimeOffset.UtcNow, PublisherFingerprint = catalog.Templates[0].PublisherFingerprint }; await WriteStoreAsync(_sources, sources.Select(x => x.Id == sourceId ? replacement : x).ToList(), cancellationToken).ConfigureAwait(false); var cache = await ReadStoreAsync(_catalogCache, cancellationToken).ConfigureAwait(false); cache[source.Id] = catalog; await WriteStoreAsync(_catalogCache, cache, cancellationToken).ConfigureAwait(false); }
            finally { _gate.Release(); }
            return catalog;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var cache = await ReadStoreReadOnlyAsync(_catalogCache, cancellationToken).ConfigureAwait(false);
            if (cache.TryGetValue(sourceId, out var cached)) return cached;
            throw;
        }
    }
    public async Task<TemplateMarketplaceStatus> GetStatusAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == sourceId);
        if (source is null || !source.IsEnabled) return new(null, TemplateSignatureStatus.NotPresent, TemplateTrustState.Untrusted, false, false, "Source unavailable");
        try
        {
            var manifest = await FetchManifestAsync(sourceId, cancellationToken).ConfigureAwait(false);
            var authorization = await ResolveEnabledAuthorizationAsync(source.Url, manifest.Id, manifest, cancellationToken).ConfigureAwait(false);
            var approved = authorization is not null && ManifestEquals(authorization.Manifest, manifest) && string.Equals(authorization.ManifestDigest, ManifestDigest(manifest), StringComparison.Ordinal);
            var trust = await GetTrustAsync(source.Url, manifest.PublisherFingerprint, cancellationToken).ConfigureAwait(false);
            return new(manifest, string.IsNullOrWhiteSpace(manifest.PublisherSignature) ? TemplateSignatureStatus.NotPresent : TemplateSignatureStatus.Verified, trust, approved, approved, approved ? "Reviewed candidate is executable" : "Explicit review is required");
        }
        catch (WslOperationFailedException ex) when (ex.Code == DistroNexusErrorCode.TemplateArtifactIntegrityFailed)
        { return new(null, TemplateSignatureStatus.Invalid, TemplateTrustState.Untrusted, false, false, "Manifest signature or integrity is invalid"); }
    }
    public async Task<TemplateMarketplaceStatus> GetStatusAsync(string sourceId, string templateId, string manifestDigest, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId) || string.IsNullOrWhiteSpace(manifestDigest))
            throw new WslOperationFailedException("Exact catalog entry identity is required.", DistroNexusErrorCode.ValidationFailed, "GetTemplateMarketplaceStatus");
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.Id == sourceId);
        if (source is null || !source.IsEnabled) return new(null, TemplateSignatureStatus.NotPresent, TemplateTrustState.Untrusted, false, false, "Source unavailable");
        try
        {
            var manifest = (await FetchCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false)).Templates.SingleOrDefault(x =>
                string.Equals(x.Id, templateId, StringComparison.OrdinalIgnoreCase) && string.Equals(ManifestDigest(x), manifestDigest, StringComparison.Ordinal));
            if (manifest is null) return new(null, TemplateSignatureStatus.NotPresent, TemplateTrustState.Untrusted, false, false, "Exact catalog entry is unavailable");
            var authorization = await ResolveEnabledAuthorizationAsync(source.Url, manifest.Id, manifest, cancellationToken).ConfigureAwait(false);
            var approved = authorization is not null && string.Equals(authorization.Artifact.Sha256, manifest.ArtifactSha256, StringComparison.OrdinalIgnoreCase);
            var trust = await GetTrustAsync(source.Url, manifest.PublisherFingerprint, cancellationToken).ConfigureAwait(false);
            return new(manifest, string.IsNullOrWhiteSpace(manifest.PublisherSignature) ? TemplateSignatureStatus.NotPresent : TemplateSignatureStatus.Verified, trust, approved, approved, approved ? "Reviewed candidate is executable" : "Explicit review is required");
        }
        catch (WslOperationFailedException ex) when (ex.Code == DistroNexusErrorCode.TemplateArtifactIntegrityFailed)
        { return new(null, TemplateSignatureStatus.Invalid, TemplateTrustState.Untrusted, false, false, "Manifest signature or integrity is invalid"); }
    }
    private async Task SetTrustAsync(string sourceUrl, string publisherFingerprint, TemplateTrustState state, CancellationToken cancellationToken = default)
    {
        if (state is TemplateTrustState.BuiltIn or TemplateTrustState.Untrusted) throw new WslOperationFailedException("Only explicit trusted, review-required, or revoked decisions may be persisted.", DistroNexusErrorCode.ValidationFailed, "SetTemplateTrust");
        var key = TrustKey(sourceUrl, publisherFingerprint); await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { var trust = await ReadStoreAsync(_trust, cancellationToken).ConfigureAwait(false); trust[key] = state; await WriteStoreAsync(_trust, trust, cancellationToken).ConfigureAwait(false); } finally { _gate.Release(); }
    }
    private async Task<TemplateTrustState> GetTrustAsync(string sourceUrl, string publisherFingerprint, CancellationToken cancellationToken = default) { var trust = await ReadStoreReadOnlyAsync(_trust, cancellationToken).ConfigureAwait(false); return trust.TryGetValue(TrustKey(sourceUrl, publisherFingerprint), out var state) ? state : TemplateTrustState.Untrusted; }
    private async Task EnsureRemoteTemplateTrustedAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default)
    {
        if (manifest.SchemaVersion != 2) throw new WslOperationFailedException("Remote template manifest version is not executable.", DistroNexusErrorCode.TemplateManifestInvalid, "AuthorizeRemoteTemplate");
        var state = await GetTrustAsync(sourceUrl, manifest.PublisherFingerprint, cancellationToken).ConfigureAwait(false);
        if (state != TemplateTrustState.Trusted) throw new WslOperationFailedException("Remote template requires an explicit source and publisher trust decision.", DistroNexusErrorCode.TemplateTrustRequired, "AuthorizeRemoteTemplate");
    }
    public async Task<TemplateManifestV2?> GetReviewedManifestForExecutionAsync(string sourceUrl, string templateId, CancellationToken cancellationToken = default)
    {
        var knownGood = await GetKnownGoodArtifactAsync(templateId, cancellationToken).ConfigureAwait(false);
        if (knownGood is null) return null;
        var normalized = NormalizeSourceUrl(sourceUrl, TemplateSourceKind.Remote, true);
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.IsEnabled && string.Equals(NormalizeSourceUrl(x.Url, x.Kind, true), normalized, StringComparison.OrdinalIgnoreCase));
        if (source is null) return null;
        var authorizations = await ReadStoreReadOnlyAsync(_authorizations, cancellationToken).ConfigureAwait(false);
        var authorization = authorizations.Values.SingleOrDefault(x =>
            string.Equals(x.SourceUrl, normalized, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Manifest.Id, templateId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Artifact.Sha256, knownGood.Sha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ManifestDigest, ManifestDigest(x.Manifest), StringComparison.Ordinal));
        return authorization?.Manifest;
    }
    public async Task<TemplateManifestV2?> GetAuthorizedManifestForExecutionAsync(string sourceUrl, string templateId, string manifestDigest, string artifactSha256, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestDigest) || !IsSha256(artifactSha256)) return null;
        var normalized = NormalizeSourceUrl(sourceUrl, TemplateSourceKind.Remote, true);
        var source = (await GetSourcesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => x.IsEnabled && string.Equals(NormalizeSourceUrl(x.Url, x.Kind, true), normalized, StringComparison.OrdinalIgnoreCase));
        if (source is null) return null;
        var authorizations = await ReadStoreReadOnlyAsync(_authorizations, cancellationToken).ConfigureAwait(false);
        var authorization = authorizations.Values.SingleOrDefault(x => string.Equals(x.SourceUrl, normalized, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Manifest.Id, templateId, StringComparison.OrdinalIgnoreCase) && string.Equals(x.ManifestDigest, manifestDigest, StringComparison.Ordinal) && string.Equals(x.Artifact.Sha256, artifactSha256, StringComparison.OrdinalIgnoreCase));
        return authorization is not null && string.Equals(authorization.ManifestDigest, ManifestDigest(authorization.Manifest), StringComparison.Ordinal) ? authorization.Manifest : null;
    }

    public async Task CompleteSuccessfulExecutionAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default)
    {
        var authorization = await ResolveEnabledAuthorizationAsync(sourceUrl, manifest.Id, manifest, cancellationToken).ConfigureAwait(false)
            ?? throw new WslOperationFailedException("The marketplace candidate was not explicitly reviewed.", DistroNexusErrorCode.TemplateTrustRequired, "PromoteTemplateArtifact");
        if (!ManifestEquals(authorization.Manifest, manifest) || !string.Equals(ManifestDigest(manifest), authorization.ManifestDigest, StringComparison.Ordinal)) throw new WslOperationFailedException("The reviewed candidate changed before execution completed.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "PromoteTemplateArtifact");
        await VerifyArtifactAsync(authorization.Manifest, authorization.Artifact, cancellationToken).ConfigureAwait(false);
        await MarkKnownGoodAsync(manifest.Id, authorization.Artifact, cancellationToken).ConfigureAwait(false);
    }
    public async Task VerifyKnownGoodForExecutionAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default)
    {
        var authorization = await ResolveEnabledAuthorizationAsync(sourceUrl, manifest.Id, manifest, cancellationToken).ConfigureAwait(false)
            ?? throw new WslOperationFailedException("No exact reviewed marketplace candidate is available.", DistroNexusErrorCode.TemplateTrustRequired, "VerifyTemplateExecution");
        if (!ManifestEquals(authorization.Manifest, manifest) || !string.Equals(ManifestDigest(manifest), authorization.ManifestDigest, StringComparison.Ordinal)) throw new WslOperationFailedException("Reviewed marketplace candidate does not match the execution manifest.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        await VerifyArtifactAsync(authorization.Manifest, authorization.Artifact, cancellationToken).ConfigureAwait(false);
    }
    public async Task<TemplateArtifact> GetVerifiedArtifactForExecutionAsync(string sourceUrl, TemplateManifestV2 manifest, CancellationToken cancellationToken = default)
    {
        var authorization = await ResolveEnabledAuthorizationAsync(sourceUrl, manifest.Id, manifest, cancellationToken).ConfigureAwait(false) ?? throw new WslOperationFailedException("No exact reviewed marketplace candidate is available.", DistroNexusErrorCode.TemplateTrustRequired, "GetTemplateExecutionArtifact");
        if (!ManifestEquals(authorization.Manifest, manifest) || !string.Equals(authorization.ManifestDigest, ManifestDigest(manifest), StringComparison.Ordinal)) throw new WslOperationFailedException("Reviewed marketplace candidate does not match the execution manifest.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "GetTemplateExecutionArtifact");
        await VerifyArtifactAsync(authorization.Manifest, authorization.Artifact, cancellationToken).ConfigureAwait(false);
        return authorization.Artifact;
    }
    public TemplateManifestV2 ValidateManifest(string manifestJson, bool isRemote)
    {
        TemplateManifestV2? manifest; try { manifest = JsonSerializer.Deserialize<TemplateManifestV2>(manifestJson, Json); } catch (JsonException ex) { throw new WslOperationFailedException("Template manifest is invalid JSON.", ex, DistroNexusErrorCode.TemplateManifestInvalid, "ValidateTemplateManifest"); }
        if (manifest is null || manifest.SchemaVersion != 2 || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.ArtifactUrl) || !IsSha256(manifest.ArtifactSha256) || (isRemote && string.IsNullOrWhiteSpace(manifest.PublisherFingerprint))) throw new WslOperationFailedException("Template manifest does not meet schema version 2 requirements.", DistroNexusErrorCode.TemplateManifestInvalid, "ValidateTemplateManifest");
        if (manifest.Capabilities.Distinct().Count() != manifest.Capabilities.Count || manifest.ScriptHashes.Any(x => !IsSha256(x)) || manifest.ExecutableFiles.Any(x => !IsExecutablePath(x.Path) || !IsSha256(x.Sha256)) || manifest.ExecutableFiles.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.ExecutableFiles.Count) throw new WslOperationFailedException("Template manifest has invalid executable file declarations.", DistroNexusErrorCode.TemplateManifestInvalid, "ValidateTemplateManifest");
        if (isRemote && (!Uri.TryCreate(manifest.ArtifactUrl, UriKind.Absolute, out var artifactUri) || artifactUri.Scheme != Uri.UriSchemeHttps)) throw new WslOperationFailedException("Remote template artifacts must use HTTPS.", DistroNexusErrorCode.TemplateManifestInvalid, "ValidateTemplateManifest");
        if (!string.IsNullOrWhiteSpace(manifest.PublisherSignature)) VerifyManifestSignature(manifestJson, manifest);
        return manifest;
    }
    public byte[] CanonicalizeManifestForSignature(TemplateManifestV2 manifest) => CanonicalizeWithoutSignature(JsonSerializer.Serialize(manifest, Json));
    public string GetManifestDigest(TemplateManifestV2 manifest) => ManifestDigest(manifest);
    public void VerifyManifestSignature(TemplateManifestV2 manifest) => VerifyManifestSignature(JsonSerializer.Serialize(manifest, Json), manifest);
    public async Task<TemplateArtifact> StoreArtifactAsync(TemplateManifestV2 manifest, Stream archive, CancellationToken cancellationToken = default)
    {
        if (manifest.SchemaVersion != 2 || !IsSha256(manifest.ArtifactSha256)) throw new WslOperationFailedException("Artifact manifest is invalid.", DistroNexusErrorCode.TemplateManifestInvalid, "StoreTemplateArtifact"); Directory.CreateDirectory(_root); var temp = Path.Combine(_root, $"artifact-{Guid.NewGuid():N}.zip");
        try { await using (var output = File.Create(temp)) { await CopyArchiveBoundedAsync(archive, output, cancellationToken).ConfigureAwait(false); } string hash; await using (var input = File.OpenRead(temp)) { hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false)).ToLowerInvariant(); } if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), Convert.FromHexString(manifest.ArtifactSha256.ToLowerInvariant()))) throw new WslOperationFailedException("Template artifact checksum did not match its manifest.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "StoreTemplateArtifact"); var destination = Path.Combine(_root, "template-artifacts", hash); if (!Directory.Exists(destination)) { Directory.CreateDirectory(destination); try { File.Move(temp, Path.Combine(destination, "artifact.zip")); SafeExtract(Path.Combine(destination, "artifact.zip"), destination); } catch { try { Directory.Delete(destination, true); } catch { } throw; } } var artifact = new TemplateArtifact(hash, destination, DateTimeOffset.UtcNow, manifest.Id, manifest.Version); await RecordVerifiedArtifactAsync(manifest, artifact, cancellationToken).ConfigureAwait(false); return artifact; } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Task<TemplateUpdateReview> ReviewUpdateAsync(TemplateManifestV2 previous, TemplateManifestV2 candidate, CancellationToken cancellationToken = default) { var added = candidate.Capabilities.Except(previous.Capabilities).Order().ToArray(); var scriptsChanged = !previous.ScriptHashes.Order().SequenceEqual(candidate.ScriptHashes.Order(), StringComparer.Ordinal); var publisherChanged = !string.Equals(previous.PublisherFingerprint, candidate.PublisherFingerprint, StringComparison.OrdinalIgnoreCase); return Task.FromResult(new TemplateUpdateReview(candidate.Id, previous.ArtifactSha256, candidate.ArtifactSha256, added, scriptsChanged, publisherChanged, scriptsChanged || publisherChanged || added.Length > 0)); }
    public async Task<TemplateUpdateReview> ReviewUpdateAsync(string sourceId, string templateId, string manifestDigest, CancellationToken cancellationToken = default)
    {
        var candidate = (await FetchCatalogAsync(sourceId, cancellationToken).ConfigureAwait(false)).Templates.SingleOrDefault(x => string.Equals(x.Id, templateId, StringComparison.OrdinalIgnoreCase) && string.Equals(ManifestDigest(x), manifestDigest, StringComparison.Ordinal))
            ?? throw new WslOperationFailedException("Exact catalog entry is unavailable.", DistroNexusErrorCode.TemplateNotFound, "ReviewTemplateUpdate");
        var previous = await GetKnownGoodManifestAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
        return previous is null ? new TemplateUpdateReview(candidate.Id, string.Empty, candidate.ArtifactSha256, candidate.Capabilities.Order().ToArray(), candidate.ScriptHashes.Count > 0 || candidate.ExecutableFiles.Count > 0, false, true) : await ReviewUpdateAsync(previous, candidate, cancellationToken).ConfigureAwait(false);
    }
    public async Task<TemplateScriptDiff> ReviewScriptDiffAsync(string templateId, string candidateSha256, CancellationToken cancellationToken = default)
    {
        var history = await GetArtifactHistoryAsync(templateId, cancellationToken).ConfigureAwait(false);
        var candidate = history.SingleOrDefault(x => string.Equals(x.Artifact.Sha256, candidateSha256, StringComparison.OrdinalIgnoreCase)) ?? throw new WslOperationFailedException("Reviewed candidate is unavailable.", DistroNexusErrorCode.TemplateNotFound, "ReviewTemplateScripts");
        var baseline = await GetKnownGoodArtifactAsync(templateId, cancellationToken).ConfigureAwait(false);
        var previous = baseline is null ? null : history.SingleOrDefault(x => string.Equals(x.Artifact.Sha256, baseline.Sha256, StringComparison.OrdinalIgnoreCase));
        var oldScripts = previous is null ? new Dictionary<string, string>() : ReadScriptMap(previous.Artifact.RootPath, previous.Manifest);
        var newScripts = ReadScriptMap(candidate.Artifact.RootPath, candidate.Manifest);
        var changed = newScripts.Keys.Intersect(oldScripts.Keys, StringComparer.Ordinal).Where(k => !string.Equals(newScripts[k], oldScripts[k], StringComparison.OrdinalIgnoreCase)).Order().ToArray();
        var oldText = previous is null ? new Dictionary<string, string>() : ReadScriptTextMap(previous.Artifact.RootPath, previous.Manifest);
        var newText = ReadScriptTextMap(candidate.Artifact.RootPath, candidate.Manifest);
        const int limit = 4096;
        var texts = changed.Take(32).Select(id => new TemplateScriptTextChange(id, NormalizeReviewText(oldText.GetValueOrDefault(id), limit, out var oldTruncated), NormalizeReviewText(newText.GetValueOrDefault(id), limit, out var newTruncated), oldTruncated || newTruncated)).ToArray();
        return new TemplateScriptDiff(newScripts.Keys.Except(oldScripts.Keys, StringComparer.Ordinal).Order().ToArray(), oldScripts.Keys.Except(newScripts.Keys, StringComparer.Ordinal).Order().ToArray(), changed, texts, changed.Length > texts.Length || texts.Any(x => x.IsTruncated));
    }
    public async Task<TemplateArtifact?> GetKnownGoodArtifactAsync(string templateId, CancellationToken cancellationToken = default) { var all = await ReadStoreReadOnlyAsync(_knownGood, cancellationToken).ConfigureAwait(false); return all.TryGetValue(templateId, out var artifact) && Directory.Exists(artifact.RootPath) ? artifact : null; }
    public async Task<TemplateManifestV2?> GetKnownGoodManifestAsync(string templateId, CancellationToken cancellationToken = default) { var knownGood = await GetKnownGoodArtifactAsync(templateId, cancellationToken).ConfigureAwait(false); if (knownGood is null) return null; return (await GetArtifactHistoryAsync(templateId, cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => string.Equals(x.Artifact.Sha256, knownGood.Sha256, StringComparison.OrdinalIgnoreCase))?.Manifest; }
    public async Task<IReadOnlyList<TemplateArtifactHistoryEntry>> GetArtifactHistoryAsync(string templateId, CancellationToken cancellationToken = default) { var all = await ReadStoreReadOnlyAsync(_history, cancellationToken).ConfigureAwait(false); return all.TryGetValue(templateId, out var history) ? history.Where(x => Directory.Exists(x.Artifact.RootPath)).OrderByDescending(x => x.RecordedAt).ToArray() : []; }
    private async Task<IReadOnlyList<TemplateArtifactHistoryEntry>> GetArtifactHistoryAsyncForChecksumAsync(string sha256, CancellationToken cancellationToken)
    {
        var all = await ReadStoreReadOnlyAsync(_history, cancellationToken).ConfigureAwait(false);
        return all.Values.SelectMany(x => x).Where(x => Directory.Exists(x.Artifact.RootPath) && string.Equals(x.Artifact.Sha256, sha256, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public async Task RollbackAsync(string templateId, string sha256, CancellationToken cancellationToken = default)
    {
        if (!IsSha256(sha256)) throw new WslOperationFailedException("Artifact checksum is invalid.", DistroNexusErrorCode.ValidationFailed, "RollbackTemplateArtifact");
        var item = (await GetArtifactHistoryAsync(templateId, cancellationToken).ConfigureAwait(false)).SingleOrDefault(x => string.Equals(x.Artifact.Sha256, sha256, StringComparison.OrdinalIgnoreCase));
        if (item is null || string.IsNullOrWhiteSpace(item.SourceUrl)) throw new WslOperationFailedException("Requested artifact has not been promoted through a reviewed source.", DistroNexusErrorCode.TemplateTrustRequired, "RollbackTemplateArtifact");
        var authorization = await ResolveEnabledAuthorizationAsync(item.SourceUrl, templateId, item.Manifest, cancellationToken).ConfigureAwait(false);
        if (authorization is null || !string.Equals(authorization.Artifact.Sha256, sha256, StringComparison.OrdinalIgnoreCase)) throw new WslOperationFailedException("Rollback requires an enabled exact source authorization or a fresh review.", DistroNexusErrorCode.TemplateTrustRequired, "RollbackTemplateArtifact");
        await MarkKnownGoodAsync(templateId, item.Artifact, cancellationToken).ConfigureAwait(false);
    }
    private async Task RecordVerifiedArtifactAsync(TemplateManifestV2 manifest, TemplateArtifact artifact, CancellationToken cancellationToken) { await _gate.WaitAsync(cancellationToken).ConfigureAwait(false); try { var all = await ReadStoreAsync(_history, cancellationToken).ConfigureAwait(false); var retained = await ReadStoreAsync(_knownGoodRetention, cancellationToken).ConfigureAwait(false); var history = all.TryGetValue(manifest.Id, out var value) ? value : []; if (!history.Any(x => string.Equals(x.Artifact.Sha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))) history.Add(new TemplateArtifactHistoryEntry(manifest, artifact, DateTimeOffset.UtcNow)); var protectedHashes = retained.TryGetValue(manifest.Id, out var hashes) ? hashes : []; all[manifest.Id] = history.OrderBy(x => protectedHashes.Contains(x.Artifact.Sha256, StringComparer.OrdinalIgnoreCase) ? 0 : 1).ThenByDescending(x => x.RecordedAt).Take(8 + protectedHashes.Count).ToList(); await WriteStoreAsync(_history, all, cancellationToken).ConfigureAwait(false); } finally { _gate.Release(); } }
    private async Task MarkKnownGoodAsync(string templateId, TemplateArtifact artifact, CancellationToken cancellationToken) { await _gate.WaitAsync(cancellationToken).ConfigureAwait(false); try { var all = await ReadStoreAsync(_knownGood, cancellationToken).ConfigureAwait(false); var retained = await ReadStoreAsync(_knownGoodRetention, cancellationToken).ConfigureAwait(false); var previous = all.TryGetValue(templateId, out var old) ? old : null; all[templateId] = artifact; retained[templateId] = new[] { artifact.Sha256, previous?.Sha256 }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!; await WriteStoreAsync(_knownGood, all, cancellationToken).ConfigureAwait(false); await WriteStoreAsync(_knownGoodRetention, retained, cancellationToken).ConfigureAwait(false); } finally { _gate.Release(); } }
    private static async Task VerifyArtifactAsync(TemplateManifestV2 manifest, TemplateArtifact artifact, CancellationToken cancellationToken)
    {
        var archive = Path.Combine(artifact.RootPath, "artifact.zip");
        if (!File.Exists(archive)) throw new WslOperationFailedException("Verified archive is missing.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        await using var input = File.OpenRead(archive);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(manifest.ArtifactSha256.ToLowerInvariant()))) throw new WslOperationFailedException("Cached template archive was modified.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        var templatePath = Path.Combine(artifact.RootPath, "template.json");
        if (!File.Exists(templatePath)) throw new WslOperationFailedException("Template artifact does not contain its reviewed definition.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        using var zip = ZipFile.OpenRead(archive);
        var archiveTemplate = zip.GetEntry("template.json") ?? throw new WslOperationFailedException("Template archive does not contain its reviewed definition.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        if (!await FileMatchesArchiveEntryAsync(templatePath, archiveTemplate, cancellationToken).ConfigureAwait(false)) throw new WslOperationFailedException("Extracted template definition was modified.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false));
        var declared = manifest.ExecutableFiles.ToDictionary(x => x.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.TryGetProperty("scripts", out var fileScripts) && fileScripts.ValueKind == JsonValueKind.Array)
            foreach (var script in fileScripts.EnumerateArray()) if (script.TryGetProperty("scriptPath", out var scriptPath) && scriptPath.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(scriptPath.GetString()))
            {
                var relative = scriptPath.GetString()!.Replace('\\', '/');
                if (!declared.TryGetValue(relative, out var expected) || !referencedPaths.Add(relative)) throw new WslOperationFailedException("Template references an undeclared executable file.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
                var entry = zip.GetEntry(relative) ?? throw new WslOperationFailedException("Declared executable file is absent from the reviewed archive.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
                var path = Path.GetFullPath(Path.Combine(artifact.RootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
                var root = Path.GetFullPath(artifact.RootPath) + Path.DirectorySeparatorChar;
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || !await FileMatchesArchiveEntryAsync(path, entry, cancellationToken).ConfigureAwait(false)) throw new WslOperationFailedException("Extracted executable file was modified.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
                await using var file = File.OpenRead(path);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
                if (!string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase)) throw new WslOperationFailedException("Executable file does not match the manifest declaration.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
                // Preserve the existing aggregate script hash contract while binding it to path declarations.
            }
        if (!referencedPaths.SetEquals(declared.Keys)) throw new WslOperationFailedException("Manifest executable declarations do not match template references.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Array)
            foreach (var script in scripts.EnumerateArray()) if (script.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                hashes.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.GetString() ?? string.Empty))).ToLowerInvariant());
        foreach (var file in Directory.EnumerateFiles(artifact.RootPath, "*.sh", SearchOption.AllDirectories))
        {
            await using var script = File.OpenRead(file);
            hashes.Add(Convert.ToHexString(await SHA256.HashDataAsync(script, cancellationToken).ConfigureAwait(false)).ToLowerInvariant());
        }
        foreach (var executable in manifest.ExecutableFiles) hashes.Add(executable.Sha256);
        if (hashes.Count != manifest.ScriptHashes.Count || !hashes.SetEquals(manifest.ScriptHashes))
            throw new WslOperationFailedException("Declared template script content no longer matches its reviewed hashes.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "VerifyTemplateExecution");
    }
    private static async Task CopyArchiveBoundedAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaximumArchiveBytes) throw new WslOperationFailedException("Template artifact exceeds the archive size limit.", DistroNexusErrorCode.TemplateArtifactUnsafe, "StoreTemplateArtifact");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
    private static async Task<bool> FileMatchesArchiveEntryAsync(string path, ZipArchiveEntry entry, CancellationToken ct)
    {
        await using var file = File.OpenRead(path); await using var archived = entry.Open();
        var left = new byte[81920]; var right = new byte[81920];
        while (true) { var a = await file.ReadAsync(left, ct).ConfigureAwait(false); var b = await archived.ReadAsync(right, ct).ConfigureAwait(false); if (a != b || !left.AsSpan(0, a).SequenceEqual(right.AsSpan(0, b))) return false; if (a == 0) return true; }
    }
    private static void SafeExtract(string archivePath, string root)
    {
        long total = 0; const long maximumEntryBytes = 64L * 1024 * 1024;
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (entry.FullName.IndexOf('\0') >= 0 || entry.FullName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entry.FullName) || unixType is 0xA000 or 0x6000 || (entry.ExternalAttributes & 0x400) != 0) throw new WslOperationFailedException("Template archive contains an unsafe path or link.", DistroNexusErrorCode.TemplateArtifactUnsafe, "ExtractTemplateArtifact");
            var path = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new WslOperationFailedException("Template archive escapes its cache root.", DistroNexusErrorCode.TemplateArtifactUnsafe, "ExtractTemplateArtifact");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var input = entry.Open(); using var output = File.Create(path);
            CopyBounded(input, output, ref total, maximumEntryBytes);
        }
    }
    private static void CopyBounded(Stream input, Stream output, ref long total, long maximumEntryBytes)
    {
        var buffer = new byte[81920]; long entryTotal = 0; int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            entryTotal += read; total += read;
            if (entryTotal > maximumEntryBytes || total > MaximumExpandedBytes) throw new WslOperationFailedException("Template archive exceeds the extracted size limit.", DistroNexusErrorCode.TemplateArtifactUnsafe, "ExtractTemplateArtifact");
            output.Write(buffer, 0, read);
        }
    }
    private static string NormalizeSourceUrl(string value, TemplateSourceKind kind, bool accepted) { if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) throw new WslOperationFailedException("Template source URL is invalid.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource"); if (kind == TemplateSourceKind.Remote && uri.Scheme != Uri.UriSchemeHttps && !accepted) throw new WslOperationFailedException("Remote template sources require HTTPS unless explicitly accepted for development.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource"); if (kind == TemplateSourceKind.UserLocal && uri.Scheme != Uri.UriSchemeFile) throw new WslOperationFailedException("User-local template sources must be file URLs.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource"); if (kind != TemplateSourceKind.Remote && !accepted) throw new WslOperationFailedException("Local template sources require explicit confirmation.", DistroNexusErrorCode.ValidationFailed, "AddTemplateSource"); return uri.AbsoluteUri.TrimEnd('/'); }
    private static string TrustKey(string url, string fingerprint) => NormalizeSourceUrl(url, TemplateSourceKind.Remote, true).ToLowerInvariant() + "|" + fingerprint.Trim().ToLowerInvariant();
    private static string CatalogDigest(TemplateMarketplaceCatalogV2 catalog)
    {
        var unsigned = catalog with { CatalogSha256 = null };
        return Convert.ToHexString(SHA256.HashData(CanonicalizeWithoutSignature(JsonSerializer.Serialize(unsigned, Json)))).ToLowerInvariant();
    }
    private static void VerifyCatalogSignature(TemplateMarketplaceCatalogV2 catalog)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(catalog.PublisherPublicKey) || string.IsNullOrWhiteSpace(catalog.PublisherFingerprint)) throw new FormatException();
            var key = Convert.FromBase64String(catalog.PublisherPublicKey); var signature = Convert.FromBase64String(catalog.CatalogSignature!);
            if (key.Length != Ed25519.PublicKeySize || signature.Length != Ed25519.SignatureSize || !string.Equals(Convert.ToHexString(SHA256.HashData(key)).ToLowerInvariant(), catalog.PublisherFingerprint, StringComparison.OrdinalIgnoreCase)) throw new FormatException();
            var canonical = CanonicalizeWithoutSignature(JsonSerializer.Serialize(catalog, Json));
            if (!Ed25519.Verify(signature, 0, key, 0, canonical, 0, canonical.Length)) throw new FormatException();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { throw new WslOperationFailedException("Template catalog signature is invalid.", ex, DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "FetchTemplateCatalog"); }
    }
    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static bool IsExecutablePath(string value) => !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && !value.Contains("..", StringComparison.Ordinal) && value.Replace('\\', '/').Split('/').All(x => x.Length > 0 && x != ".");
    private static void VerifyManifestSignature(string rawJson, TemplateManifestV2 manifest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(manifest.PublisherPublicKey)) throw new FormatException("Publisher public key is required.");
            var publicKey = Convert.FromBase64String(manifest.PublisherPublicKey);
            var signature = Convert.FromBase64String(manifest.PublisherSignature!);
            if (publicKey.Length != Ed25519.PublicKeySize || signature.Length != Ed25519.SignatureSize) throw new FormatException("Ed25519 key or signature length is invalid.");
            var fingerprint = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(fingerprint), Encoding.ASCII.GetBytes(manifest.PublisherFingerprint.ToLowerInvariant()))) throw new FormatException("Publisher fingerprint does not match the supplied public key.");
            var canonical = CanonicalizeWithoutSignature(rawJson);
            if (!Ed25519.Verify(signature, 0, publicKey, 0, canonical, 0, canonical.Length)) throw new FormatException("Detached signature did not verify.");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or JsonException)
        {
            throw new WslOperationFailedException("Template manifest signature is invalid.", ex, DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ValidateTemplateManifest");
        }
    }
    private static byte[] CanonicalizeWithoutSignature(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(document.RootElement, writer, true);
        return stream.ToArray();
    }
    private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer, bool excludeSignature = false)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().Where(p => !(excludeSignature && (string.Equals(p.Name, "publisherSignature", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, "catalogSignature", StringComparison.OrdinalIgnoreCase)))).OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(property.Value, writer); }
            writer.WriteEndObject(); return;
        }
        if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(item, writer); writer.WriteEndArray(); return; }
        value.WriteTo(writer);
    }
    private async Task BindArtifactSourceAsync(string templateId, string sha256, string sourceUrl, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = await ReadStoreAsync(_history, ct).ConfigureAwait(false);
            if (!all.TryGetValue(templateId, out var entries)) return;
            all[templateId] = entries.Select(x => string.Equals(x.Artifact.Sha256, sha256, StringComparison.OrdinalIgnoreCase)
                ? x with { SourceUrl = NormalizeSourceUrl(sourceUrl, TemplateSourceKind.Remote, true) } : x).ToList();
            await WriteStoreAsync(_history, all, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task SaveAuthorizationAsync(TemplateReviewAuthorization authorization, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var all = await ReadStoreAsync(_authorizations, ct).ConfigureAwait(false);
            all[AuthorizationKey(authorization.SourceUrl, authorization.Manifest.Id, authorization.ManifestDigest)] = authorization;
            await WriteStoreAsync(_authorizations, all, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<TemplateReviewAuthorization?> ResolveEnabledAuthorizationAsync(string sourceUrl, string templateId, TemplateManifestV2? expectedManifest, CancellationToken ct)
    {
        var normalized = NormalizeSourceUrl(sourceUrl, TemplateSourceKind.Remote, true);
        var source = (await GetSourcesAsync(ct).ConfigureAwait(false)).SingleOrDefault(x => x.IsEnabled && string.Equals(NormalizeSourceUrl(x.Url, x.Kind, true), normalized, StringComparison.OrdinalIgnoreCase));
        if (source is null) return null;
        var all = await ReadStoreReadOnlyAsync(_authorizations, ct).ConfigureAwait(false);
        var authorization = all.Values.FirstOrDefault(x => string.Equals(x.SourceUrl, normalized, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Manifest.Id, templateId, StringComparison.OrdinalIgnoreCase) && (expectedManifest is null || string.Equals(x.ManifestDigest, ManifestDigest(expectedManifest), StringComparison.Ordinal)));
        return authorization is not null && string.Equals(authorization.ManifestDigest, ManifestDigest(authorization.Manifest), StringComparison.Ordinal) ? authorization : null;
    }

    private static string AuthorizationKey(string sourceUrl, string templateId, string manifestDigest) => NormalizeSourceUrl(sourceUrl, TemplateSourceKind.Remote, true).ToLowerInvariant() + "|" + templateId.Trim().ToLowerInvariant() + "|" + manifestDigest;
    private static bool ManifestEquals(TemplateManifestV2 left, TemplateManifestV2 right) => string.Equals(ManifestDigest(left), ManifestDigest(right), StringComparison.Ordinal);
    private static bool IsReviewGrantProvenanceValid(TemplateReviewGrant grant)
    {
        if (string.IsNullOrWhiteSpace(grant.Artifact.RootPath) || !Directory.Exists(grant.Artifact.RootPath) || !File.Exists(Path.Combine(grant.Artifact.RootPath, "artifact.zip"))) return false;
        var canonical = Convert.ToBase64String(CanonicalizeFullManifest(grant.Manifest));
        return string.Equals(canonical, grant.CanonicalManifest, StringComparison.Ordinal) &&
               string.Equals(ManifestDigest(grant.Manifest), grant.ManifestDigest, StringComparison.Ordinal) &&
               string.Equals(ExecutableFilesDigest(grant.Manifest.ExecutableFiles), grant.ExecutableFilesDigest, StringComparison.Ordinal) &&
               string.Equals(ScriptDiffDigest(grant.ScriptDiff), grant.ScriptDiffDigest, StringComparison.Ordinal);
    }
    private static string ExecutableFilesDigest(IReadOnlyList<TemplateExecutableFile> files) => DigestCanonicalJson(files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ThenBy(file => file.Sha256, StringComparer.Ordinal).ToArray());
    private static string ScriptDiffDigest(TemplateScriptDiff diff) => DigestCanonicalJson(diff);
    private static string DigestCanonicalJson<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, Json));
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(document.RootElement, writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
    /// <summary>Canonical full-manifest representation for grants and durable authorization identity; unlike signature verification it includes the signature itself.</summary>
    private static string ManifestDigest(TemplateManifestV2 manifest) => Convert.ToHexString(SHA256.HashData(CanonicalizeFullManifest(manifest))).ToLowerInvariant();
    private static byte[] CanonicalizeFullManifest(TemplateManifestV2 manifest)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(manifest, Json));
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(document.RootElement, writer);
        return stream.ToArray();
    }
    private static Dictionary<string, string> ReadScriptMap(string root, TemplateManifestV2 manifest)
    {
        var path = Path.Combine(root, "template.json"); if (!File.Exists(path)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path)); var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Array)
            foreach (var script in scripts.EnumerateArray())
            {
                var id = script.TryGetProperty("id", out var key) ? key.GetString() : script.TryGetProperty("name", out var name) ? name.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id) && script.TryGetProperty("content", out var content)) result[id] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.GetString() ?? string.Empty))).ToLowerInvariant();
            }
        foreach (var executable in manifest.ExecutableFiles)
            if (IsExecutablePath(executable.Path)) result["file:" + executable.Path.Replace('\\', '/')] = executable.Sha256;
        return result;
    }
    private static Dictionary<string, string> ReadScriptTextMap(string root, TemplateManifestV2 manifest)
    {
        var path = Path.Combine(root, "template.json"); if (!File.Exists(path)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(path)); var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Array)
            foreach (var script in scripts.EnumerateArray())
            {
                var id = script.TryGetProperty("id", out var key) ? key.GetString() : script.TryGetProperty("name", out var name) ? name.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id) && script.TryGetProperty("content", out var content)) result[id] = content.GetString() ?? string.Empty;
            }
        foreach (var executable in manifest.ExecutableFiles)
        {
            var relative = executable.Path.Replace('\\', '/');
            if (!IsExecutablePath(relative)) continue;
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            var basePath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) continue;
            result["file:" + relative] = ReadBoundedText(full, 4097);
        }
        return result;
    }
    private static string ReadBoundedText(string path, int limit)
    {
        using var reader = new StreamReader(File.OpenRead(path), Encoding.UTF8, true, 4096, false);
        var buffer = new char[limit]; var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }
    private static string NormalizeReviewText(string? value, int limit, out bool truncated)
    {
        var normalized = (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        truncated = normalized.Length > limit;
        return truncated ? normalized[..limit] + "\n[truncated]" : normalized;
    }

    private async Task<Stream> OpenManifestAsync(TemplateSource source, CancellationToken ct)
    {
        if (source.Kind == TemplateSourceKind.UserLocal)
        {
            var path = source.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(source.Url).LocalPath : source.Url;
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumCatalogBytes) throw new WslOperationFailedException("Template catalog is unavailable or exceeds the size limit.", DistroNexusErrorCode.TemplateManifestInvalid, "FetchTemplateManifest");
            return File.OpenRead(path);
        }
        var response = await _httpClient.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) { response.Dispose(); throw new WslOperationFailedException("Template catalog could not be fetched.", DistroNexusErrorCode.TemplateManifestInvalid, "FetchTemplateManifest"); }
        if (response.Content.Headers.ContentLength is > MaximumCatalogBytes) { response.Dispose(); throw new WslOperationFailedException("Template catalog exceeds the size limit.", DistroNexusErrorCode.TemplateManifestInvalid, "FetchTemplateManifest"); }
        // The response owns its content, but the returned network stream remains safe for the
        // bounded reader and is disposed by the caller on completion.
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    private async Task<Stream> OpenArtifactAsync(TemplateSource source, string artifactUrl, CancellationToken ct)
    {
        if (source.Kind == TemplateSourceKind.UserLocal)
        {
            var uri = new Uri(artifactUrl, UriKind.Absolute);
            if (!uri.IsFile) throw new WslOperationFailedException("Local template artifacts must use file URLs.", DistroNexusErrorCode.TemplateManifestInvalid, "DownloadTemplateArtifact");
            var info = new FileInfo(uri.LocalPath);
            if (!info.Exists || info.Length > MaximumArchiveBytes) throw new WslOperationFailedException("Template artifact is unavailable or exceeds the size limit.", DistroNexusErrorCode.TemplateArtifactUnsafe, "DownloadTemplateArtifact");
            return File.OpenRead(info.FullName);
        }
        using var response = await _httpClient.GetAsync(artifactUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumArchiveBytes) throw new WslOperationFailedException("Template artifact exceeds the archive size limit.", DistroNexusErrorCode.TemplateArtifactUnsafe, "DownloadTemplateArtifact");
        await using var sourceStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var copy = new MemoryStream(); await CopyArchiveBoundedAsync(sourceStream, copy, ct).ConfigureAwait(false); copy.Position = 0; return copy;
    }

    private static async Task<T> ReadStoreAsync<T>(VersionedJsonStore<T> store, CancellationToken ct)
    {
        var result = await store.ReadAsync(ct).ConfigureAwait(false);
        if (result.Error == StoreErrorKind.NotFound) return Activator.CreateInstance<T>();
        if (!result.Succeeded || result.Value is null)
            throw new WslOperationFailedException(result.Error == StoreErrorKind.NewerSchema ? "Marketplace state uses a newer schema and is available read-only." : "Marketplace state could not be read.", DistroNexusErrorCode.ValidationFailed, "ReadTemplateMarketplaceState");
        return result.Value.Value;
    }

    private static async Task<T> ReadStoreReadOnlyAsync<T>(VersionedJsonStore<T> store, CancellationToken ct)
    {
        var result = await store.ReadAsync(ct).ConfigureAwait(false);
        if (result.Error is StoreErrorKind.NotFound or StoreErrorKind.NewerSchema) return Activator.CreateInstance<T>();
        if (!result.Succeeded || result.Value is null) throw new WslOperationFailedException("Marketplace state could not be read.", DistroNexusErrorCode.ValidationFailed, "ReadTemplateMarketplaceState");
        return result.Value.Value;
    }

    private static async Task WriteStoreAsync<T>(VersionedJsonStore<T> store, T value, CancellationToken ct)
    {
        var current = await store.ReadAsync(ct).ConfigureAwait(false);
        if (current.Error == StoreErrorKind.NewerSchema)
            throw new WslOperationFailedException("Marketplace state uses a newer schema and cannot be changed by this version.", DistroNexusErrorCode.ValidationFailed, "WriteTemplateMarketplaceState");
        var write = await store.WriteAsync(value, current.Value?.Revision ?? 0, ct).ConfigureAwait(false);
        if (!write.Succeeded) throw new WslOperationFailedException("Marketplace state could not be saved.", DistroNexusErrorCode.ValidationFailed, "WriteTemplateMarketplaceState");
    }

    private sealed class LimitedReadStream(Stream inner, long limit) : Stream
    {
        private long _read;
        public override bool CanRead => inner.CanRead; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => throw new NotSupportedException(); public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { var result = inner.Read(buffer, offset, count); Count(result); return result; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { var result = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); Count(result); return result; }
        private void Count(int value) { _read += value; if (_read > limit) throw new WslOperationFailedException("Template catalog exceeds the size limit.", DistroNexusErrorCode.TemplateManifestInvalid, "FetchTemplateManifest"); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
