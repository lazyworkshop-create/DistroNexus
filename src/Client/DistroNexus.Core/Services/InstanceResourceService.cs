using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Issues durable, same-user, single-use sparse-mode grants for registered WSL2 instances.</summary>
public sealed class InstanceResourceService : IInstanceResourceService
{
    private const int TokenBytes = 32;
    private readonly IRegisteredInstanceSparseAdapter _adapter;
    private readonly string _root;
    private readonly TimeProvider _clock;
    private readonly Func<string> _sid;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;

    public InstanceResourceService(IRegisteredInstanceSparseAdapter adapter, string? grantRoot = null, TimeProvider? clock = null, Func<string>? sid = null, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _root = grantRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus", "instance-sparse-grants");
        _clock = clock ?? TimeProvider.System;
        _sid = sid ?? (() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Current user identity is unavailable."));
        _protect = protect ?? (bytes => ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        _unprotect = unprotect ?? (bytes => ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
    }

    public async Task<InstanceResourceSnapshot> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var state = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        return new(state.Name, state.WslVersion, state.SparseMode);
    }

    public async Task<InstanceSparsePreview> PreviewSparseAsync(string name, bool enabled, CancellationToken cancellationToken = default)
    {
        var state = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes));
        var expires = _clock.GetUtcNow().AddMinutes(2);
        var grant = new Grant(1, state.Name, state.Identity, state.SparseMode, enabled, expires, _sid());
        await WriteGrantAsync(token, grant, cancellationToken).ConfigureAwait(false);
        return new(token, state.Name, enabled, expires, [$"Set sparse mode to {enabled.ToString().ToLowerInvariant()}."]);
    }

    public async Task<InstanceSparseOperationResult> ExecuteSparseAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        var grant = await ConsumeAsync(previewToken, cancellationToken).ConfigureAwait(false);
        if (grant.ExpiresAt <= _clock.GetUtcNow()) return new(false, "InstanceSparse.PreviewExpired");
        if (!string.Equals(grant.Sid, _sid(), StringComparison.Ordinal)) return new(false, "InstanceSparse.PreviewInvalid");
        var state = await _adapter.GetAsync(grant.Name, cancellationToken).ConfigureAwait(false);
        if (state is null || state.WslVersion != 2 || !string.Equals(state.Name, grant.Name, StringComparison.Ordinal) || !string.Equals(state.Identity, grant.Identity, StringComparison.Ordinal) || state.SparseMode != grant.CurrentSparseMode)
            return new(false, "InstanceSparse.StateChanged");
        return await _adapter.SetSparseAsync(state.Name, grant.Enabled, cancellationToken).ConfigureAwait(false)
            ? new(true, "Succeeded") : new(false, "InstanceSparse.SetFailed");
    }

    private async Task<RegisteredInstanceSparseState> RequireAsync(string name, CancellationToken ct)
    {
        ValidateName(name);
        var state = await _adapter.GetAsync(name, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("InstanceSparse.InstanceNotFound");
        if (state.WslVersion != 2) throw new InvalidOperationException("InstanceSparse.Wsl2Required");
        return state;
    }

    private async Task WriteGrantAsync(string token, Grant grant, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        await using var gate = await OpenLockAsync(ct).ConfigureAwait(false);
        Sweep();
        var path = PathFor(token);
        var bytes = _protect(JsonSerializer.SerializeToUtf8Bytes(grant));
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await file.WriteAsync(bytes, ct).ConfigureAwait(false);
        await file.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task<Grant> ConsumeAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != TokenBytes * 2 || token.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("InstanceSparse.PreviewInvalid");
        Directory.CreateDirectory(_root);
        await using var gate = await OpenLockAsync(ct).ConfigureAwait(false);
        var path = PathFor(token);
        if (!File.Exists(path)) throw new InvalidOperationException("InstanceSparse.PreviewInvalid");
        var consumed = path + ".consumed." + Guid.NewGuid().ToString("N");
        try
        {
            File.Move(path, consumed);
            var grant = JsonSerializer.Deserialize<Grant>(_unprotect(await File.ReadAllBytesAsync(consumed, ct).ConfigureAwait(false))) ?? throw new InvalidOperationException("InstanceSparse.PreviewInvalid");
            File.Delete(consumed);
            if (grant.SchemaVersion != 1) throw new InvalidOperationException("InstanceSparse.PreviewInvalid");
            return grant;
        }
        catch (IOException) { throw new InvalidOperationException("InstanceSparse.PreviewInvalid"); }
        catch (CryptographicException) { TryDelete(consumed); throw new InvalidOperationException("InstanceSparse.PreviewInvalid"); }
        catch (JsonException) { TryDelete(consumed); throw new InvalidOperationException("InstanceSparse.PreviewInvalid"); }
    }

    private async Task<FileStream> OpenLockAsync(CancellationToken ct)
    {
        var lockPath = Path.Combine(_root, ".lock");
        for (var attempt = 0; ; attempt++) try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) when (attempt < 100) { await Task.Delay(20, ct).ConfigureAwait(false); }
    }
    private string PathFor(string token) => Path.Combine(_root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private void Sweep()
    {
        var now = _clock.GetUtcNow();
        foreach (var path in Directory.EnumerateFiles(_root, "*.grant").OrderBy(path => path, StringComparer.Ordinal).Take(256))
        {
            try { var grant = JsonSerializer.Deserialize<Grant>(_unprotect(File.ReadAllBytes(path))); if (grant is null || grant.ExpiresAt <= now) DeleteRequired(path); }
            catch (CryptographicException) { DeleteRequired(path); }
            catch (JsonException) { DeleteRequired(path); }
        }
        foreach (var path in Directory.EnumerateFiles(_root, "*.consumed.*").OrderBy(path => path, StringComparer.Ordinal).Take(256))
            if (File.GetLastWriteTimeUtc(path) <= now.UtcDateTime.AddMinutes(-10)) DeleteRequired(path);
    }
    private static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("A valid registered instance name is required.", nameof(name)); }
    private static void DeleteRequired(string path) { try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new InvalidOperationException("InstanceSparse.PreviewInvalid", ex); } }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    private sealed record Grant(int SchemaVersion, string Name, string Identity, bool CurrentSparseMode, bool Enabled, DateTimeOffset ExpiresAt, string Sid);
}
