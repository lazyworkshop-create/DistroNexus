using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>WSLg-only short-lived discovery grants. Files are per-user DPAPI protected and hash-addressed.</summary>
internal sealed class WslgDiscoveryGrantStore
{
    private const int SchemaVersion = 1;
    private const int MaxRecords = 64;
    private const long MaxBytes = 8 * 1024 * 1024;
    private const int MaxApplications = 2048;
    private const int MaxSerializedBytes = 2 * 1024 * 1024;
    private readonly string _directory;
    private readonly TimeProvider _clock;
    public WslgDiscoveryGrantStore(string root, TimeProvider? clock = null) { _directory = Path.Combine(root, "wslg-discovery-grants"); _clock = clock ?? TimeProvider.System; }

    public async Task<(string Token, DateTimeOffset ExpiresAt)> IssueAsync(string instanceName, IReadOnlyList<WslgApplication> applications, CancellationToken ct)
    {
        if (applications.Count > MaxApplications) throw Invalid("Wslg.DiscoveryGrantInvalid");
        Directory.CreateDirectory(_directory);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = _clock.GetUtcNow().AddMinutes(2);
        var record = new Grant(SchemaVersion, instanceName, expires, applications);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(record);
        if (plaintext.Length > MaxSerializedBytes) throw Invalid("Wslg.DiscoveryGrantInvalid");
        var bytes = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        var target = Path.Combine(_directory, Hash(token) + ".grant");
        await using var lockFile = await OpenLockAsync(ct); Sweep();
        if (Directory.EnumerateFiles(_directory, "*.grant").Count() >= MaxRecords || Directory.EnumerateFiles(_directory, "*.grant").Sum(p => new FileInfo(p).Length) + bytes.Length > MaxBytes) throw Invalid("Wslg.DiscoveryGrantInvalid");
        await using var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await file.WriteAsync(bytes, ct);
        return (token, expires);
    }

    public async Task<WslgApplication> ResolveAsync(string token, string applicationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c))) throw Invalid("Wslg.DiscoveryGrantInvalid");
        var path = Path.Combine(_directory, Hash(token) + ".grant");
        await using var lockFile = await OpenLockAsync(ct); Sweep(path);
        if (!File.Exists(path)) throw Invalid("Wslg.DiscoveryGrantInvalid");
        Grant? grant;
        try { grant = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(await File.ReadAllBytesAsync(path, ct), null, DataProtectionScope.CurrentUser)); }
        catch (CryptographicException) { throw Invalid("Wslg.DiscoveryGrantInvalid"); }
        catch (JsonException) { throw Invalid("Wslg.DiscoveryGrantInvalid"); }
        if (grant is null || grant.SchemaVersion != SchemaVersion) throw Invalid("Wslg.DiscoveryGrantInvalid");
        if (grant.ExpiresAt <= _clock.GetUtcNow()) { TryDelete(path); throw Invalid("Wslg.DiscoveryGrantExpired"); }
        return grant.Applications.SingleOrDefault(a => string.Equals(a.Id, applicationId, StringComparison.Ordinal)) ?? throw Invalid("Wslg.ApplicationNotFound");
    }

    private async Task<FileStream> OpenLockAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, ".lock");
        for (var i = 0; ; i++) try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) when (i < 100) { await Task.Delay(20, ct); }
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private void Sweep(string? excludedPath = null)
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "*.grant"))
        {
            if (string.Equals(path, excludedPath, StringComparison.OrdinalIgnoreCase)) continue;
            try { var grant = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)); if (grant is null || grant.ExpiresAt <= _clock.GetUtcNow()) TryDelete(path); }
            catch { TryDelete(path); }
        }
    }
    private static InvalidOperationException Invalid(string code) => new(code);
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    private sealed record Grant(int SchemaVersion, string InstanceName, DateTimeOffset ExpiresAt, IReadOnlyList<WslgApplication> Applications);
}
