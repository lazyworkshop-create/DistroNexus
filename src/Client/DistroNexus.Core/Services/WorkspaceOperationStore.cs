using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace DistroNexus.Core.Services;

/// <summary>Durable, same-user operation state shared by the short-lived bridge and worker.</summary>
public sealed class WorkspaceOperationStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DistroNexus.WorkspaceOperation.v1");
    private readonly string _root;
    public WorkspaceOperationStore(string root) { _root = root; Directory.CreateDirectory(root); }
    public async Task CreateAsync(WorkspaceOperationRecord record, CancellationToken ct = default) => await WriteAsync(record, ct);
    public async Task<WorkspaceOperationRecord> ReadAsync(string operationId, CancellationToken ct = default)
    {
        ValidateId(operationId); var path = Path.Combine(_root, operationId + ".operation");
        if (!File.Exists(path)) throw new InvalidOperationException("Workspace.OperationNotFound");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        try
        {
            var record = JsonSerializer.Deserialize<WorkspaceOperationRecord>(Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser))) ?? throw new InvalidOperationException("Workspace.OperationNotFound");
            if (record.Sid != Sid()) throw new InvalidOperationException("Workspace.OperationNotFound");
            return record;
        }
        catch (CryptographicException) { throw new InvalidOperationException("Workspace.OperationNotFound"); }
    }
    public async Task WriteAsync(WorkspaceOperationRecord record, CancellationToken ct = default)
    {
        ValidateId(record.OperationId); if (record.Sid != Sid()) throw new InvalidOperationException("Workspace.OperationNotFound");
        await using var stateLock = await AcquireStateLockAsync(record.OperationId, ct);
        var target = Path.Combine(_root, record.OperationId + ".operation"); var temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
        // A cancel can arrive while an action is executing. Preserve it when the worker publishes
        // progress or terminal state so the older worker snapshot can never clear cancellation.
        if (File.Exists(target))
        {
            var current = await ReadUnsafeAsync(target, ct);
            if (current.IsTerminal) record = current with { CancellationRequested = current.CancellationRequested || record.CancellationRequested };
            else if (current.CancellationRequested) record = record with { CancellationRequested = true };
        }
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record)), Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(temporary, bytes, ct); File.Move(temporary, target, true);
    }
    public async Task<bool> RequestCancelAsync(string operationId, CancellationToken ct = default)
    {
        var record = await ReadAsync(operationId, ct); if (record.IsTerminal) return false;
        await WriteAsync(record with { CancellationRequested = true }, ct); return true;
    }
    public FileStream? TryAcquireWorkerLock(string operationId)
    {
        ValidateId(operationId);
        try { return new FileStream(Path.Combine(_root, operationId + ".lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
        catch (IOException) { return null; }
    }
    public bool HasWorkerLock(string operationId) => File.Exists(Path.Combine(_root, operationId + ".lock"));
    public async Task<WorkspaceOperationRecord> RecoverAsync(string operationId, CancellationToken ct = default)
    {
        var record = await ReadAsync(operationId, ct);
        if (!record.IsTerminal && !HasWorkerLock(operationId)) { record = record with { IsTerminal = true, Outcome = "Failed", ErrorCode = "Workspace.WorkerInterrupted" }; await WriteAsync(record, ct); }
        return record;
    }
    public static string CurrentSid() => Sid();
    private static string Sid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Workspace.OperationNotFound");
    private async Task<FileStream> AcquireStateLockAsync(string operationId, CancellationToken ct)
    {
        var path = Path.Combine(_root, operationId + ".state.lock");
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (IOException) { await Task.Delay(5, ct); }
        }
    }
    private async Task<WorkspaceOperationRecord> ReadUnsafeAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return JsonSerializer.Deserialize<WorkspaceOperationRecord>(Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser))) ?? throw new InvalidOperationException("Workspace.OperationNotFound");
    }
    private static void ValidateId(string id) { if (id.Length != 64 || id.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Workspace.OperationNotFound"); }
}
public sealed record WorkspaceOperationRecord(string OperationId, string Sid, string Kind, Guid WorkspaceId, Guid? ActionId, long Revision, IReadOnlyList<Models.WorkspaceActionResult> Progress, bool IsTerminal, bool CancellationRequested, string? Outcome = null, string? ErrorCode = null, Models.WorkspaceLaunchResult? Result = null);
