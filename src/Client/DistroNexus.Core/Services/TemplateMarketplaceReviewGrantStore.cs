using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Durable, same-user, one-shot review grants shared by bridge processes.</summary>
internal sealed class TemplateMarketplaceReviewGrantStore
{
    private readonly string _directory;
    private readonly Func<string> _sid;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;
    private readonly TimeProvider _clock;

    public TemplateMarketplaceReviewGrantStore(string root, Func<string>? sid = null, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null, TimeProvider? clock = null)
    {
        _directory = Path.Combine(root, "template-marketplace-review-grants");
        _sid = sid ?? (() => WindowsIdentity.GetCurrent().User?.Value ?? throw Invalid("Template.ReviewGrantInvalid"));
        _protect = protect ?? (bytes => ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        _unprotect = unprotect ?? (bytes => ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task IssueAsync(TemplateReviewGrant grant, CancellationToken ct)
    {
        ValidateToken(grant.Token);
        if (grant.ExpiresAt <= _clock.GetUtcNow()) throw Invalid("Template.ReviewGrantExpired");
        var record = Record.FromGrant(_sid(), grant);
        record.Validate();
        Directory.CreateDirectory(_directory);
        await using var gate = await LockAsync(ct);
        Sweep();
        var target = PathFor(grant.Token);
        var temporary = target + ".new." + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, _protect(JsonSerializer.SerializeToUtf8Bytes(record)), ct);
            File.Move(temporary, target, overwrite: false);
        }
        catch (IOException) { throw Invalid("Template.ReviewGrantInvalid"); }
        finally { TryDelete(temporary); }
    }

    public async Task<TemplateReviewGrant> ConsumeAsync(string token, CancellationToken ct)
    {
        ValidateToken(token);
        Directory.CreateDirectory(_directory);
        await using var gate = await LockAsync(ct);
        var source = PathFor(token); var claimed = source + ".consumed." + Guid.NewGuid().ToString("N");
        try
        {
            File.Move(source, claimed);
            var record = JsonSerializer.Deserialize<Record>(_unprotect(await File.ReadAllBytesAsync(claimed, ct))) ?? throw Invalid("Template.ReviewGrantInvalid");
            if (record.SchemaVersion != 2 || !string.Equals(record.Sid, _sid(), StringComparison.Ordinal) || !string.Equals(record.Token, token, StringComparison.Ordinal)) throw Invalid("Template.ReviewGrantInvalid");
            if (record.ExpiresAt <= _clock.GetUtcNow()) throw Invalid("Template.ReviewGrantExpired");
            record.Validate();
            return record.ToGrant();
        }
        catch (FileNotFoundException) { throw Invalid("Template.ReviewGrantInvalid"); }
        catch (IOException) { throw Invalid("Template.ReviewGrantInvalid"); }
        catch (CryptographicException) { throw Invalid("Template.ReviewGrantInvalid"); }
        catch (JsonException) { throw Invalid("Template.ReviewGrantInvalid"); }
        finally { TryDelete(claimed); }
    }

    public async Task RevokeSourceAsync(string sourceId, CancellationToken ct)
    {
        Directory.CreateDirectory(_directory);
        await using var gate = await LockAsync(ct);
        foreach (var file in Directory.EnumerateFiles(_directory, "*.grant"))
        {
            try { var record = JsonSerializer.Deserialize<Record>(_unprotect(await File.ReadAllBytesAsync(file, ct))); if (record is null || string.Equals(record.SourceId, sourceId, StringComparison.Ordinal)) TryDelete(file); }
            catch { TryDelete(file); }
        }
    }

    private string PathFor(string token) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private static void ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || token.Any(value => !Uri.IsHexDigit(value))) throw Invalid("Template.ReviewGrantInvalid");
    }
    private async Task<FileStream> LockAsync(CancellationToken ct) { var path = Path.Combine(_directory, ".lock"); for (var i = 0; ; i++) try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch (IOException) when (i < 100) { await Task.Delay(20, ct); } }
    private void Sweep() { foreach (var file in Directory.EnumerateFiles(_directory, "*.grant")) try { var r = JsonSerializer.Deserialize<Record>(_unprotect(File.ReadAllBytes(file))); if (r is null || r.ExpiresAt <= _clock.GetUtcNow()) TryDelete(file); } catch { TryDelete(file); } }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    private static InvalidOperationException Invalid(string code) => new(code);
    /// <summary>Protected durable provenance for one exact reviewed candidate.</summary>
    internal sealed record Record(
        int SchemaVersion,
        string Sid,
        string Token,
        string SourceId,
        string NormalizedSourceUrl,
        string TemplateId,
        string TemplateVersion,
        string ManifestDigest,
        string CanonicalManifest,
        string ArtifactSha256,
        string ArtifactRootPath,
        string ArtifactRootPathDigest,
        string ExecutableFilesDigest,
        TemplateScriptDiff ScriptDiff,
        string ScriptDiffDigest,
        DateTimeOffset ExpiresAt)
    {
        public static Record FromGrant(string sid, TemplateReviewGrant grant)
        {
            var canonical = CanonicalManifestFor(grant.Manifest);
            var executableDigest = ExecutableFilesDigestFor(grant.Manifest.ExecutableFiles);
            var diffDigest = ScriptDiffDigestFor(grant.ScriptDiff);
            return new(2, sid, grant.Token, grant.SourceId, grant.NormalizedSourceUrl, grant.Manifest.Id, grant.Manifest.Version, ManifestDigestFor(grant.Manifest), canonical, grant.Artifact.Sha256, grant.Artifact.RootPath, Digest(Encoding.UTF8.GetBytes(grant.Artifact.RootPath)), executableDigest, grant.ScriptDiff, diffDigest, grant.ExpiresAt);
        }

        public TemplateReviewGrant ToGrant()
        {
            var manifest = JsonSerializer.Deserialize<TemplateManifestV2>(Encoding.UTF8.GetString(Convert.FromBase64String(CanonicalManifest))) ?? throw Invalid("Template.ReviewGrantInvalid");
            return new(Token, SourceId, NormalizedSourceUrl, manifest, new TemplateArtifact(ArtifactSha256, ArtifactRootPath, DateTimeOffset.UtcNow, TemplateId, TemplateVersion), ScriptDiff, ExpiresAt, ManifestDigest, CanonicalManifest, ExecutableFilesDigest, ScriptDiffDigest);
        }

        public void Validate()
        {
            ValidateToken(Token);
            if (string.IsNullOrWhiteSpace(SourceId) || string.IsNullOrWhiteSpace(NormalizedSourceUrl) || string.IsNullOrWhiteSpace(TemplateId) || string.IsNullOrWhiteSpace(TemplateVersion) || string.IsNullOrWhiteSpace(ArtifactRootPath) || !IsSha256(ManifestDigest) || !IsSha256(ArtifactSha256) || !IsSha256(ArtifactRootPathDigest) || !IsSha256(ExecutableFilesDigest) || !IsSha256(ScriptDiffDigest)) throw Invalid("Template.ReviewGrantInvalid");
            TemplateManifestV2 manifest;
            try { manifest = JsonSerializer.Deserialize<TemplateManifestV2>(Encoding.UTF8.GetString(Convert.FromBase64String(CanonicalManifest))) ?? throw Invalid("Template.ReviewGrantInvalid"); }
            catch (FormatException) { throw Invalid("Template.ReviewGrantInvalid"); }
            catch (JsonException) { throw Invalid("Template.ReviewGrantInvalid"); }
            if (!string.Equals(manifest.Id, TemplateId, StringComparison.Ordinal) || !string.Equals(manifest.Version, TemplateVersion, StringComparison.Ordinal) || !string.Equals(manifest.ArtifactSha256, ArtifactSha256, StringComparison.OrdinalIgnoreCase) || !string.Equals(Digest(Encoding.UTF8.GetBytes(ArtifactRootPath)), ArtifactRootPathDigest, StringComparison.Ordinal) || !string.Equals(CanonicalManifestFor(manifest), CanonicalManifest, StringComparison.Ordinal) || !string.Equals(ManifestDigestFor(manifest), ManifestDigest, StringComparison.Ordinal) || !string.Equals(ExecutableFilesDigestFor(manifest.ExecutableFiles), ExecutableFilesDigest, StringComparison.Ordinal) || !string.Equals(ScriptDiffDigestFor(ScriptDiff), ScriptDiffDigest, StringComparison.Ordinal)) throw Invalid("Template.ReviewGrantInvalid");
        }

        private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
        private static string CanonicalManifestFor(TemplateManifestV2 manifest) => Convert.ToBase64String(CanonicalJson(manifest));
        private static string ManifestDigestFor(TemplateManifestV2 manifest) => Digest(CanonicalJson(manifest));
        private static string ExecutableFilesDigestFor(IReadOnlyList<TemplateExecutableFile> files) => DigestCanonical(files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ThenBy(file => file.Sha256, StringComparer.Ordinal).ToArray());
        private static string ScriptDiffDigestFor(TemplateScriptDiff diff) => DigestCanonical(diff);
        private static string DigestCanonical<T>(T value) => Digest(CanonicalJson(value));
        private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        private static byte[] CanonicalJson<T>(T value)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(document.RootElement, writer);
            return stream.ToArray();
        }
        private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer)
        {
            if (value.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(property.Value, writer); } writer.WriteEndObject(); return; }
            if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(item, writer); writer.WriteEndArray(); return; }
            value.WriteTo(writer);
        }
    }
}
