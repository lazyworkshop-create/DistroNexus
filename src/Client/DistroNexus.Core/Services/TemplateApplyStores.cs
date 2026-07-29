using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Same-user, one-shot grant store for the template apply preview/execute contract.</summary>
public sealed class TemplateApplyGrantStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DistroNexus.TemplateApplyGrant.v1");
    private readonly string _root;
    private readonly Func<string> _sid;
    private readonly TimeProvider _clock;
    public TemplateApplyGrantStore(string root, Func<string>? sid = null, TimeProvider? clock = null)
    { _root = root; Directory.CreateDirectory(root); _sid = sid ?? CurrentSid; _clock = clock ?? TimeProvider.System; }

    public async Task<string> IssueAsync(TemplateApplyGrantRecord record, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        record = record with { Sid = _sid(), ExpiresAt = _clock.GetUtcNow().AddMinutes(5) };
        var file = Path.Combine(_root, token + ".grant");
        await File.WriteAllBytesAsync(file, Protect(JsonSerializer.SerializeToUtf8Bytes(record)), ct).ConfigureAwait(false);
        return token;
    }

    public async Task<TemplateApplyGrantRecord> ConsumeAsync(string token, CancellationToken ct = default)
    {
        ValidateToken(token); var file = Path.Combine(_root, token + ".grant"); var claim = file + ".claimed-" + Guid.NewGuid().ToString("N");
        try { File.Move(file, claim); }
        catch (FileNotFoundException) { throw Invalid("Template.GrantInvalid"); }
        catch (IOException) { throw Invalid("Template.GrantInvalid"); }
        try
        {
            var value = JsonSerializer.Deserialize<TemplateApplyGrantRecord>(Unprotect(await File.ReadAllBytesAsync(claim, ct).ConfigureAwait(false))) ?? throw Invalid("Template.GrantInvalid");
            if (value.SchemaVersion != 1 || value.Sid != _sid()) throw Invalid("Template.GrantInvalid");
            if (value.ExpiresAt <= _clock.GetUtcNow()) throw Invalid("Template.GrantExpired");
            return value;
        }
        catch (CryptographicException) { throw Invalid("Template.GrantInvalid"); }
        catch (JsonException) { throw Invalid("Template.GrantInvalid"); }
        finally { try { File.Delete(claim); } catch { } }
    }

    private static byte[] Protect(byte[] bytes) => ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
    private static byte[] Unprotect(byte[] bytes) => ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser);
    public static string CurrentSid() => WindowsIdentity.GetCurrent().User?.Value ?? throw Invalid("Template.GrantInvalid");
    private static void ValidateToken(string token) { if (token.Length != 64 || token.Any(x => !Uri.IsHexDigit(x))) throw Invalid("Template.GrantInvalid"); }
    private static InvalidOperationException Invalid(string code) => new(code);
}

public sealed record TemplateApplyGrantRecord(int SchemaVersion, string Sid, string InstanceName, string TemplateId,
    string TemplateVersion, string SourceUrl, string ManifestDigest, string ArtifactSha256, string ArtifactRootDigest, string ExecutableFilesDigest,
    string NormalizedVariables, string VariablesDigest, string CapabilitiesDigest, string RecoveryFingerprint, bool RecoveryAvailable,
    string RecoveryInstanceName, string RecoveryReason, string RecoveryMessageKey, bool RecoveryDeclined,
    DateTimeOffset ExpiresAt);

