using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace DistroNexus.Core.Services;

public sealed class TemplateLocalPreviewStore
{
    private const int MaxValueBytes = 1024 * 1024;
    private readonly string _directory;
    private readonly Func<string> _sid;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;
    private readonly TimeProvider _clock;

    public TemplateLocalPreviewStore(
        string root,
        Func<string>? sid = null,
        Func<byte[], byte[]>? protect = null,
        Func<byte[], byte[]>? unprotect = null,
        TimeProvider? clock = null)
    {
        _directory = Path.Combine(root, "template-local-previews");
        _sid = sid ?? (() => WindowsIdentity.GetCurrent().User?.Value ?? throw Invalid("Template.GrantInvalid"));
        _protect = protect ?? (value => ProtectedData.Protect(value, null, DataProtectionScope.CurrentUser));
        _unprotect = unprotect ?? (value => ProtectedData.Unprotect(value, null, DataProtectionScope.CurrentUser));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<string> IssueAsync(string operation, string value, CancellationToken cancellationToken)
    {
        ValidateOperation(operation);
        if (Encoding.UTF8.GetByteCount(value) > MaxValueBytes) throw Invalid("Template.InvalidRequest");
        Directory.CreateDirectory(_directory);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new Grant(1, _sid(), operation, value, _clock.GetUtcNow().AddMinutes(5));
        var target = PathFor(token);
        var temporary = target + ".new." + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, _protect(JsonSerializer.SerializeToUtf8Bytes(record)), cancellationToken);
            File.Move(temporary, target);
            return token;
        }
        finally { TryDelete(temporary); }
    }

    public async Task<Grant> ConsumeAsync(string token, string operation, CancellationToken cancellationToken)
    {
        ValidateOperation(operation);
        if (token.Length != 64 || token.Any(value => !Uri.IsHexDigit(value))) throw Invalid("Template.GrantInvalid");
        Directory.CreateDirectory(_directory);
        var source = PathFor(token);
        var claimed = source + ".consumed." + Guid.NewGuid().ToString("N");
        try
        {
            File.Move(source, claimed);
            var grant = JsonSerializer.Deserialize<Grant>(_unprotect(await File.ReadAllBytesAsync(claimed, cancellationToken))) ?? throw Invalid("Template.GrantInvalid");
            if (grant.SchemaVersion != 1 || !string.Equals(grant.Sid, _sid(), StringComparison.Ordinal) || !string.Equals(grant.Operation, operation, StringComparison.Ordinal)) throw Invalid("Template.GrantInvalid");
            if (grant.ExpiresAt <= _clock.GetUtcNow()) throw Invalid("Template.GrantExpired");
            if (Encoding.UTF8.GetByteCount(grant.Value) > MaxValueBytes) throw Invalid("Template.GrantInvalid");
            return grant;
        }
        catch (FileNotFoundException) { throw Invalid("Template.GrantInvalid"); }
        catch (IOException) { throw Invalid("Template.GrantInvalid"); }
        catch (CryptographicException) { throw Invalid("Template.GrantInvalid"); }
        catch (JsonException) { throw Invalid("Template.GrantInvalid"); }
        finally { TryDelete(claimed); }
    }

    private string PathFor(string token) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private static void ValidateOperation(string operation)
    {
        if (operation is not ("import" or "export" or "remove")) throw Invalid("Template.InvalidRequest");
    }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    private static InvalidOperationException Invalid(string code) => new(code);
    public sealed record Grant(int SchemaVersion, string Sid, string Operation, string Value, DateTimeOffset ExpiresAt);
}
