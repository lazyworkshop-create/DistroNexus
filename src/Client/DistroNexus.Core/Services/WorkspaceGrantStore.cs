using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace DistroNexus.Core.Services;

/// <summary>Same-user, single-use durable grants for the fixed workspace protocol.</summary>
public sealed class WorkspaceGrantStore
{
    private readonly string _root;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DistroNexus.WorkspaceGrant.v1");
    public WorkspaceGrantStore(string root) { _root = root ?? throw new ArgumentNullException(nameof(root)); Directory.CreateDirectory(_root); }
    public async Task<string> IssueAsync(string kind, string payload, TimeSpan lifetime, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var record = new WorkspaceGrantRecord(1, CurrentSid(), kind, payload, DateTimeOffset.UtcNow.Add(lifetime));
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record)), Entropy, DataProtectionScope.CurrentUser);
        var target = Path.Combine(_root, Hash(token) + ".grant");
        var temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(temporary, bytes, ct);
        File.Move(temporary, target);
        return token;
    }
    public async Task<WorkspaceGrantRecord> ConsumeAsync(string token, string kind, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64) throw new InvalidOperationException("Workspace.PreviewInvalid");
        var target = Path.Combine(_root, Hash(token) + ".grant");
        var consumed = target + ".consumed-" + Guid.NewGuid().ToString("N");
        try { File.Move(target, consumed); } catch (FileNotFoundException) { throw new InvalidOperationException("Workspace.PreviewExpired"); } catch (IOException) { throw new InvalidOperationException("Workspace.PreviewExpired"); }
        try
        {
            var bytes = await File.ReadAllBytesAsync(consumed, ct);
            var record = JsonSerializer.Deserialize<WorkspaceGrantRecord>(Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser))) ?? throw new InvalidOperationException("Workspace.PreviewInvalid");
            if (record.SchemaVersion != 1 || record.Kind != kind || record.Sid != CurrentSid() || record.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Workspace.PreviewExpired");
            return record;
        }
        catch (CryptographicException) { throw new InvalidOperationException("Workspace.PreviewInvalid"); }
        finally { try { File.Delete(consumed); } catch (IOException) { } }
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string CurrentSid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Workspace.PreviewInvalid");
}
public sealed record WorkspaceGrantRecord(int SchemaVersion, string Sid, string Kind, string Payload, DateTimeOffset ExpiresAt);