/// <summary>Durable operation record with an exclusive state lock. Worker liveness is lock-based, never PID-based.</summary>
public sealed class TemplateApplyOperationStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DistroNexus.TemplateApplyOperation.v1");
    private readonly string _root;
    private readonly string? _stagingRoot;
    private readonly Action<string, string, bool> _replaceFile;
    public TemplateApplyOperationStore(string root, string? stagingRoot = null) : this(root, stagingRoot, File.Move) { }
    internal TemplateApplyOperationStore(string root, string? stagingRoot, Action<string, string, bool> replaceFile)
    {
        _root = root;
        _stagingRoot = stagingRoot is null ? null : Path.GetFullPath(stagingRoot);
        _replaceFile = replaceFile ?? throw new ArgumentNullException(nameof(replaceFile));
        Directory.CreateDirectory(root);
    }
    public async Task CreateAsync(TemplateApplyOperationRecord record, CancellationToken ct = default) => await WriteAsync(record, ct).ConfigureAwait(false);
    public async Task<TemplateApplyOperationRecord> ReadAsync(string id, CancellationToken ct = default)
    {
        Validate(id); var path = Path.Combine(_root, id + ".operation");
        await using var stateLock = await AcquireStateLockAsync(id, ct).ConfigureAwait(false);
        try { return await ReadUnsafeAsync(path, ct).ConfigureAwait(false); }
        catch (FileNotFoundException) { throw Invalid("Template.OperationNotFound"); }
    }
    public async Task WriteAsync(TemplateApplyOperationRecord record, CancellationToken ct = default)
    {
        Validate(record.OperationId); if (record.Sid != TemplateApplyGrantStore.CurrentSid()) throw Invalid("Template.OperationNotFound");
        await using var l = await AcquireStateLockAsync(record.OperationId, ct).ConfigureAwait(false);
        var target = Path.Combine(_root, record.OperationId + ".operation");
        if (File.Exists(target)) { var old = await ReadUnsafeAsync(target, ct).ConfigureAwait(false); if (old.CancelRequested) record = record with { CancelRequested = true }; if (record.WorkerPid is null && old.WorkerPid is not null) record = record with { WorkerPid=old.WorkerPid, WorkerStartedAt=old.WorkerStartedAt }; }
        await WriteProtectedRecordAsync(target, record, ct).ConfigureAwait(false);
    }
    public async Task<TemplateApplyOperationRecord> UpdateAsync(string id, Func<TemplateApplyOperationRecord, TemplateApplyOperationRecord> update, CancellationToken ct = default)
    { var r = await ReadAsync(id, ct).ConfigureAwait(false); r = update(r) with { UpdatedAt = DateTimeOffset.UtcNow }; await WriteAsync(r, ct).ConfigureAwait(false); return r; }
    /// <summary>Starts the already-claimed fixed child while retaining the per-operation state lock.</summary>
    public async Task<Process?> StartClaimedChildAsync(GrantedTemplateScriptPlan plan, Func<Process> start, CancellationToken ct = default)
    {
        await using var stateLock = await AcquireStateLockAsync(plan.OperationId, ct).ConfigureAwait(false);
        var path = Path.Combine(_root, plan.OperationId + ".operation");
        var record = await ReadUnsafeAsync(path, ct).ConfigureAwait(false);
        var pending = record.PendingScript;
        if (record.State != TemplateOperationState.Running || record.InstanceName != plan.InstanceName || pending is null || pending.State != TemplatePendingScriptState.Claimed || pending.Ordinal != plan.ScriptOrdinal || pending.Type != plan.ScriptType || !string.Equals(pending.StagedFileSha256, plan.StagedFileSha256, StringComparison.OrdinalIgnoreCase)) throw Invalid("Template.ExecutionPlanInvalid");
        ValidateStagedPlan(plan);
        if (record.CancelRequested)
        {
            await WriteUnsafeAsync(path, record with { State=TemplateOperationState.Cancelled, PendingScript=null, CurrentScript=null, Message="Template.Cancelled", ErrorCode="Template.Cancelled", UpdatedAt=DateTimeOffset.UtcNow }, ct).ConfigureAwait(false);
            return null;
        }
        Process child;
        try { child = start(); }
        catch { await WriteUnsafeAsync(path, record with { State=TemplateOperationState.Failed, PendingScript=null, CurrentScript=null, Message="Template.Failed", ErrorCode="Template.Failed", UpdatedAt=DateTimeOffset.UtcNow }, ct).ConfigureAwait(false); throw; }
        pending = pending with { ChildProcessId=child.Id, ChildStartedAt=DateTimeOffset.UtcNow };
        await WriteUnsafeAsync(path, record with { PendingScript=pending, UpdatedAt=DateTimeOffset.UtcNow }, ct).ConfigureAwait(false);
        return child;
    }
    public async Task<bool> RequestCancelAsync(string id, CancellationToken ct = default)
    { var r = await ReadAsync(id, ct).ConfigureAwait(false); if (Terminal(r.State)) return false; await WriteAsync(r with { CancelRequested = true, UpdatedAt = DateTimeOffset.UtcNow }, ct).ConfigureAwait(false); return true; }
    /// <summary>Persists success only if cancellation did not win the final state-lock race.</summary>
    public async Task<bool> TryFinishSucceededAsync(string id, CancellationToken ct = default)
    {
        await using var stateLock = await AcquireStateLockAsync(id, ct).ConfigureAwait(false);
        var path = Path.Combine(_root, id + ".operation"); var record = await ReadUnsafeAsync(path, ct).ConfigureAwait(false);
        if (record.CancelRequested || Terminal(record.State)) return false;
        await WriteUnsafeAsync(path, record with { State=TemplateOperationState.Succeeded, Message="Succeeded", ErrorCode=null, CurrentScript=null, PendingScript=null, UpdatedAt=DateTimeOffset.UtcNow }, ct).ConfigureAwait(false);
        return true;
    }
    /// <summary>
    /// Starts the fixed worker and publishes its diagnostic identity under the same
    /// operation-state lock.  A queued cancellation wins before any worker is started.
    /// </summary>
    public async Task<Process?> StartWorkerAsync(string id, Func<Process> start, CancellationToken ct = default)
    {
        await using var stateLock = await AcquireStateLockAsync(id, ct).ConfigureAwait(false);
        var path = Path.Combine(_root, id + ".operation");
        var record = await ReadUnsafeAsync(path, ct).ConfigureAwait(false);
        if (Terminal(record.State)) return null;
        if (record.CancelRequested)
        {
            await WriteUnsafeAsync(path, record with
            {
                State = TemplateOperationState.Cancelled,
                Message = "Template.Cancelled",
                ErrorCode = "Template.Cancelled",
                UpdatedAt = DateTimeOffset.UtcNow
            }, ct).ConfigureAwait(false);
            return null;
        }

        try
        {
            var worker = start();
            await WriteUnsafeAsync(path, record with
            {
                WorkerPid = worker.Id,
                WorkerStartedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }, ct).ConfigureAwait(false);
            return worker;
        }
        catch
        {
            await WriteUnsafeAsync(path, record with
            {
                State = TemplateOperationState.Failed,
                Message = "Template.WorkerStartFailed",
                ErrorCode = "Template.WorkerStartFailed",
                UpdatedAt = DateTimeOffset.UtcNow
            }, ct).ConfigureAwait(false);
            return null;
        }
    }
    public FileStream? TryAcquireWorkerLock(string id) { Validate(id); try { return new FileStream(Path.Combine(_root, id + ".worker.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); } catch (IOException) { return null; } }
    public async Task<TemplateApplyOperationRecord> RecoverAsync(string id, CancellationToken ct = default)
    {
        var r = await ReadAsync(id, ct).ConfigureAwait(false); if (Terminal(r.State)) return r;
        using var held = TryAcquireWorkerLock(id); if (held is null) return r;
        var now = DateTimeOffset.UtcNow;
        // A bridge response may be observed before the worker process acquires its lock. A queued
        // operation is therefore authoritative until the bounded launch deadline actually passes.
        if (r.State == TemplateOperationState.Queued && r.WorkerLaunchDeadlineAt > now) return r;
        var state = r.State == TemplateOperationState.Queued ? TemplateOperationState.Failed : TemplateOperationState.Interrupted;
        var code = state == TemplateOperationState.Failed ? "Template.WorkerStartFailed" : "Template.WorkerInterrupted";
        await WriteAsync(r with { State = state, ErrorCode = code, Message = code, UpdatedAt = now }, ct).ConfigureAwait(false);
        return await ReadAsync(id, ct).ConfigureAwait(false);
    }
    public static bool Terminal(TemplateOperationState s) => s is TemplateOperationState.Succeeded or TemplateOperationState.Failed or TemplateOperationState.Cancelled or TemplateOperationState.Interrupted;
    private async Task<TemplateApplyOperationRecord> ReadUnsafeAsync(string path, CancellationToken ct)
    {
        try { var r = JsonSerializer.Deserialize<TemplateApplyOperationRecord>(ProtectedData.Unprotect(await ReadAllBytesSharedAsync(path, ct).ConfigureAwait(false), Entropy, DataProtectionScope.CurrentUser)) ?? throw Invalid("Template.OperationNotFound"); if (r.Sid != TemplateApplyGrantStore.CurrentSid()) throw Invalid("Template.OperationNotFound"); return r; }
        catch (CryptographicException) { throw Invalid("Template.OperationNotFound"); }
        catch (JsonException) { throw Invalid("Template.OperationNotFound"); }
    }
    /// <summary>
    /// Operation readers must not prevent the atomic replacement used by writers.  In particular,
    /// Windows requires the reader to opt into delete sharing before <see cref="File.Move"/> can
    /// replace the state file while a status request is in flight.
    /// </summary>
    private static async Task<byte[]> ReadAllBytesSharedAsync(string path, CancellationToken ct)
    {
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct).ConfigureAwait(false);
        return buffer.ToArray();
    }
    private async Task WriteUnsafeAsync(string target, TemplateApplyOperationRecord record, CancellationToken ct)
    {
        await WriteProtectedRecordAsync(target, record, ct).ConfigureAwait(false);
    }
    private async Task WriteProtectedRecordAsync(string target, TemplateApplyOperationRecord record, CancellationToken ct)
    {
        var tmp = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(tmp, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(record), Entropy, DataProtectionScope.CurrentUser), ct).ConfigureAwait(false);
            await ReplaceAtomicallyWithRetryAsync(tmp, target, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
    private async Task ReplaceAtomicallyWithRetryAsync(string source, string destination, CancellationToken ct)
    {
        const int maxAttempts = 6;
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                _replaceFile(source, destination, true);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts - 1 && IsTransientSharingFailure(ex, destination))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (1 << attempt)), ct).ConfigureAwait(false);
            }
        }
    }
    internal static bool IsTransientSharingFailure(Exception exception, string destination)
    {
        const int sharingViolation = 32;
        const int lockViolation = 33;
        var win32Error = exception.HResult & 0xffff;
        return OperatingSystem.IsWindows()
            && exception is IOException
            && win32Error is sharingViolation or lockViolation;
    }
    private async Task<FileStream> AcquireStateLockAsync(string id, CancellationToken ct) { var path=Path.Combine(_root,id+".state.lock"); while(true) { ct.ThrowIfCancellationRequested(); try { return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose); } catch(IOException) { await Task.Delay(5,ct).ConfigureAwait(false); } } }
    private static void Validate(string id) { if (id.Length != 64 || id.Any(x => !Uri.IsHexDigit(x))) throw Invalid("Template.OperationNotFound"); }
    private void ValidateStagedPlan(GrantedTemplateScriptPlan plan)
    {
        if (_stagingRoot is null) return;
        var root = Path.Combine(_stagingRoot, plan.OperationId) + Path.DirectorySeparatorChar;
        var staged = Path.GetFullPath(plan.CoreStagedFile);
        if (!staged.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(staged) || !string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(staged))).ToLowerInvariant(), plan.StagedFileSha256, StringComparison.OrdinalIgnoreCase)) throw Invalid("Template.ExecutionPlanInvalid");
    }
    private static InvalidOperationException Invalid(string code) => new(code);
}

