using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Owns the state-bound, same-user preview grants for per-instance configuration.</summary>
public sealed class InstanceConfigurationGrantService(IDistributionConfigurationService configuration, string root)
{
    private readonly string _root = Path.Combine(root, "instance-configuration-grants");
    private static string Sid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Instance.ConfigGrantInvalid");

    public async Task<InstanceConfigurationReadResult> ReadAsync(string name, CancellationToken ct = default)
    {
        var document = await configuration.ReadAsync(ValidateName(name), ct);
        var allowed = WslConfigurationSchema.Distribution.Select(x => $"{x.Section}.{x.Key}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var safe = document.Settings.Values.Where(x => allowed.Contains(x.Key) && x.Key.Length <= 128 && x.Value.Length <= 1024)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return new(name, SchemaRevision, safe, document.Fingerprint, "Instance.ConfigRead");
    }

    public async Task<InstanceConfigurationRecoveryResult> RecoveryAsync(string name, CancellationToken ct = default)
    {
        name = ValidateName(name); var offer = await configuration.GetRecoveryOfferAsync(name, ct);
        return new(name, offer.IsAvailable ? "Available" : "Unavailable", offer.IsAvailable ? Hash(offer.MessageKey) : null, offer.IsAvailable ? "Instance.ConfigRecoveryAvailable" : "Instance.ConfigRecoveryUnavailable");
    }

    public async Task<InstanceConfigurationPreviewResult> PreviewAsync(string name, IReadOnlyDictionary<string, string?> changes, CancellationToken ct = default)
    {
        name = ValidateName(name); ValidateChanges(changes);
        var current = await configuration.ReadAsync(name, ct);
        if (changes.Count == 0) throw new InvalidOperationException("Instance.ConfigNoChanges");
        await configuration.PreviewAsync(name, changes, current.Fingerprint, ct);
        var recovery = await RecoveryAsync(name, ct); var canonical = Canonical(changes); var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
        await StoreAsync(token, new Grant(Sid(), name, SchemaRevision, current.Fingerprint, canonical, recovery.RecoveryFingerprint, expiry), ct);
        return new(token, expiry, name, changes.Keys.Order(StringComparer.Ordinal).Take(32).ToArray(), "Instance.ConfigPreview");
    }

    public async Task<InstanceConfigurationSaveResult> ExecuteAsync(string token, CancellationToken ct = default)
    {
        var grant = await ConsumeAsync(token, ct); if (grant.SchemaRevision != SchemaRevision) throw new InvalidOperationException("Instance.ConfigStateChanged"); var current = await configuration.ReadAsync(grant.Name, ct);
        if (!string.Equals(current.Fingerprint, grant.Fingerprint, StringComparison.Ordinal)) throw new InvalidOperationException("Instance.ConfigStateChanged");
        var recovery = await RecoveryAsync(grant.Name, ct);
        if (!string.Equals(recovery.RecoveryFingerprint, grant.RecoveryFingerprint, StringComparison.Ordinal)) throw new InvalidOperationException("Instance.ConfigStateChanged");
        var result = await configuration.SaveAsync(grant.Name, Parse(grant.Changes), grant.Fingerprint, ct);
        return new(grant.Name, !string.IsNullOrWhiteSpace(result.BackupPath), result.BackupPath is null ? "None" : "BackupCreated", "Instance.ConfigSaved");
    }

    private async Task StoreAsync(string token, Grant grant, CancellationToken ct)
    {
        Directory.CreateDirectory(_root); var path = FileFor(token); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grant), null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(temp, bytes, ct); File.Move(temp, path, false);
    }
    private async Task<Grant> ConsumeAsync(string token, CancellationToken ct)
    {
        if (token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Instance.ConfigGrantInvalid");
        var path = FileFor(token); var used = path + ".used";
        try { File.Move(path, used, false); } catch (IOException) { throw new InvalidOperationException("Instance.ConfigGrantReplayed"); }
        try
        {
            var grant = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(await File.ReadAllBytesAsync(used, ct), null, DataProtectionScope.CurrentUser)) ?? throw new InvalidOperationException("Instance.ConfigGrantInvalid");
            if (grant.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Instance.ConfigGrantExpired");
            if (!string.Equals(grant.Sid, Sid(), StringComparison.Ordinal)) throw new InvalidOperationException("Instance.ConfigGrantInvalid");
            return grant;
        }
        catch (CryptographicException) { throw new InvalidOperationException("Instance.ConfigGrantInvalid"); }
        finally { try { File.Delete(used); } catch { } }
    }
    private string FileFor(string token) => Path.Combine(_root, Hash(token) + ".grant");
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.IndexOfAny(['\r','\n','\0']) >= 0) throw new InvalidOperationException("Instance.ConfigNotFound"); return name; }
    private static void ValidateChanges(IReadOnlyDictionary<string,string?> changes) { var allowed = WslConfigurationSchema.Distribution.Select(x => $"{x.Section}.{x.Key}").ToHashSet(StringComparer.OrdinalIgnoreCase); if (changes is null || changes.Count > 32 || changes.Any(x => !allowed.Contains(x.Key) || string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 128 || x.Value?.Length > 1024 || x.Key.IndexOfAny(['\r','\n','\0']) >= 0 || x.Value?.IndexOfAny(['\r','\n','\0']) >= 0)) throw new InvalidOperationException("Instance.ConfigInvalidChanges"); }
    private static string Canonical(IReadOnlyDictionary<string,string?> c) => JsonSerializer.Serialize(c.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    private static IReadOnlyDictionary<string,string?> Parse(string value) => JsonSerializer.Deserialize<Dictionary<string,string?>>(value) ?? throw new InvalidOperationException("Instance.ConfigGrantInvalid");
    private const int SchemaRevision = 1;
    private sealed record Grant(string Sid, string Name, int SchemaRevision, string Fingerprint, string Changes, string? RecoveryFingerprint, DateTimeOffset ExpiresAt);
}
