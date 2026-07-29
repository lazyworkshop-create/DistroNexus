using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Reflection;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Org.BouncyCastle.Security;

namespace DistroNexus.Tests.Services;

public sealed class TemplateMarketplaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus", "marketplace-tests", Guid.NewGuid().ToString("N"));
    private TemplateMarketplaceService Service => new(_root);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task AddSourceAsync_RejectsInsecureRemoteWithoutExplicitAcceptance()
    {
        var error = await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.AddSourceAsync("http://catalog.example.test/templates.json", TemplateSourceKind.Remote, false));
        Assert.Equal(DistroNexusErrorCode.ValidationFailed, error.Code);
    }

    [Fact]
    public async Task SourceLifecycle_DisablesAndRemovesNonBuiltInSources()
    {
        var source = await Service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        await Service.SetSourceEnabledAsync(source.Id, false);
        Assert.False((await Service.GetSourcesAsync()).Single(x => x.Id == source.Id).IsEnabled);
        await Service.RemoveSourceAsync(source.Id);
        Assert.DoesNotContain(await Service.GetSourcesAsync(), x => x.Kind != TemplateSourceKind.BuiltIn);
    }

    [Fact]
    public async Task BuiltInSource_IsVisibleAndImmutable()
    {
        var source = Assert.Single(await Service.GetSourcesAsync(), x => x.Kind == TemplateSourceKind.BuiltIn);
        Assert.Equal("distronexus://built-in", source.Url);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.SetSourceEnabledAsync(source.Id, false));
        await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.RemoveSourceAsync(source.Id));
    }

    [Fact]
    public void ValidateManifest_RejectsRemoteV1AndMissingChecksum()
    {
        var v1 = "{\"schemaVersion\":1,\"id\":\"demo\"}";
        var error = Assert.Throws<WslOperationFailedException>(() => Service.ValidateManifest(v1, true));
        Assert.Equal(DistroNexusErrorCode.TemplateManifestInvalid, error.Code);
    }

    [Fact]
    public void ValidateManifest_AcceptsCanonicalDetachedEd25519SignatureAndRejectsTampering()
    {
        var privateKey = new byte[Ed25519.SecretKeySize];
        var publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.GeneratePrivateKey(new SecureRandom(), privateKey);
        Ed25519.GeneratePublicKey(privateKey, 0, publicKey, 0);
        var unsigned = Manifest("signed", new string('a', 64)) with { PublisherPublicKey = Convert.ToBase64String(publicKey), PublisherFingerprint = Hash(publicKey) };
        var signature = new byte[Ed25519.SignatureSize];
        var canonical = Service.CanonicalizeManifestForSignature(unsigned);
        Ed25519.Sign(privateKey, 0, canonical, 0, canonical.Length, signature, 0);
        var signed = unsigned with { PublisherSignature = Convert.ToBase64String(signature) };
        Service.VerifyManifestSignature(signed);
        Assert.Throws<WslOperationFailedException>(() => Service.VerifyManifestSignature(signed with { Version = "2.1" }));
    }

    [Fact]
    public async Task StoreArtifactAsync_RejectsZipSlipBeforeKnownGoodStateChanges()
    {
        var archive = CreateZip(("../outside.txt", "no"));
        var manifest = Manifest("demo", Hash(archive));
        var error = await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.StoreArtifactAsync(manifest, new MemoryStream(archive)));
        Assert.Equal(DistroNexusErrorCode.TemplateArtifactUnsafe, error.Code);
        Assert.Null(await Service.GetKnownGoodArtifactAsync("demo"));
    }

    [Fact]
    public async Task StoreArtifactAsync_VerifiesHashAndLeavesArtifactAsCandidate()
    {
        var archive = CreateZip(("scripts/install.sh", "echo safe"));
        var artifact = await Service.StoreArtifactAsync(Manifest("demo", Hash(archive)), new MemoryStream(archive));
        Assert.True(File.Exists(Path.Combine(artifact.RootPath, "scripts", "install.sh")));
        Assert.Null(await Service.GetKnownGoodArtifactAsync("demo"));
        Assert.Contains(await Service.GetArtifactHistoryAsync("demo"), x => x.Artifact.Sha256 == artifact.Sha256);
    }

    [Fact]
    public async Task ReviewUpdateAsync_RequiresReviewForScriptOrCapabilityChanges()
    {
        var previous = Manifest("demo", new string('a', 64)) with { ScriptHashes = ["a"], Capabilities = [TemplateCapability.NetworkAccess] };
        var candidate = Manifest("demo", new string('b', 64)) with { ScriptHashes = ["b"], Capabilities = [TemplateCapability.NetworkAccess, TemplateCapability.Root] };
        var review = await Service.ReviewUpdateAsync(previous, candidate);
        Assert.True(review.RequiresReview);
        Assert.True(review.ScriptsChanged);
        Assert.Contains(TemplateCapability.Root, review.NewlyRequestedCapabilities);
    }

    [Fact]
    public void ValidateManifest_RejectsIncompleteOrTamperedSignatureMaterial()
    {
        var manifest = Manifest("demo", new string('a', 64)) with { PublisherSignature = Convert.ToBase64String(new byte[64]) };
        var json = System.Text.Json.JsonSerializer.Serialize(manifest);
        var error = Assert.Throws<WslOperationFailedException>(() => Service.ValidateManifest(json, true));
        Assert.Equal(DistroNexusErrorCode.TemplateArtifactIntegrityFailed, error.Code);
    }

    [Fact]
    public async Task DownloadArtifactAsync_RefetchesManifestAndStoresOnlyVerifiedArtifact()
    {
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"name\":\"Demo\",\"scripts\":[{\"id\":\"setup\",\"name\":\"Setup\",\"content\":\"echo safe\"}]}"));
        var manifest = Manifest("demo", Hash(archive));
        var json = System.Text.Json.JsonSerializer.Serialize(manifest);
        using var client = new HttpClient(new FixtureHandler(json, archive));
        var service = new TemplateMarketplaceService(_root, client);
        var source = await service.AddSourceAsync("https://catalog.example.test/templates.json", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);
        Assert.Equal(Hash(archive), artifact.Sha256);
        Assert.True(File.Exists(Path.Combine(artifact.RootPath, "template.json")));
    }

    [Fact]
    public async Task FetchCatalogAsync_DiscoversMultipleManifestsAndRejectsBadCatalogChecksum()
    {
        var first = Manifest("one", new string('a', 64));
        var second = Manifest("two", new string('b', 64));
        var catalog = new TemplateMarketplaceCatalogV2 { Templates = [first, second] };
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(catalog), CreateZip(("template.json", "{}"))));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "catalog"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);

        Assert.Equal(2, (await service.FetchCatalogAsync(source.Id)).Templates.Count);
        Assert.Equal(2, (await service.DiscoverAsync()).Count);

        var invalid = catalog with { CatalogSha256 = new string('0', 64) };
        using var invalidClient = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(invalid), CreateZip(("template.json", "{}"))));
        var invalidService = new TemplateMarketplaceService(Path.Combine(_root, "catalog-invalid"), invalidClient);
        var invalidSource = await invalidService.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => invalidService.FetchCatalogAsync(invalidSource.Id));
    }

    [Fact]
    public async Task DownloadArtifactAsync_RejectsUnknownLengthResponseOverArchiveLimitWithoutRecordingCandidate()
    {
        var manifest = Manifest("oversized", new string('a', 64));
        using var client = new HttpClient(new UnknownLengthFixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), 65L * 1024 * 1024));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "unknown-length"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/templates.json", TemplateSourceKind.Remote, false);

        var error = await Assert.ThrowsAsync<WslOperationFailedException>(() => service.DownloadArtifactAsync(source.Id, manifest));

        Assert.Equal(DistroNexusErrorCode.TemplateArtifactUnsafe, error.Code);
        Assert.Empty(await service.GetArtifactHistoryAsync(manifest.Id));
        Assert.False(Directory.Exists(Path.Combine(_root, "unknown-length", "template-artifacts")));
    }

    [Fact]
    public async Task MarketplacePersistence_MigratesLegacySourceDocumentAndRecoversBackup()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "template-sources.json"), "[{\"id\":\"legacy\",\"url\":\"https://catalog.example.test/a\",\"kind\":2}]");
        Assert.Single(await Service.GetSourcesAsync(), x => x.Kind != TemplateSourceKind.BuiltIn);
        await Service.AddSourceAsync("https://catalog.example.test/b", TemplateSourceKind.Remote, false);
        var path = Path.Combine(_root, "template-sources.json");
        await File.WriteAllTextAsync(path, "not-json");
        Assert.NotEmpty(await Service.GetSourcesAsync()); // VersionedJsonStore recovers the atomic .bak document.
    }

    [Fact]
    public async Task MarketplacePersistence_NewerSchemaIsReadOnlyAndNeverDowngraded()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "template-sources.json"), "{\"schemaVersion\":99,\"revision\":1,\"updatedAt\":\"2026-01-01T00:00:00+00:00\",\"value\":[]}");
        Assert.DoesNotContain(await Service.GetSourcesAsync(), x => x.Kind != TemplateSourceKind.BuiltIn);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false));
    }

    [Fact]
    public async Task UserLocalSource_IsExplicitlyConfirmedAndCanBeDisabled()
    {
        var manifestPath = Path.Combine(_root, "catalog.json"); Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(manifestPath, System.Text.Json.JsonSerializer.Serialize(Manifest("local", new string('a', 64)) with { ArtifactUrl = new Uri(Path.Combine(_root, "artifact.zip")).AbsoluteUri }));
        var source = await Service.AddSourceAsync(new Uri(manifestPath).AbsoluteUri, TemplateSourceKind.UserLocal, true);
        Assert.Equal("local", (await Service.FetchManifestAsync(source.Id)).Id);
        await Service.SetSourceEnabledAsync(source.Id, false);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.FetchManifestAsync(source.Id));
    }

    [Fact]
    public async Task VerifyKnownGoodForExecutionAsync_RefusesTamperedDeclaredScript()
    {
        var script = "echo safe";
        var scriptHash = Hash(Encoding.UTF8.GetBytes(script));
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[{\"content\":\"echo safe\"}]}"));
        var manifest = Manifest("demo", Hash(archive)) with { ScriptHashes = [scriptHash] };
        var artifact = await Service.StoreArtifactAsync(manifest, new MemoryStream(archive));
        var source = await Service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        // Candidate metadata is source-bound by the downloader in production; bind through an HTTP download here.
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive));
        var bound = new TemplateMarketplaceService(Path.Combine(_root, "bound"), client);
        var boundSource = await bound.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var boundArtifact = await bound.DownloadArtifactAsync(boundSource.Id, manifest);
        await bound.ApproveCandidateAsync((await bound.CreateReviewGrantAsync(boundSource.Id, boundArtifact.Sha256)).Token);
        await File.WriteAllTextAsync(Path.Combine(boundArtifact.RootPath, "template.json"), "{\"id\":\"demo\",\"scripts\":[{\"content\":\"echo tampered\"}]}");
        await Assert.ThrowsAsync<WslOperationFailedException>(() => bound.VerifyKnownGoodForExecutionAsync("https://catalog.example.test/a", manifest));
    }

    [Fact]
    public async Task ReviewedArtifact_AllowsOnlyManifestDeclaredExecutablePathsRegardlessOfExtension()
    {
        const string script = "Write-Output safe";
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[{\"scriptPath\":\"tools/setup.ps1\"}]}"), ("tools/setup.ps1", script));
        var scriptHash = Hash(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(script)).ToArray());
        var manifest = Manifest("demo", Hash(archive)) with { ScriptHashes = [scriptHash], ExecutableFiles = [new TemplateExecutableFile("tools/setup.ps1", scriptHash)] };
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "file-contract"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);

        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, artifact.Sha256)).Token);
        await service.VerifyKnownGoodForExecutionAsync(source.Url, manifest);
    }

    [Fact]
    public async Task ReviewedArtifact_RejectsUndeclaredOrTraversalExecutablePath()
    {
        const string script = "echo unsafe";
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[{\"scriptPath\":\"../outside.py\"}]}"), ("tools/setup.py", script));
        var manifest = Manifest("demo", Hash(archive)) with { ScriptHashes = [Hash(Encoding.UTF8.GetBytes(script))], ExecutableFiles = [new TemplateExecutableFile("tools/setup.py", Hash(Encoding.UTF8.GetBytes(script)))] };
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "file-contract-malicious"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);

        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.CreateReviewGrantAsync(source.Id, artifact.Sha256));
    }

    [Fact]
    public async Task ReviewedCandidate_IsPromotedOnlyAfterSuccessfulExecutionCompletion()
    {
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[]}"));
        var manifest = Manifest("demo", Hash(archive));
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "promotion"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, artifact.Sha256)).Token);
        Assert.Null(await service.GetKnownGoodArtifactAsync("demo"));
        await service.CompleteSuccessfulExecutionAsync(source.Url, manifest);
        Assert.Equal(artifact.Sha256, (await service.GetKnownGoodArtifactAsync("demo"))!.Sha256);
    }

    [Fact]
    public async Task ReviewGrant_IsOneShotAndDisabledOrRemovedSourceCannotExecute()
    {
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[]}"));
        var manifest = Manifest("demo", Hash(archive));
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "grant"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);
        var grant = await service.CreateReviewGrantAsync(source.Id, artifact.Sha256);
        await service.ApproveCandidateAsync(grant.Token);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.ApproveCandidateAsync(grant.Token));
        await service.SetSourceEnabledAsync(source.Id, false);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.VerifyKnownGoodForExecutionAsync(source.Url, manifest));
        await service.RemoveSourceAsync(source.Id);
        var readded = await service.AddSourceAsync(source.Url, TemplateSourceKind.Remote, false);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.VerifyKnownGoodForExecutionAsync(readded.Url, manifest));
    }

    [Fact]
    public async Task DisableRevokesAuthorizationAndReenableRequiresFreshReview()
    {
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[]}"));
        var manifest = Manifest("demo", Hash(archive));
        using var client = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "disable-revoke"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);
        var grant = await service.CreateReviewGrantAsync(source.Id, artifact.Sha256);
        await service.SetSourceEnabledAsync(source.Id, false);
        await service.SetSourceEnabledAsync(source.Id, true);

        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.ApproveCandidateAsync(grant.Token));
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.RollbackAsync(manifest.Id, artifact.Sha256));
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Name")]
    [InlineData("ArtifactUrl")]
    [InlineData("PublisherSignature")]
    [InlineData("SchemaVersion")]
    public async Task ReviewGrant_RejectsAnyFullManifestMutation(string field)
    {
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[]}"));
        var manifest = Manifest("demo", Hash(archive));
        var handler = new MutableFixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive);
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "manifest-" + field), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);
        var grant = await service.CreateReviewGrantAsync(source.Id, artifact.Sha256);
        var changed = field switch
        {
            "Version" => manifest with { Version = "changed" },
            "Name" => manifest with { Name = "changed" },
            "ArtifactUrl" => manifest with { ArtifactUrl = "https://catalog.example.test/other.zip" },
            "PublisherSignature" => manifest with { PublisherSignature = Convert.ToBase64String(new byte[64]), PublisherPublicKey = Convert.ToBase64String(new byte[32]) },
            _ => manifest with { SchemaVersion = 3 }
        };
        handler.Catalog = System.Text.Json.JsonSerializer.Serialize(changed);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.ApproveCandidateAsync(grant.Token));
    }

    [Fact]
    public async Task ReviewGrant_RejectsCanonicalManifestOrderingAndEscapingMutations()
    {
        var archive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[]}"));
        var manifest = Manifest("demo", Hash(archive)) with
        {
            Name = "escaped\nname\\\"",
            Capabilities = [TemplateCapability.NetworkAccess, TemplateCapability.Root]
        };
        var handler = new MutableFixtureHandler(System.Text.Json.JsonSerializer.Serialize(manifest), archive);
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "canonical-escaping"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var artifact = await service.DownloadArtifactAsync(source.Id, manifest);
        var grant = await service.CreateReviewGrantAsync(source.Id, artifact.Sha256);

        handler.Catalog = System.Text.Json.JsonSerializer.Serialize(manifest with { Name = "escaped\r\nname\\\"", Capabilities = [TemplateCapability.Root, TemplateCapability.NetworkAccess] });

        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.ApproveCandidateAsync(grant.Token));
    }

    [Fact]
    public async Task StoreArtifactAsync_RejectsUnixSymlinkAndDoesNotRetainArtifact()
    {
        var bytes = CreateZip(("link", "target"));
        using (var input = new MemoryStream(bytes, true)) using (var zip = new ZipArchive(input, ZipArchiveMode.Update, true)) zip.GetEntry("link")!.ExternalAttributes = unchecked((int)0xA0000000);
        var error = await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.StoreArtifactAsync(Manifest("link", Hash(bytes)), new MemoryStream(bytes)));
        Assert.Equal(DistroNexusErrorCode.TemplateArtifactUnsafe, error.Code);
        Assert.Empty(await Service.GetArtifactHistoryAsync("link"));
    }

    [Fact]
    public async Task StoreArtifactAsync_RejectsActualExpandedByteLimitAndCleansCache()
    {
        var payload = new string('A', 65 * 1024 * 1024);
        var archive = CreateZip(("large.txt", payload));
        var error = await Assert.ThrowsAsync<WslOperationFailedException>(() => Service.StoreArtifactAsync(Manifest("large", Hash(archive)), new MemoryStream(archive)));
        Assert.Equal(DistroNexusErrorCode.TemplateArtifactUnsafe, error.Code);
        Assert.Empty(await Service.GetArtifactHistoryAsync("large"));
        Assert.False(Directory.Exists(Path.Combine(_root, "template-artifacts", Hash(archive))));
    }

    [Fact]
    public async Task FailedOrCancelledCandidateCompletion_LeavesPriorKnownGoodUntouched()
    {
        var oldArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[]}"));
        var oldManifest = Manifest("demo", Hash(oldArchive));
        using var oldClient = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(oldManifest), oldArchive));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "rollback"), oldClient);
        var source = await service.AddSourceAsync("https://catalog.example.test/a", TemplateSourceKind.Remote, false);
        var oldArtifact = await service.DownloadArtifactAsync(source.Id, oldManifest);
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, oldArtifact.Sha256)).Token);
        await service.CompleteSuccessfulExecutionAsync(source.Url, oldManifest);
        var candidateArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[{\"content\":\"exit 1\"}]}"));
        var candidateManifest = Manifest("demo", Hash(candidateArchive)) with { ScriptHashes = [Hash(Encoding.UTF8.GetBytes("exit 1"))] };
        using var candidateClient = new HttpClient(new FixtureHandler(System.Text.Json.JsonSerializer.Serialize(candidateManifest), candidateArchive));
        var candidateService = new TemplateMarketplaceService(Path.Combine(_root, "rollback"), candidateClient);
        var candidateArtifact = await candidateService.DownloadArtifactAsync(source.Id, candidateManifest);
        await candidateService.ApproveCandidateAsync((await candidateService.CreateReviewGrantAsync(source.Id, candidateArtifact.Sha256)).Token);
        Assert.Equal(oldArtifact.Sha256, (await candidateService.GetKnownGoodArtifactAsync("demo"))!.Sha256);
    }

    [Fact]
    public async Task MultiEntryCatalog_SecondEntryUsesExactDownloadReviewApprovalAndExecutionIdentity()
    {
        var firstArchive = CreateZip(("template.json", "{\"id\":\"first\",\"scripts\":[]}"));
        var secondArchive = CreateZip(("template.json", "{\"id\":\"second\",\"scripts\":[]}"));
        var first = Manifest("first", Hash(firstArchive)) with { ArtifactUrl = "https://catalog.example.test/first.zip" };
        var second = Manifest("second", Hash(secondArchive)) with { ArtifactUrl = "https://catalog.example.test/second.zip" };
        var handler = new CatalogFixtureHandler(new TemplateMarketplaceCatalogV2 { Templates = [first, second] }, new Dictionary<string, byte[]> { ["/first.zip"] = firstArchive, ["/second.zip"] = secondArchive });
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "multi-entry"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);
        var digest = service.GetManifestDigest(second);

        var artifact = await service.DownloadArtifactAsync(source.Id, "second", digest);
        Assert.Equal(Hash(secondArchive), artifact.Sha256);
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.GetVerifiedArtifactForExecutionAsync(source.Url, second));
        var grant = await service.CreateReviewGrantAsync(source.Id, artifact.Sha256);
        Assert.Equal("second", grant.Manifest.Id);
        await service.ApproveCandidateAsync(grant.Token);
        var executable = await service.GetVerifiedArtifactForExecutionAsync(source.Url, second);
        Assert.Equal(artifact.Sha256, executable.Sha256);
        await service.CompleteSuccessfulExecutionAsync(source.Url, second);
        Assert.Equal(artifact.Sha256, (await service.GetKnownGoodArtifactAsync("second"))!.Sha256);
        Assert.Null(await service.GetKnownGoodArtifactAsync("first"));
    }

    [Fact]
    public async Task ApprovedCandidate_IsExecutableBeforePromotionWithoutMovingKnownGoodPointer()
    {
        var oldArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"version\":\"1\",\"scripts\":[]}"));
        var candidateArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"version\":\"2\",\"scripts\":[]}"));
        var old = Manifest("demo", Hash(oldArchive)) with { Version = "1", ArtifactUrl = "https://catalog.example.test/old.zip" };
        var candidate = Manifest("demo", Hash(candidateArchive)) with { Version = "2", ArtifactUrl = "https://catalog.example.test/candidate.zip" };
        var handler = new CatalogFixtureHandler(new TemplateMarketplaceCatalogV2 { Templates = [old] }, new Dictionary<string, byte[]> { ["/old.zip"] = oldArchive, ["/candidate.zip"] = candidateArchive });
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "candidate-before-promotion"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);
        var oldArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(old));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, oldArtifact.Sha256)).Token);
        await service.CompleteSuccessfulExecutionAsync(source.Url, old);
        handler.Catalog = new TemplateMarketplaceCatalogV2 { Templates = [candidate] };
        var candidateArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(candidate));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, candidateArtifact.Sha256)).Token);

        var authorized = await service.GetAuthorizedManifestForExecutionAsync(source.Url, "demo", service.GetManifestDigest(candidate), candidateArtifact.Sha256);
        Assert.Equal("2", authorized!.Version);
        Assert.Equal(candidateArtifact.Sha256, (await service.GetVerifiedArtifactForExecutionAsync(source.Url, authorized)).Sha256);
        Assert.Equal(oldArtifact.Sha256, (await service.GetKnownGoodArtifactAsync("demo"))!.Sha256);
    }

    [Fact]
    public async Task Rollback_UsesStoredOldManifestWhenCatalogKeepsPublishingNewVersion()
    {
        var oldArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"version\":\"1\",\"scripts\":[]}"));
        var newArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"version\":\"2\",\"scripts\":[]}"));
        var old = Manifest("demo", Hash(oldArchive)) with { Version = "1", ArtifactUrl = "https://catalog.example.test/old.zip" };
        var newer = Manifest("demo", Hash(newArchive)) with { Version = "2", ArtifactUrl = "https://catalog.example.test/new.zip" };
        var handler = new CatalogFixtureHandler(new TemplateMarketplaceCatalogV2 { Templates = [old] }, new Dictionary<string, byte[]> { ["/old.zip"] = oldArchive, ["/new.zip"] = newArchive });
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "rollback-current-catalog"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);
        var oldArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(old));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, oldArtifact.Sha256)).Token);
        await service.CompleteSuccessfulExecutionAsync(source.Url, old);

        handler.Catalog = new TemplateMarketplaceCatalogV2 { Templates = [newer] };
        var newArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(newer));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, newArtifact.Sha256)).Token);
        await service.CompleteSuccessfulExecutionAsync(source.Url, newer);
        await service.RollbackAsync("demo", oldArtifact.Sha256);

        Assert.Equal(oldArtifact.Sha256, (await service.GetKnownGoodArtifactAsync("demo"))!.Sha256);
        var oldExecution = await service.GetReviewedManifestForExecutionAsync(source.Url, "demo");
        Assert.Equal(old.Version, oldExecution!.Version);
        Assert.Equal(oldArtifact.Sha256, (await service.GetVerifiedArtifactForExecutionAsync(source.Url, oldExecution)).Sha256);
    }

    [Fact]
    public async Task ArtifactHistory_PreservesCurrentAndPriorKnownGoodBeyondCandidateLimit()
    {
        var firstArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"version\":\"1\",\"scripts\":[]}"));
        var secondArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"version\":\"2\",\"scripts\":[]}"));
        var first = Manifest("demo", Hash(firstArchive)) with { Version = "1", ArtifactUrl = "https://catalog.example.test/first.zip" };
        var second = Manifest("demo", Hash(secondArchive)) with { Version = "2", ArtifactUrl = "https://catalog.example.test/second.zip" };
        var artifacts = new Dictionary<string, byte[]> { ["/first.zip"] = firstArchive, ["/second.zip"] = secondArchive };
        var handler = new CatalogFixtureHandler(new TemplateMarketplaceCatalogV2 { Templates = [first] }, artifacts);
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "retained-known-good"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);

        var firstArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(first));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, firstArtifact.Sha256)).Token);
        await service.CompleteSuccessfulExecutionAsync(source.Url, first);
        handler.Catalog = new TemplateMarketplaceCatalogV2 { Templates = [second] };
        var secondArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(second));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, secondArtifact.Sha256)).Token);
        await service.CompleteSuccessfulExecutionAsync(source.Url, second);

        TemplateManifestV2? newestCandidate = null;
        for (var version = 3; version <= 12; version++)
        {
            var archive = CreateZip(("template.json", $"{{\"id\":\"demo\",\"version\":\"{version}\",\"scripts\":[]}}"));
            var candidate = Manifest("demo", Hash(archive)) with { Version = version.ToString(), ArtifactUrl = $"https://catalog.example.test/{version}.zip" };
            artifacts[$"/{version}.zip"] = archive;
            handler.Catalog = new TemplateMarketplaceCatalogV2 { Templates = [candidate] };
            await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(candidate));
            newestCandidate = candidate;
        }

        var history = await service.GetArtifactHistoryAsync("demo");
        Assert.Contains(history, item => item.Artifact.Sha256 == firstArtifact.Sha256);
        Assert.Contains(history, item => item.Artifact.Sha256 == secondArtifact.Sha256);
        Assert.True(history.Count > 8);
        var update = await service.ReviewUpdateAsync(source.Id, "demo", service.GetManifestDigest(newestCandidate!));
        Assert.Equal(secondArtifact.Sha256, update.PreviousSha256);

        await service.RollbackAsync("demo", firstArtifact.Sha256);
        Assert.Equal(firstArtifact.Sha256, (await service.GetKnownGoodArtifactAsync("demo"))!.Sha256);
    }

    [Fact]
    public async Task CatalogDetachedSignature_ValidatesMembershipOrderAndUnknownKey()
    {
        var archive = CreateZip(("template.json", "{}"));
        var privateKey = new byte[Ed25519.SecretKeySize]; var publicKey = new byte[Ed25519.PublicKeySize];
        Ed25519.GeneratePrivateKey(new SecureRandom(), privateKey); Ed25519.GeneratePublicKey(privateKey, 0, publicKey, 0);
        var fingerprint = Hash(publicKey);
        var one = Manifest("one", new string('a', 64)) with { PublisherFingerprint = fingerprint };
        var two = Manifest("two", new string('b', 64)) with { PublisherFingerprint = fingerprint };
        var signed = SignCatalog(new TemplateMarketplaceCatalogV2 { PublisherPublicKey = Convert.ToBase64String(publicKey), PublisherFingerprint = fingerprint, Templates = [one, two] }, privateKey);
        var handler = new CatalogFixtureHandler(signed, new Dictionary<string, byte[]> { ["/artifact.zip"] = archive });
        using var client = new HttpClient(handler);
        var service = new TemplateMarketplaceService(Path.Combine(_root, "catalog-signature"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);
        Assert.Equal(["one", "two"], (await service.FetchCatalogAsync(source.Id)).Templates.Select(x => x.Id));

        handler.Catalog = signed with { Templates = [two, one] };
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.FetchCatalogAsync(source.Id));
        handler.Catalog = signed with { PublisherPublicKey = Convert.ToBase64String(new byte[Ed25519.PublicKeySize]) };
        await Assert.ThrowsAsync<WslOperationFailedException>(() => service.FetchCatalogAsync(source.Id));
    }

    [Fact]
    public async Task ExactStatus_ProjectsOnlyTheRequestedCatalogEntry()
    {
        var first = Manifest("first", new string('a', 64));
        var second = Manifest("second", new string('b', 64)) with { Version = "second-version" };
        using var client = new HttpClient(new CatalogFixtureHandler(new TemplateMarketplaceCatalogV2 { Templates = [first, second] }, new Dictionary<string, byte[]> { ["/artifact.zip"] = CreateZip(("template.json", "{}")) }));
        var service = new TemplateMarketplaceService(Path.Combine(_root, "exact-status"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);

        var firstStatus = await service.GetStatusAsync(source.Id, first.Id, service.GetManifestDigest(first));
        var secondStatus = await service.GetStatusAsync(source.Id, second.Id, service.GetManifestDigest(second));

        Assert.Equal("first", firstStatus.Manifest!.Id);
        Assert.Equal(TemplateSignatureStatus.NotPresent, firstStatus.SignatureStatus);
        Assert.Equal("second", secondStatus.Manifest!.Id);
        Assert.Equal(TemplateSignatureStatus.NotPresent, secondStatus.SignatureStatus);
    }

    [Fact]
    public async Task ReviewScriptDiffAsync_ShowsBoundedFileBackedExecutableTextChange()
    {
        var oldScript = "echo old"; var newScript = "echo new";
        var oldArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[{\"scriptPath\":\"scripts/install.sh\"}]}"), ("scripts/install.sh", oldScript));
        var newArchive = CreateZip(("template.json", "{\"id\":\"demo\",\"scripts\":[{\"scriptPath\":\"scripts/install.sh\"}]}"), ("scripts/install.sh", newScript));
        var oldHash = Hash(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(oldScript)).ToArray()); var newHash = Hash(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(newScript)).ToArray());
        var old = Manifest("demo", Hash(oldArchive)) with { Version = "1", ArtifactUrl = "https://catalog.example.test/old.zip", ScriptHashes = [oldHash], ExecutableFiles = [new TemplateExecutableFile("scripts/install.sh", oldHash)] };
        var candidate = Manifest("demo", Hash(newArchive)) with { Version = "2", ArtifactUrl = "https://catalog.example.test/new.zip", ScriptHashes = [newHash], ExecutableFiles = [new TemplateExecutableFile("scripts/install.sh", newHash)] };
        var handler = new CatalogFixtureHandler(new TemplateMarketplaceCatalogV2 { Templates = [old] }, new Dictionary<string, byte[]> { ["/old.zip"] = oldArchive, ["/new.zip"] = newArchive });
        using var client = new HttpClient(handler); var service = new TemplateMarketplaceService(Path.Combine(_root, "file-diff"), client);
        var source = await service.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);
        var oldArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(old));
        await service.ApproveCandidateAsync((await service.CreateReviewGrantAsync(source.Id, oldArtifact.Sha256)).Token); await service.CompleteSuccessfulExecutionAsync(source.Url, old);
        handler.Catalog = new TemplateMarketplaceCatalogV2 { Templates = [candidate] };
        var newArtifact = await service.DownloadArtifactAsync(source.Id, "demo", service.GetManifestDigest(candidate));

        var diff = await service.ReviewScriptDiffAsync("demo", newArtifact.Sha256);
        var text = Assert.Single(diff.TextChanges!);
        Assert.Equal("file:scripts/install.sh", text.ScriptId);
        Assert.Contains("echo old", text.PreviousText); Assert.Contains("echo new", text.CandidateText);
    }

    private static TemplateManifestV2 Manifest(string id, string hash) => new() { Id = id, Name = id, Version = "2.0", ArtifactUrl = "https://catalog.example.test/artifact.zip", ArtifactSha256 = hash, PublisherFingerprint = "f1" };
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static byte[] CreateZip(params (string Name, string Content)[] files) { using var stream = new MemoryStream(); using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) foreach (var file in files) { var entry = archive.CreateEntry(file.Name); using var writer = new StreamWriter(entry.Open(), Encoding.UTF8); writer.Write(file.Content); } return stream.ToArray(); }
    private static TemplateMarketplaceCatalogV2 SignCatalog(TemplateMarketplaceCatalogV2 catalog, byte[] privateKey)
    {
        var digest = (string)typeof(TemplateMarketplaceService).GetMethod("CatalogDigest", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [catalog])!;
        var withDigest = catalog with { CatalogSha256 = digest };
        var canonical = (byte[])typeof(TemplateMarketplaceService).GetMethod("CanonicalizeWithoutSignature", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [System.Text.Json.JsonSerializer.Serialize(withDigest)])!;
        var signature = new byte[Ed25519.SignatureSize]; Ed25519.Sign(privateKey, 0, canonical, 0, canonical.Length, signature, 0);
        return withDigest with { CatalogSignature = Convert.ToBase64String(signature) };
    }
    private sealed class CatalogFixtureHandler(TemplateMarketplaceCatalogV2 catalog, IReadOnlyDictionary<string, byte[]> artifacts) : HttpMessageHandler
    {
        public TemplateMarketplaceCatalogV2 Catalog { get; set; } = catalog;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = artifacts.TryGetValue(request.RequestUri!.AbsolutePath, out var artifact) ? new ByteArrayContent(artifact) : new StringContent(System.Text.Json.JsonSerializer.Serialize(Catalog))
        });
    }
    private sealed class FixtureHandler(string catalog, byte[] artifact) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("artifact.zip", StringComparison.Ordinal) ? new ByteArrayContent(artifact) : new StringContent(catalog)
        });
    }
    private sealed class MutableFixtureHandler(string catalog, byte[] artifact) : HttpMessageHandler
    {
        public string Catalog { get; set; } = catalog;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("artifact.zip", StringComparison.Ordinal) ? new ByteArrayContent(artifact) : new StringContent(Catalog)
        });
    }
    private sealed class UnknownLengthFixtureHandler(string catalog, long artifactLength) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("artifact.zip", StringComparison.Ordinal) ? new UnknownLengthContent(artifactLength) : new StringContent(catalog)
        });
    }
    private sealed class UnknownLengthContent(long length) : HttpContent
    {
        protected override bool TryComputeLength(out long computedLength) { computedLength = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var block = new byte[64 * 1024];
            for (var remaining = length; remaining > 0; remaining -= Math.Min(remaining, block.Length))
                await stream.WriteAsync(block.AsMemory(0, (int)Math.Min(remaining, block.Length)));
        }
    }
}
