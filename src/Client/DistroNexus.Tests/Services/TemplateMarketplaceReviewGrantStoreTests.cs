using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using System.Text;
using System.Text.Json;

namespace DistroNexus.Tests.Services;

public sealed class TemplateMarketplaceReviewGrantStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task DurableGrant_IsConsumedAcrossStoreInstances_AndCannotReplay()
    {
        var issue = new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity);
        var token = new string('d', 64);
        var grant = Grant(token);
        await issue.IssueAsync(grant, default);
        var consumed = await new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity).ConsumeAsync(token, default);
        Assert.Equal(grant.Manifest.Id, consumed.Manifest.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => issue.ConsumeAsync(token, default));
    }
    [Fact]
    public async Task DurableGrant_RejectsDifferentSid()
    {
        var issue = new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity);
        var token = new string('e', 64);
        await issue.IssueAsync(Grant(token), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TemplateMarketplaceReviewGrantStore(_root, () => "S-2", Identity, Identity).ConsumeAsync(token, default));
    }
    [Fact]
    public async Task DurableGrant_RejectsExpiryTamperingAndConcurrentConsumption()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var issue = new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity, clock);
        var token = new string('a', 64);
        await issue.IssueAsync(Grant(token, clock.GetUtcNow().AddMinutes(1)), default);
        var path = Path.Combine(_root, "template-marketplace-review-grants", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => issue.ConsumeAsync(token, default));

        var replayToken = new string('b', 64);
        await issue.IssueAsync(Grant(replayToken, clock.GetUtcNow().AddMinutes(1)), default);
        var stores = new[] { new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity, clock), new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity, clock) };
        var results = await Task.WhenAll(stores.Select(async store => { try { await store.ConsumeAsync(replayToken, default); return true; } catch (InvalidOperationException) { return false; } }));
        Assert.Equal(1, results.Count(result => result));

        var expiringToken = new string('c', 64);
        await issue.IssueAsync(Grant(expiringToken, clock.GetUtcNow().AddMinutes(1)), default);
        clock.Advance(TimeSpan.FromMinutes(2));
        var expired = await Assert.ThrowsAsync<InvalidOperationException>(() => issue.ConsumeAsync(expiringToken, default));
        Assert.Equal("Template.ReviewGrantExpired", expired.Message);
    }
    [Fact]
    public async Task DurableGrant_RejectsTamperingOfEveryBoundProvenanceComponent()
    {
        var mutations = new Func<TemplateMarketplaceReviewGrantStore.Record, TemplateMarketplaceReviewGrantStore.Record>[]
        {
            record => record with { CanonicalManifest = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")) },
            record => record with { ArtifactRootPath = "other-root" },
            record => record with { ExecutableFilesDigest = new string('d', 64) },
            record => record with { ScriptDiff = new TemplateScriptDiff(["other"], [], []), ScriptDiffDigest = new string('e', 64) }
        };
        for (var i = 0; i < mutations.Length; i++)
        {
            var token = i.ToString("x") + new string('f', 63);
            var store = new TemplateMarketplaceReviewGrantStore(_root, () => "S-1", Identity, Identity);
            await store.IssueAsync(Grant(token), default);
            var path = GrantPath(token);
            var record = JsonSerializer.Deserialize<TemplateMarketplaceReviewGrantStore.Record>(await File.ReadAllBytesAsync(path))!;
            await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(mutations[i](record)));
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync(token, default));
            Assert.Equal("Template.ReviewGrantInvalid", failure.Message);
        }
    }
    private string GrantPath(string token) => Path.Combine(_root, "template-marketplace-review-grants", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private static TemplateReviewGrant Grant(string token, DateTimeOffset? expiresAt = null) => new(token, "source", "https://example.test/catalog.json", new TemplateManifestV2 { Id = "template", Version = "1", ArtifactSha256 = new string('a', 64) }, new TemplateArtifact(new string('a', 64), "internal", DateTimeOffset.UtcNow), new TemplateScriptDiff([], [], []), expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(1), new string('b', 64));
    private static byte[] Identity(byte[] value) => value;
    private sealed class TestClock(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
        public void Advance(TimeSpan value) => _value = _value.Add(value);
    }
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
}
