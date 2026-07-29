using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Issues grants and executes the five reviewed lifecycle paths through a closed Core runtime.</summary>
public sealed class LifecyclePathOperationService : ILifecyclePathOperationService
{
    private readonly ILifecyclePathRuntime _runtime; private readonly ILifecycleMetadataCleanup? _cleanup; private readonly LifecyclePathResolver _paths; private readonly LifecycleGrantStore _grants; private readonly TimeProvider _clock; private readonly string _recoveryRoot; private readonly Func<string> _sid;
    public LifecyclePathOperationService(ILifecyclePathRuntime runtime, string root, ILifecycleMetadataCleanup? cleanup = null, LifecyclePathResolver? paths = null, TimeProvider? clock = null, Func<string>? sid = null) { _runtime = runtime; _cleanup = cleanup; _paths = paths ?? new(); _clock = clock ?? TimeProvider.System; _sid = sid ?? CurrentSid; _grants = new LifecycleGrantStore(root, _clock, _sid); _recoveryRoot = Path.Combine(root, "lifecycle-recovery"); }
    public Task<LifecycleOperationPreview> PreviewRemoveAsync(string name, bool keepFiles, CancellationToken ct = default) => PreviewAsync(LifecyclePathOperation.Remove, name, null, keepFiles, false, null, null, ct);
    public Task<LifecycleOperationPreview> PreviewMoveAsync(string name, string destination, CancellationToken ct = default) => PreviewAsync(LifecyclePathOperation.Move, name, null, false, false, null, _paths.ResolveDestinationRoot(destination, name), ct);
    public Task<LifecycleOperationPreview> PreviewRenameAsync(string name, string newName, CancellationToken ct = default) => PreviewAsync(LifecyclePathOperation.Rename, name, LifecyclePathResolver.ValidateInstanceName(newName), false, false, null, null, ct);
    public Task<LifecycleOperationPreview> PreviewExportAsync(string name, string destination, bool stopRunning, CancellationToken ct = default) => PreviewAsync(LifecyclePathOperation.Export, name, null, false, stopRunning, null, _paths.ResolveArchiveDestination(destination), ct);
    public Task<LifecycleOperationPreview> PreviewImportAsync(string name, string source, string installPath, CancellationToken ct = default) => PreviewAsync(LifecyclePathOperation.Import, name, null, false, false, _paths.ResolveImportSource(source), _paths.ResolveDestinationRoot(installPath, name), ct);
    private async Task<LifecycleOperationPreview> PreviewAsync(LifecyclePathOperation op, string name, string? newName, bool keepFiles, bool stopRunning, string? source, string? target, CancellationToken ct)
    {
        if (op == LifecyclePathOperation.Remove && keepFiles) throw new InvalidOperationException("Lifecycle.KeepFilesUnavailable");
        name = LifecyclePathResolver.ValidateInstanceName(name); var listed = await _runtime.GetInstancesAsync(ct); var present = listed.SingleOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (op == LifecyclePathOperation.Import ? present is not null : present is null) throw new InvalidOperationException("Lifecycle.InstanceStateChanged");
        if (target is not null) Reserve(target); var state = present?.State ?? "new"; var fingerprint = Fingerprint(op, name, newName, state, source, target);
        var expires = _clock.GetUtcNow().AddMinutes(2); var grant = new LifecycleOperationGrant(_sid(), op, name, newName, keepFiles, stopRunning, source, target, fingerprint, expires, target is null ? null : ReservationId(target));
        var issued = await _grants.IssueAsync(grant, ct); return new(issued.Token, op, name, issued.ExpiresAt);
    }
    public async Task<LifecycleOperationResult> ExecuteAsync(string previewToken, CancellationToken ct = default)
    {
        var grant = await _grants.ConsumeAsync(previewToken, ct); var listed = await _runtime.GetInstancesAsync(ct); var current = listed.SingleOrDefault(x => string.Equals(x.Name, grant.InstanceName, StringComparison.OrdinalIgnoreCase));
        if (grant.Operation == LifecyclePathOperation.Import ? current is not null : current is null) { Release(grant.Target); return Failure(grant, "Lifecycle.StateChanged"); }
        var state = current?.State ?? "new"; if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(grant.Fingerprint), Convert.FromHexString(Fingerprint(grant.Operation, grant.InstanceName, grant.NewName, state, grant.Source, grant.Target)))) { Release(grant.Target); return Failure(grant, "Lifecycle.StateChanged"); }
        try
        {
            if (grant.Source is not null) _paths.Revalidate(grant.Source, true); if (grant.Target is not null) { _paths.Revalidate(Path.GetDirectoryName(grant.Target)!, true); if (grant.Operation is LifecyclePathOperation.Move or LifecyclePathOperation.Import) { if (!Directory.Exists(grant.Target)) Directory.CreateDirectory(grant.Target); _paths.Revalidate(grant.Target, true, true); } }
            var recovery = await JournalAsync(grant, "Prepared", ct);
            switch (grant.Operation) { case LifecyclePathOperation.Remove: await _runtime.RemoveAsync(grant.InstanceName, grant.KeepFiles, ct); if (_cleanup is not null) await _cleanup.CleanupRemovedInstanceAsync(grant.InstanceName, ct); break; case LifecyclePathOperation.Move: await _runtime.MoveAsync(grant.InstanceName, grant.Target!, ct); break; case LifecyclePathOperation.Rename: await _runtime.RenameAsync(grant.InstanceName, grant.NewName!, ct); break; case LifecyclePathOperation.Export: await _runtime.ExportAsync(grant.InstanceName, grant.Target!, grant.StopRunning, ct); break; case LifecyclePathOperation.Import: await _runtime.ImportAsync(grant.InstanceName, grant.Source!, grant.Target!, ct); break; }
            await JournalAsync(grant, "Committed", ct, recovery.Id); return new(true, grant.Operation, grant.NewName ?? grant.InstanceName, "Lifecycle.Succeeded");
        }
        catch (OperationCanceledException) { var r = await JournalAsync(grant, "Cancelled", CancellationToken.None); return new(false, grant.Operation, grant.InstanceName, grant.Operation == LifecyclePathOperation.Export ? "Lifecycle.ExportCompletedAfterCancellation" : "Lifecycle.Cancelled", LifecycleRecoveryAction.ManualRecoveryRequired, r.Id); }
        catch (InvalidOperationException ex) when (ex.Message == "Lifecycle.RollbackRestored") { var r = await JournalAsync(grant, "RollbackRestored", CancellationToken.None); return new(false, grant.Operation, grant.InstanceName, "Lifecycle.RollbackRestored", LifecycleRecoveryAction.None, r.Id); }
        catch { var r = await JournalAsync(grant, "RecoveryRequired", CancellationToken.None); return new(false, grant.Operation, grant.InstanceName, "Lifecycle.RollbackFailed", LifecycleRecoveryAction.ManualRecoveryRequired, r.Id); }
        finally { Release(grant.Target); }
    }
    private async Task<LifecycleRecoveryRecord> JournalAsync(LifecycleOperationGrant g, string checkpoint, CancellationToken ct, string? id = null) { Directory.CreateDirectory(_recoveryRoot); var r = new LifecycleRecoveryRecord(id ?? Guid.NewGuid().ToString("N"), g.Operation, g.InstanceName, checkpoint, _clock.GetUtcNow(), "Lifecycle." + checkpoint); await File.WriteAllTextAsync(Path.Combine(_recoveryRoot, r.Id + ".json"), JsonSerializer.Serialize(r), ct); return r; }
    private void Reserve(string target) { var p = target + ".distronexus-reservation"; using var f = new FileStream(p, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
    private static void Release(string? target) { if (target is null) return; try { File.Delete(target + ".distronexus-reservation"); } catch (IOException) { } }
    private static string ReservationId(string target) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target)));
    private static string Fingerprint(LifecyclePathOperation op, string name, string? newer, string state, string? source, string? target) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{op}|{name}|{newer}|{state}|{source}|{target}")));
    private static string CurrentSid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Lifecycle.GrantInvalid");
    private static LifecycleOperationResult Failure(LifecycleOperationGrant g, string code) => new(false, g.Operation, g.InstanceName, code);
}