public sealed record TemplateApplyOperationRecord(int SchemaVersion, string OperationId, string Sid, string InstanceName,
    string TemplateId, string TemplateVersion, string SourceUrl, string ManifestDigest, string ArtifactSha256, string ExecutableFilesDigest, string VariablesDigest, bool RecoveryDeclined, TemplateOperationState State,
    DateTimeOffset CreatedAt, DateTimeOffset WorkerLaunchDeadlineAt, DateTimeOffset UpdatedAt, int CompletedScripts,
    int TotalScripts, string? CurrentScript, string Message, string? ErrorCode, IReadOnlyList<string> ExecutedScripts,
    bool CancelRequested, TemplatePendingScriptRecord? PendingScript = null, int? WorkerPid = null, DateTimeOffset? WorkerStartedAt = null,
    string NormalizedVariables = "", string ArtifactRootDigest = "", string CapabilitiesDigest = "", string RecoveryFingerprint = "", string? MarketplacePromotionErrorCode = null);
public enum TemplatePendingScriptState { Prepared, Claimed }
public sealed record TemplatePendingScriptRecord(int Ordinal, TemplateScriptType Type, string StagedFileSha256, TemplatePendingScriptState State, string AttemptId, DateTimeOffset PreparedAt, DateTimeOffset? ClaimedAt, int? ChildProcessId, DateTimeOffset? ChildStartedAt);
