using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Issues same-user, short-lived, single-use compaction grants for fixed bridge execution.</summary>
public sealed class InstanceCompactionService : IInstanceCompactionService
{
    private const int TokenBytes = 32;
    private readonly IRegisteredInstanceCompactionAdapter _adapter;
    private readonly string _root;
    private readonly TimeProvider _clock;
    private readonly Func<string> _sid;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;

    public InstanceCompactionService(IRegisteredInstanceCompactionAdapter adapter, string? grantRoot = null, TimeProvider? clock = null, Func<string>? sid = null, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _root = grantRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus", "instance-compaction-grants");
        _clock = clock ?? TimeProvider.System;
        _sid = sid ?? (() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid"));
        _protect = protect ?? (bytes => ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        _unprotect = unprotect ?? (bytes => ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
    }

    public async Task<InstanceCompactionPreview> PreviewAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        var state = await _adapter.GetAsync(name, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Lifecycle.CompactionInstanceNotFound");
        if (!string.Equals(state.PrerequisiteOutcome, "Ready", StringComparison.Ordinal))
            throw new InvalidOperationException(state.PrerequisiteOutcome);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes));
        var expires = _clock.GetUtcNow().AddMinutes(2);
        var grant = new Grant(1, _sid(), state.Name, state.Identity, state.VhdxIdentity, state.IsRunning, state.CurrentSizeBytes, state.Method, expires);
        await WriteAsync(token, grant, cancellationToken).ConfigureAwait(false);
        return new(token, state.Name, state.CurrentSizeBytes, "Measured", ["A fixed compaction method is available."], ["Current size is not an estimate of reclaimable space."], expires);
    }

    public async Task<InstanceCompactionResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        var grant = await ConsumeAsync(previewToken, cancellationToken).ConfigureAwait(false);
        if (grant.ExpiresAt <= _clock.GetUtcNow()) return Failed(grant, "Lifecycle.CompactionGrantExpired");
        if (!string.Equals(grant.Sid, _sid(), StringComparison.Ordinal)) return Failed(grant, "Lifecycle.CompactionGrantInvalid");
        var current = await _adapter.GetAsync(grant.Name, cancellationToken).ConfigureAwait(false);
        if (current is null || current.PrerequisiteOutcome != "Ready" || !Equivalent(grant, current)) return Failed(grant, "Lifecycle.CompactionStateChanged");
        var execution = await _adapter.CompactAsync(current, cancellationToken).ConfigureAwait(false);
        var after = execution.AfterBytes;
        return new(execution.Succeeded, grant.Name, execution.OutcomeCode, grant.CurrentSizeBytes, after,
            after is null ? null : Math.Max(0, grant.CurrentSizeBytes - after.Value), execution.Method, execution.Restarted, execution.RecoveryAction);
    }

    private static InstanceCompactionResult Failed(Grant grant, string code) => new(false, grant.Name, code, null, null, null, grant.Method, false);
    private static bool Equivalent(Grant grant, RegisteredInstanceCompactionState current) =>
        string.Equals(grant.Identity, current.Identity, StringComparison.Ordinal) &&
        string.Equals(grant.VhdxIdentity, current.VhdxIdentity, StringComparison.Ordinal) &&
        grant.WasRunning == current.IsRunning && grant.CurrentSizeBytes == current.CurrentSizeBytes &&
        string.Equals(grant.Method, current.Method, StringComparison.Ordinal);

    private async Task WriteAsync(string token, Grant grant, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        await using var gate = await OpenLockAsync(ct).ConfigureAwait(false);
        Sweep();
        var bytes = _protect(JsonSerializer.SerializeToUtf8Bytes(grant));
        await using var stream = new FileStream(PathFor(token), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task<Grant> ConsumeAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != TokenBytes * 2 || token.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid");
        Directory.CreateDirectory(_root);
        await using var gate = await OpenLockAsync(ct).ConfigureAwait(false);
        var source = PathFor(token);
        if (!File.Exists(source)) throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid");
        var consumed = source + ".consumed." + Guid.NewGuid().ToString("N");
        try
        {
            File.Move(source, consumed);
            var grant = JsonSerializer.Deserialize<Grant>(_unprotect(await File.ReadAllBytesAsync(consumed, ct).ConfigureAwait(false))) ?? throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid");
            File.Delete(consumed);
            return grant.SchemaVersion == 1 ? grant : throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid");
        }
        catch (IOException) { throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid"); }
        catch (CryptographicException) { TryDelete(consumed); throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid"); }
        catch (JsonException) { TryDelete(consumed); throw new InvalidOperationException("Lifecycle.CompactionGrantInvalid"); }
    }

    private async Task<FileStream> OpenLockAsync(CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++) try { return new FileStream(Path.Combine(_root, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) when (attempt < 100) { await Task.Delay(20, ct).ConfigureAwait(false); }
    }
    private string PathFor(string token) => Path.Combine(_root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private void Sweep()
    {
        foreach (var path in Directory.EnumerateFiles(_root, "*.grant").Take(256))
        {
            try { var grant = JsonSerializer.Deserialize<Grant>(_unprotect(File.ReadAllBytes(path))); if (grant is null || grant.ExpiresAt <= _clock.GetUtcNow()) File.Delete(path); }
            catch (CryptographicException) { TryDelete(path); } catch (JsonException) { TryDelete(path); }
        }
    }
    private static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("A valid registered instance name is required.", nameof(name)); }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    private sealed record Grant(int SchemaVersion, string Sid, string Name, string Identity, string VhdxIdentity, bool WasRunning, long CurrentSizeBytes, string Method, DateTimeOffset ExpiresAt);
}
