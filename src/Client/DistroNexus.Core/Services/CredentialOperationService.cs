using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Owns the closed, same-user credential preview/execute grant boundary.</summary>
public sealed class CredentialOperationService
{
    private static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(2);
    private readonly IProcessRunner _processes;
    private readonly Func<string, CancellationToken, Task<bool>> _instanceExists;
    private readonly string _grantRoot;
    private readonly TimeProvider _clock;
    private readonly Func<string, CancellationToken, Task<string>> _fingerprint;

    public CredentialOperationService(IProcessRunner processes, Func<string, CancellationToken, Task<bool>> instanceExists, string grantRoot, TimeProvider? clock = null, Func<string, CancellationToken, Task<string>>? fingerprint = null)
    {
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _instanceExists = instanceExists ?? throw new ArgumentNullException(nameof(instanceExists));
        _grantRoot = grantRoot ?? throw new ArgumentNullException(nameof(grantRoot));
        _clock = clock ?? TimeProvider.System;
        _fingerprint = fingerprint ?? (async (name, ct) => await _instanceExists(name, ct).ConfigureAwait(false) ? "present" : "missing");
    }

    public async Task<CredentialOperationPreview> PreviewAsync(string instanceName, string username, string secretEnvelope, CancellationToken cancellationToken = default)
    {
        Validate(instanceName, username, secretEnvelope);
        var bytes = Unprotect(secretEnvelope);
        try { if (bytes.Length is < 1 or > 1024) throw new InvalidOperationException("Lifecycle.CredentialInvalid"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        if (!await _instanceExists(instanceName, cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Lifecycle.CredentialStateChanged");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = _clock.GetUtcNow().Add(GrantLifetime);
        var grant = new CredentialOperationGrant(CurrentSid(), instanceName, username, secretEnvelope, Identity(secretEnvelope), await _fingerprint(instanceName, cancellationToken).ConfigureAwait(false), expires);
        Directory.CreateDirectory(_grantRoot);
        var target = GrantPath(token);
        var data = JsonSerializer.SerializeToUtf8Bytes(grant);
        try
        {
            var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(target, protectedData, cancellationToken).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(data); }
        return new CredentialOperationPreview(token, instanceName, expires);
    }

    public async Task<CredentialOperationResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken) || previewToken.Length != 64 || previewToken.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Lifecycle.CredentialGrantInvalid");
        var path = GrantPath(previewToken);
        byte[] protectedData;
        try
        {
            var consumed = path + ".consumed";
            File.Move(path, consumed, false);
            using (var file = new FileStream(consumed, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                protectedData = new byte[file.Length];
                _ = await file.ReadAsync(protectedData, cancellationToken).ConfigureAwait(false);
            }
            File.Delete(consumed);
        }
        catch (FileNotFoundException) { throw new InvalidOperationException("Lifecycle.CredentialGrantInvalid"); }
        catch (IOException) { throw new InvalidOperationException("Lifecycle.CredentialGrantInvalid"); }
        CredentialOperationGrant grant;
        byte[] grantData = [];
        try
        {
            grantData = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
            grant = JsonSerializer.Deserialize<CredentialOperationGrant>(grantData) ?? throw new InvalidOperationException();
        }
        catch { throw new InvalidOperationException("Lifecycle.CredentialGrantInvalid"); }
        finally { CryptographicOperations.ZeroMemory(protectedData); CryptographicOperations.ZeroMemory(grantData); }
        if (!string.Equals(grant.Sid, CurrentSid(), StringComparison.Ordinal)) throw new InvalidOperationException("Lifecycle.CredentialGrantInvalid");
        if (grant.ExpiresAt <= _clock.GetUtcNow()) throw new InvalidOperationException("Lifecycle.CredentialGrantExpired");
        if (!await _instanceExists(grant.InstanceName, cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException("Lifecycle.CredentialStateChanged");
        if (!string.Equals(grant.InstanceFingerprint, await _fingerprint(grant.InstanceName, cancellationToken).ConfigureAwait(false), StringComparison.Ordinal) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(grant.EnvelopeIdentity), Convert.FromHexString(Identity(grant.SecretEnvelope)))) throw new InvalidOperationException("Lifecycle.CredentialStateChanged");
        var password = Unprotect(grant.SecretEnvelope);
        try
        {
            var input = grant.Username + ":" + Encoding.UTF8.GetString(password) + "\n";
            var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", grant.InstanceName, "--user", "root", "--exec", "chpasswd"], TimeSpan.FromSeconds(30), StandardInput: input), cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None) throw new InvalidOperationException("Lifecycle.CredentialFailed");
            return new CredentialOperationResult(true, grant.InstanceName, "Lifecycle.CredentialSucceeded");
        }
        finally { CryptographicOperations.ZeroMemory(password); }
    }

    private static void Validate(string name, string username, string envelope)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
            string.IsNullOrWhiteSpace(username) || !System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-z_][a-z0-9_-]{0,31}$") ||
            string.IsNullOrWhiteSpace(envelope) || envelope.Length > 16_384) throw new InvalidOperationException("Lifecycle.CredentialInvalid");
    }
    private string GrantPath(string token) => Path.Combine(_grantRoot, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private static byte[] Unprotect(string envelope) { try { return ProtectedData.Unprotect(Convert.FromBase64String(envelope), null, DataProtectionScope.CurrentUser); } catch { throw new InvalidOperationException("Lifecycle.CredentialInvalid"); } }
    private static string CurrentSid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Lifecycle.CredentialGrantInvalid");
    private static string Identity(string envelope) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope)));
}
