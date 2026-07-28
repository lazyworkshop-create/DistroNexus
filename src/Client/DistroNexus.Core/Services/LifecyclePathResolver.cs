using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Principal;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Canonicalizes the deliberately narrow local paths accepted by lifecycle previews.</summary>
public sealed class LifecyclePathResolver
{
    public string ResolveDestinationRoot(string path, string instanceName) => CombineUnderRoot(ResolveApprovedRoot(path), instanceName);
    public string ResolveArchiveDestination(string path)
    {
        var full = ResolveExistingParent(path);
        if (!full.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) || Directory.Exists(full) || File.Exists(full)) throw Invalid();
        return full;
    }
    public string ResolveImportSource(string path)
    {
        var full = ResolveExistingParent(path);
        if (!full.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) || !File.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        return full;
    }
    public string ResolveApprovedRoot(string path)
    {
        var full = ResolveExistingParent(path);
        if (!Directory.Exists(full) || string.Equals(Path.GetPathRoot(full), full, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        return full;
    }
    public void Revalidate(string path, bool mustExist, bool mustBeEmptyDirectory = false)
    {
        var full = ResolveExistingParent(path);
        if (mustExist && !File.Exists(full) && !Directory.Exists(full)) throw Invalid();
        if ((File.Exists(full) || Directory.Exists(full)) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        if (mustBeEmptyDirectory && Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) throw Invalid();
    }
    public static string ValidateInstanceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\')) throw Invalid();
        return name.Trim();
    }
    private static string CombineUnderRoot(string root, string name)
    {
        var target = Path.GetFullPath(Path.Combine(root, ValidateInstanceName(name)));
        if (!target.StartsWith(root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        if (Directory.Exists(target) || File.Exists(target)) throw Invalid();
        return target;
    }
    private static string ResolveExistingParent(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.Any(char.IsControl) || path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("\\\\?\\", StringComparison.Ordinal)) throw Invalid();
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(full) || new DriveInfo(root).DriveType != DriveType.Fixed || IsProtectedSystemPath(full)) throw Invalid();
        var cursor = Directory.Exists(full) ? full : Path.GetDirectoryName(full);
        while (!string.IsNullOrEmpty(cursor)) { if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) throw Invalid(); cursor = Path.GetDirectoryName(cursor); }
        return full;
    }
    private static bool IsProtectedSystemPath(string path)
    {
        var protectedRoots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) };
        return protectedRoots.Where(x => !string.IsNullOrWhiteSpace(x)).Any(root => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
    private static ArgumentException Invalid() => new("Lifecycle.PathInvalid");
}

/// <summary>Per-user, hash-addressed, atomically consumed lifecycle grants.</summary>
public sealed class LifecycleGrantStore
{
    private readonly string _directory; private readonly TimeProvider _clock; private readonly Func<string> _sid;
    public LifecycleGrantStore(string root, TimeProvider? clock = null, Func<string>? sid = null) { _directory = Path.Combine(root, "lifecycle-grants"); _clock = clock ?? TimeProvider.System; _sid = sid ?? (() => WindowsIdentity.GetCurrent().User?.Value ?? throw Invalid("Lifecycle.GrantInvalid")); }
    internal async Task<(string Token, DateTimeOffset ExpiresAt)> IssueAsync(LifecycleOperationGrant grant, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_directory); var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grant), null, DataProtectionScope.CurrentUser);
        await using var gate = await LockAsync(ct); var path = PathFor(token); await File.WriteAllBytesAsync(path, bytes, ct); if (grant.Target is not null) await File.WriteAllBytesAsync(path + ".reservation", ProtectedData.Protect(Encoding.UTF8.GetBytes(grant.Target), null, DataProtectionScope.CurrentUser), ct); return (token, grant.ExpiresAt);
    }
    internal async Task<LifecycleOperationGrant> ConsumeAsync(string token, CancellationToken ct = default)
    {
        if (token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c))) throw Invalid("Lifecycle.GrantInvalid");
        await using var gate = await LockAsync(ct); var path = PathFor(token); if (!File.Exists(path)) throw Invalid("Lifecycle.GrantInvalid");
        try { var grant = JsonSerializer.Deserialize<LifecycleOperationGrant>(ProtectedData.Unprotect(await File.ReadAllBytesAsync(path, ct), null, DataProtectionScope.CurrentUser)) ?? throw Invalid("Lifecycle.GrantInvalid"); File.Delete(path); if (grant.ExpiresAt <= _clock.GetUtcNow()) { ReleaseReservation(path); throw Invalid("Lifecycle.GrantExpired"); } if (!string.Equals(grant.Sid, _sid(), StringComparison.Ordinal)) { ReleaseReservation(path); throw Invalid("Lifecycle.GrantInvalid"); } TryDelete(path + ".reservation"); return grant; }
        catch (CryptographicException) { TryDelete(path); ReleaseReservation(path); throw Invalid("Lifecycle.GrantInvalid"); }
        catch (JsonException) { TryDelete(path); ReleaseReservation(path); throw Invalid("Lifecycle.GrantInvalid"); }
    }
    private async Task<FileStream> LockAsync(CancellationToken ct) { Directory.CreateDirectory(_directory); for (var i = 0; ; i++) try { return new FileStream(Path.Combine(_directory, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch (IOException) when (i < 100) { await Task.Delay(20, ct); } }
    private string PathFor(string token) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private static void TryDelete(string p) { try { File.Delete(p); } catch (IOException) { } }
    private static void ReleaseReservation(string grantPath) { var marker = grantPath + ".reservation"; try { var target = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(marker), null, DataProtectionScope.CurrentUser)); File.Delete(target + ".distronexus-reservation"); } catch (IOException) { } catch (CryptographicException) { } finally { TryDelete(marker); } }
    private static InvalidOperationException Invalid(string c) => new(c);
}
