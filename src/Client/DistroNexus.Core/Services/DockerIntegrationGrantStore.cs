using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DistroNexus.Core.Services;

/// <summary>Same-user, short-lived, single-use Docker integration preview grants.</summary>
internal sealed class DockerIntegrationGrantStore
{
    private const int MaxRecords = 64;
    private const int MaxBytes = 1024 * 1024;
    private readonly string _directory;
    public DockerIntegrationGrantStore(string root) => _directory = Path.Combine(root, "docker-integration-grants");
    public async Task IssueAsync(string token, DockerIntegrationGrant grant, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); Sweep();
        if (Directory.EnumerateFiles(_directory, "*.grant").Count() >= MaxRecords || Directory.EnumerateFiles(_directory, "*.grant").Sum(x => new FileInfo(x).Length) >= MaxBytes) throw Invalid("DockerIntegration.PreviewInvalid");
        var protectedBytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grant), null, DataProtectionScope.CurrentUser);
        await using var file = new FileStream(PathFor(token), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await file.WriteAsync(protectedBytes, ct); await file.FlushAsync(ct);
    }
    public async Task<DockerIntegrationGrant> ConsumeAsync(string token, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); var path = PathFor(token); Sweep(path);
        if (!File.Exists(path)) throw Invalid("DockerIntegration.PreviewInvalid");
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            var grant = JsonSerializer.Deserialize<DockerIntegrationGrant>(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser)) ?? throw Invalid("DockerIntegration.PreviewInvalid");
            File.Delete(path);
            if (grant.ExpiresAt <= DateTimeOffset.UtcNow) throw Invalid("DockerIntegration.PreviewExpired");
            return grant;
        }
        catch (CryptographicException) { TryDelete(path); throw Invalid("DockerIntegration.PreviewInvalid"); }
        catch (JsonException) { TryDelete(path); throw Invalid("DockerIntegration.PreviewInvalid"); }
    }
    private async Task<FileStream> LockAsync(CancellationToken ct)
    { Directory.CreateDirectory(_directory); var path=Path.Combine(_directory,".lock"); for(var i=0;;i++) try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);} catch(IOException) when(i<100){await Task.Delay(20,ct);} }
    private string PathFor(string token) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private void Sweep(string? excluded = null) { foreach(var p in Directory.EnumerateFiles(_directory,"*.grant")) { if(string.Equals(p, excluded, StringComparison.OrdinalIgnoreCase)) continue; try { var g=JsonSerializer.Deserialize<DockerIntegrationGrant>(ProtectedData.Unprotect(File.ReadAllBytes(p),null,DataProtectionScope.CurrentUser)); if(g is null || g.ExpiresAt <= DateTimeOffset.UtcNow) TryDelete(p); } catch { TryDelete(p); } } }
    private static void TryDelete(string path) { try { File.Delete(path); } catch(IOException) { } }
    private static InvalidOperationException Invalid(string code) => new(code);
}
internal sealed record DockerIntegrationGrant(string Name, bool Enabled, string Fingerprint, string Identity, DateTimeOffset ExpiresAt);
