using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Exceptions;

namespace DistroNexus.Core.Services;

public sealed class RecoveryPointService : IRecoveryPointService
{
    private const int SchemaVersion = 1;
    private readonly IRecoveryPointRuntime _runtime;
    private readonly IBackupService? _backups;
    private readonly string _root;
    private string CatalogPath => Path.Combine(_root, "recovery-points.json");
    private string RetentionPath => Path.Combine(_root, "retention.json");
    private string StatePath => Path.Combine(_root, "recovery-state.json");
    private string StateLockPath => Path.Combine(_root, "recovery-state.lock");
    private string OperationsRoot => Path.Combine(_root, "operations");
    private readonly ConcurrentDictionary<string, RecoveryOperationPreview> _previews = new(StringComparer.Ordinal);

    public RecoveryPointService(IRecoveryPointRuntime runtime, IBackupService? backups = null, string? root = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _backups = backups;
        _root = Path.GetFullPath(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus", "RecoveryPoints"));
    }

    public async Task<IReadOnlyList<RecoveryPointSummary>> ListAsync(CancellationToken ct = default)
    {
        await ReconcileOwnedOperationsAsync(ct);
        if (!Directory.Exists(_root)) return [];
        var result = new List<RecoveryPointSummary>();
        foreach (var directory in (await ReadStateAsync(ct)).Catalog)
        {
            ct.ThrowIfCancellationRequested();
            var manifest = await ReadManifestAsync(directory, ct);
            if (manifest is not null && IsOwnedPointDirectory(directory, manifest)) result.Add(new(manifest, directory, await GetVerificationAsync(manifest, directory, ct)));
        }
        return result.OrderByDescending(x => x.Manifest.CreatedAt).ToArray();
    }

    public async Task<RecoveryOperationPreview> PreviewCreateAsync(RecoveryPointCreateRequest request, CancellationToken ct = default)
    {
        ValidateCreate(request);
        var source = await _runtime.GetSourceAsync(request.SourceInstance, ct);
        if (request.Format == RecoveryPointFormat.Vhdx && (source.WslVersion != 2 || !source.SupportsVhdExport))
            throw new InvalidOperationException("VHDX recovery points require supported WSL 2 VHD export.");
        var warnings = new List<string>();
        if (source.IsRunning) warnings.Add("A running export may not be application-consistent; stop the instance for a consistent export.");
        if (request.Format == RecoveryPointFormat.Vhdx) warnings.Add("VHDX recovery points may be large and require capability-supported restore.");
        var canonical = CanonicalCreate(request);
        var preview = new RecoveryOperationPreview(Guid.NewGuid().ToString("N"), "Create", null, canonical.SourceInstance, "", canonical.DestinationRoot, canonical.Format,
            source.IsRunning, canonical.Format == RecoveryPointFormat.Vhdx, warnings, source.EstimatedBytes + source.EstimatedBytes / 10, Fingerprint(canonical));
        _previews[preview.Token] = preview;
        return preview;
    }

    public async Task<RecoveryPointSummary> CreateAsync(RecoveryPointCreateRequest request, string previewToken, CancellationToken ct = default, IProgress<RecoveryOperationProgress>? progress = null)
    {
        var preview = Consume(previewToken, "Create");
        request = CanonicalCreate(request);
        if (!StringComparer.Ordinal.Equals(preview.RequestFingerprint, Fingerprint(request)))
            throw new InvalidOperationException("Recovery point request no longer matches its preview.");
        Directory.CreateDirectory(_root); Directory.CreateDirectory(request.DestinationRoot);
        progress?.Report(new("Create", "Validating", 5));
        var source = await _runtime.GetSourceAsync(request.SourceInstance, ct);
        if (request.Format == RecoveryPointFormat.Vhdx && (source.WslVersion != 2 || !source.SupportsVhdExport))
            throw new InvalidOperationException("VHDX recovery points require supported WSL 2 VHD export.");
        var required = RequiredBytes(source.EstimatedBytes);
        if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(request.DestinationRoot))!).AvailableFreeSpace < required)
            throw new IOException("Destination does not have the estimated free space required for this recovery point.");
        var id = Guid.NewGuid(); var directory = Path.Combine(Path.GetFullPath(request.DestinationRoot), $"DistroNexusRecovery-{id:N}"); Directory.CreateDirectory(directory);
        var payloadName = request.Format == RecoveryPointFormat.Tar ? "instance.tar" : "instance.vhdx";
        var partial = Path.Combine(directory, payloadName + ".partial"); var payload = Path.Combine(directory, payloadName);
        try
        {
            var stoppedForExport = false;
            if (source.IsRunning && request.RestartAfterExport) { await _runtime.StopAsync(request.SourceInstance, ct); stoppedForExport = true; }
            progress?.Report(new("Create", "Exporting", 20));
            try { await _runtime.ExportAsync(request.SourceInstance, partial, request.Format, ct); }
            finally { if (stoppedForExport) await _runtime.StartAsync(request.SourceInstance, ct); }
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(partial)) throw new IOException("Recovery export did not produce its operation-owned payload.");
            File.Move(partial, payload);
            var size = new FileInfo(payload).Length;
            progress?.Report(new("Create", "Verifying", 80));
            var hash = await HashAsync(payload, ct);
            var manifest = new RecoveryPointManifest(SchemaVersion, id, request.Name.Trim(), request.SourceInstance.Trim(), source.WslVersion, request.Format,
                DateTimeOffset.UtcNow, payloadName, size, hash, "2.3.0", Normalize(request.Tags), request.Description?.Trim() ?? "");
            await WriteManifestAsync(directory, manifest, ct);
            await AddToCatalogAsync(directory, ct);
            progress?.Report(new("Create", "Completed", 100));
            return new(manifest, directory, RecoveryPointVerification.Verified);
        }
        catch
        {
            // Cancellation must not prevent compensation for a partial export.
            RecoveryPathSafety.DeleteOwnedCreateDirectory(directory, id); throw;
        }
    }

    public async Task<RecoveryOperationPreview> PreviewRestoreAsync(RecoveryRestoreRequest request, CancellationToken ct = default)
    {
        var item = await FindAsync(request.RecoveryPointId, ct) ?? throw new KeyNotFoundException("Recovery point was not found.");
        ValidateTarget(request);
        if (await _runtime.InstanceExistsAsync(request.TargetInstance, ct)
            || (!request.ImportInPlace && Directory.Exists(request.TargetDirectory)))
            throw new InvalidOperationException("Restore never overwrites an existing instance or target directory.");
        await EnsureRestoreCapabilitiesAsync(item.Manifest, request.ImportInPlace, ct);
        request = CanonicalRestore(request);
        var preview = new RecoveryOperationPreview(Guid.NewGuid().ToString("N"), "Restore", item.Manifest.Id, item.Manifest.SourceInstance, request.TargetInstance, request.TargetDirectory, item.Manifest.Format,
            false, request.ImportInPlace, ["Restore creates a distinct instance and does not replace the source."], item.Manifest.SizeBytes, Fingerprint(request));
        _previews[preview.Token] = preview; return preview;
    }

    public async Task RestoreAsync(RecoveryRestoreRequest request, string previewToken, CancellationToken ct = default, IProgress<RecoveryOperationProgress>? progress = null)
    {
        var preview = Consume(previewToken, "Restore");
        request = CanonicalRestore(request);
        if (!StringComparer.Ordinal.Equals(preview.RequestFingerprint, Fingerprint(request))) throw new InvalidOperationException("Restore request no longer matches its preview.");
        var item = await FindAsync(request.RecoveryPointId, ct) ?? throw new KeyNotFoundException("Recovery point was not found.");
        if (!IsOwnedPointDirectory(item.DirectoryPath, item.Manifest)) throw new InvalidOperationException("Recovery point ownership could not be verified.");
        await EnsureRestoreCapabilitiesAsync(item.Manifest, request.ImportInPlace, ct);
        if (request.VerifyChecksum && await GetVerificationAsync(item.Manifest, item.DirectoryPath, ct) != RecoveryPointVerification.Verified) throw new InvalidOperationException("Recovery point checksum verification failed; restore is blocked.");
        progress?.Report(new("Restore", "ReservingTarget", 5));
        if (await _runtime.InstanceExistsAsync(request.TargetInstance, ct)) throw new WslOperationFailedException("Restore target instance already exists.", DistroNexusErrorCode.InstanceAlreadyExists, "Restore", request.TargetInstance);
        var operationId = Guid.NewGuid().ToString("N");
        var journal = Path.Combine(OperationsRoot, operationId + ".json");
        var marker = request.ImportInPlace
            ? Path.Combine(OperationsRoot, $"import-in-place-{operationId}.marker.json")
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(request.TargetDirectory))!, $".distronexus-recovery-{operationId}.json");
        var record = new RecoveryOperationMarker(operationId, request.TargetInstance,
            request.ImportInPlace ? "" : Path.GetFullPath(request.TargetDirectory), "Importing");
        var removeEvidence = false;
        Directory.CreateDirectory(OperationsRoot);
        await using var reservation = request.ImportInPlace
            ? ReserveImportInPlaceTarget(request.TargetInstance, operationId)
            : ReserveTarget(request.TargetDirectory, request.TargetInstance, operationId);
        await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(record), ct);
        await File.WriteAllTextAsync(journal, JsonSerializer.Serialize(record), ct);
        try
        {
            progress?.Report(new("Restore", "Importing", 25));
            await _runtime.ImportAsync(operationId, request.TargetInstance, Path.Combine(item.DirectoryPath, item.Manifest.PayloadFile), request.TargetDirectory, item.Manifest.Format, request.ImportInPlace, ct);
            var registration = await _runtime.GetRegistrationAsync(request.TargetInstance, ct);
            if (registration is null) throw new IOException("Import registration ownership could not be established.");
            if (!request.ImportInPlace && !PathEquals(registration.BasePath, request.TargetDirectory))
                throw new IOException("Imported registration base path does not match the reserved target directory.");
            record = record with { State = "Imported", Registration = registration };
            await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(record), ct);
            await File.WriteAllTextAsync(journal, JsonSerializer.Serialize(record), ct);
            if (!await _runtime.VerifyBootAsync(request.TargetInstance, ct)) throw new IOException("Imported recovery point did not pass its boot verification.");
            progress?.Report(new("Restore", "Verifying", 85));
            await File.WriteAllTextAsync(journal, JsonSerializer.Serialize(record with { State = "Completed" }), ct);
            progress?.Report(new("Restore", "Completed", 100));
            removeEvidence = true;
        }
        catch (OperationCanceledException)
        {
            record = record with { State = "ManualRecoveryRequired", Failure = "The import was cancelled; its final WSL registration state is unknown.", ManualRecoveryInstructions = ManualRecoveryInstructions() };
            await WriteManualRecoveryEvidenceAsync(marker, journal, record);
            throw;
        }
        catch (Exception ex)
        {
            // WSL has no compare-and-unregister operation. A registration can change after
            // any observation, so automatic unregister would risk removing an external distro.
            // Keep both operation-owned records for explicit, evidence-based recovery.
            record = record with { State = "ManualRecoveryRequired", Failure = ex.Message, ManualRecoveryInstructions = ManualRecoveryInstructions() };
            await WriteManualRecoveryEvidenceAsync(marker, journal, record);
            throw new WslOperationFailedException(
                "Recovery import may have left a registered instance. Automatic cleanup is disabled. Review the retained operation journal, compare the target with the current WSL registration, and only then perform any manual recovery action.",
                ex,
                DistroNexusErrorCode.RecoveryManualRecoveryRequired,
                "RestoreManualRecovery",
                request.TargetInstance);
        }
        finally
        {
            if (removeEvidence && !request.ImportInPlace) RecoveryPathSafety.DeleteOperationEvidence(operationId, request.TargetInstance, request.TargetDirectory, journal);
        }
    }

    public async Task<RecoveryOperationPreview> PreviewCloneAsync(RecoveryCloneRequest request, CancellationToken ct = default)
    {
        var create = await PreviewCreateAsync(request.Snapshot, ct);
        var source = await _runtime.GetSourceAsync(request.Snapshot.SourceInstance, ct);
        if (await _runtime.InstanceExistsAsync(request.TargetInstance, ct) || Directory.Exists(request.TargetDirectory)) throw new InvalidOperationException("Clone never overwrites an existing instance or target directory.");
        // A clone performs an import immediately after its export.  Validate every requested
        // VHDX/import feature before an export directory or recovery-point manifest exists.
        if (request.Snapshot.Format == RecoveryPointFormat.Vhdx && request.ImportInPlace && !source.SupportsImportInPlace)
            throw new InvalidOperationException("VHDX import-in-place cloning requires the supported WSL import capability.");
        request = CanonicalClone(request);
        var preview = new RecoveryOperationPreview(Guid.NewGuid().ToString("N"), "Clone", null, request.Snapshot.SourceInstance, request.TargetInstance, request.TargetDirectory, request.Snapshot.Format, create.RequiresStop, request.ImportInPlace, create.Warnings, create.EstimatedBytes, Fingerprint(request));
        _previews.TryRemove(create.Token, out _); _previews[preview.Token] = preview; return preview;
    }
    public async Task RestoreCloneAsync(RecoveryCloneRequest request, string previewToken, CancellationToken ct = default, IProgress<RecoveryOperationProgress>? progress = null)
    {
        var preview = Consume(previewToken, "Clone");
        request = CanonicalClone(request);
        if (!StringComparer.Ordinal.Equals(preview.RequestFingerprint, Fingerprint(request))) throw new InvalidOperationException("Clone request no longer matches its preview.");
        // Clone is deliberately expressed as portable export plus distinct-instance restore.
        var createPreview = await PreviewCreateAsync(request.Snapshot, ct);
        progress?.Report(new("Clone", "CreatingPayload", 5));
        var point = await CreateAsync(request.Snapshot, createPreview.Token, ct, progress);
        var restore = new RecoveryRestoreRequest(point.Manifest.Id, request.TargetInstance, request.TargetDirectory, true, request.ImportInPlace);
        var restorePreview = await PreviewRestoreAsync(restore, ct);
        await RestoreAsync(restore, restorePreview.Token, ct, progress);
        // A successful clone retains its explicit portable point. In particular an
        // import-in-place clone is backed by that VHDX and must never delete it implicitly.
    }

    public async Task<RecoveryPointVerification> VerifyAsync(Guid id, CancellationToken ct = default) { var item = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Recovery point was not found."); return await GetVerificationAsync(item.Manifest, item.DirectoryPath, ct); }
    public async Task UpdateNotesAsync(Guid id, string description, IReadOnlyList<string> tags, bool pinned, CancellationToken ct = default) { var item = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Recovery point was not found."); EnsureOwnedPoint(item); await WriteManifestAsync(item.DirectoryPath, item.Manifest with { Description = description?.Trim() ?? "", Tags = Normalize(tags), Pinned = pinned }, ct); }
    public async Task<RecoveryOperationPreview> PreviewDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Recovery point was not found.");
        EnsureOwnedPoint(item);
        var preview = new RecoveryOperationPreview(Guid.NewGuid().ToString("N"), "Delete", item.Manifest.Id, item.Manifest.SourceInstance,
            "", item.DirectoryPath, item.Manifest.Format, false, false,
            ["Deletion permanently removes this recovery point and its payload."], item.Manifest.SizeBytes, FingerprintDelete(item));
        _previews[preview.Token] = preview;
        return preview;
    }
    public async Task DeleteAsync(Guid id, string previewToken, CancellationToken ct = default)
    {
        var preview = Consume(previewToken, "Delete");
        if (preview.RecoveryPointId != id) throw new InvalidOperationException("Recovery point no longer matches its deletion preview.");
        var item = await FindAsync(id, ct) ?? throw new KeyNotFoundException("Recovery point was not found.");
        EnsureOwnedPoint(item);
        if (!StringComparer.Ordinal.Equals(preview.RequestFingerprint, FingerprintDelete(item)))
            throw new InvalidOperationException("Recovery point changed after its deletion preview; generate a new preview.");
        RecoveryPathSafety.DeleteOwnedPoint(item);
        await RemoveFromCatalogAsync(item.DirectoryPath, ct);
    }
    public async Task ApplyRetentionAsync(string sourceInstance, int maximum, CancellationToken ct = default)
    {
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        await MutateStateAsync(state =>
        {
            var retention = new Dictionary<string, int>(state.Retention ?? new(), StringComparer.OrdinalIgnoreCase) { [sourceInstance.Trim()] = maximum };
            return state with { Retention = retention };
        }, ct);
        var all = await ListAsync(ct);
        var candidates = all.Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.Manifest.SourceInstance, sourceInstance) && !x.Manifest.Pinned).OrderByDescending(x => x.Manifest.CreatedAt).ToArray();
        var remaining = all.Count;
        foreach (var item in candidates.Skip(maximum)) { if (remaining <= 1) break; EnsureOwnedPoint(item); RecoveryPathSafety.DeleteOwnedPoint(item); await RemoveFromCatalogAsync(item.DirectoryPath, ct); remaining--; }
    }
    public async Task<int?> GetRetentionAsync(string sourceInstance, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceInstance)) return null;
        var retention = (await ReadStateAsync(ct)).Retention;
        return retention.TryGetValue(sourceInstance.Trim(), out var value) && value > 0 ? value : null;
    }
    public async Task<IReadOnlyList<RecoveryHistoryEntry>> GetHistoryAsync(CancellationToken ct = default)
    {
        var history = (await ListAsync(ct)).Select(x => new RecoveryHistoryEntry(x.Manifest.Id.ToString(), x.Manifest.SourceInstance, x.Manifest.CreatedAt, "RecoveryPoint", x.Verification.ToString(), x.DirectoryPath, x.Manifest.Id)).ToList();
        if (_backups is not null)
        {
            foreach (var record in await _backups.GetHealthHistoryAsync(ct))
            {
                var kind = string.IsNullOrWhiteSpace(record.Kind) ? "Backup" : record.Kind;
                history.Add(new RecoveryHistoryEntry($"backup:{record.CompletedAt.UtcTicks}:{record.InstanceName}:{kind}", record.InstanceName,
                    record.CompletedAt, kind, record.Succeeded ? "Completed" : "Failed", record.Destination));
                if (!record.Succeeded || string.IsNullOrWhiteSpace(record.Destination) || !Directory.Exists(record.Destination)) continue;
                foreach (var artifact in EnumerateBackupArtifacts(record.Destination))
                    history.Add(new RecoveryHistoryEntry($"artifact:{artifact.FullName}", record.InstanceName, new DateTimeOffset(artifact.CreationTimeUtc), "BackupArtifact", "Completed", artifact.FullName));
            }
        }
        return history.OrderByDescending(x => x.CreatedAt).ToArray();
    }

    private async Task<RecoveryPointSummary?> FindAsync(Guid id, CancellationToken ct) => (await ListAsync(ct)).FirstOrDefault(x => x.Manifest.Id == id);
    private async Task EnsureRestoreCapabilitiesAsync(RecoveryPointManifest manifest, bool importInPlace, CancellationToken ct)
    {
        if (manifest.Format != RecoveryPointFormat.Vhdx)
        {
            if (importInPlace) throw new InvalidOperationException("Import-in-place requires a supported VHDX recovery point.");
            return;
        }

        var source = await _runtime.GetSourceAsync(manifest.SourceInstance, ct);
        // Both --import --vhd and --import-in-place are capability-dependent.  Do not
        // present a standard VHDX import as safe merely because import-in-place is off.
        if (source.WslVersion != 2 || !source.SupportsVhdExport)
            throw new InvalidOperationException("VHDX recovery point restore requires supported WSL 2 VHD import capability.");
        if (importInPlace && !source.SupportsImportInPlace)
            throw new InvalidOperationException("Import-in-place requires a supported VHDX recovery point.");
    }
    private RecoveryOperationPreview Consume(string token, string operation) => _previews.TryRemove(token ?? "", out var value) && value.Operation == operation ? value : throw new InvalidOperationException("A current explicit operation preview is required.");
    private static string FingerprintDelete(RecoveryPointSummary item) => Fingerprint(new { item.Manifest.Id, item.DirectoryPath, item.Manifest.Sha256, item.Manifest.SizeBytes, item.Manifest.Pinned, item.Manifest.Description, Tags = item.Manifest.Tags.OrderBy(x => x, StringComparer.Ordinal) });
    private static async Task<RecoveryPointVerification> GetVerificationAsync(RecoveryPointManifest m, string directory, CancellationToken ct) { var payload = Path.GetFullPath(Path.Combine(directory, m.PayloadFile)); if (!payload.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(payload)) return RecoveryPointVerification.Missing; if (new FileInfo(payload).Length != m.SizeBytes) return RecoveryPointVerification.Corrupt; return StringComparer.Ordinal.Equals(await HashAsync(payload, ct), m.Sha256) ? RecoveryPointVerification.Verified : RecoveryPointVerification.Corrupt; }
    private static async Task<RecoveryPointManifest?> ReadManifestAsync(string directory, CancellationToken ct)
    {
        var path = Path.Combine(directory, "manifest.json");
        if (!File.Exists(path)) return null;
        try
        {
            var m = JsonSerializer.Deserialize<RecoveryPointManifest>(await File.ReadAllTextAsync(path, ct));
            return m is not null && m.SchemaVersion == SchemaVersion && m.Id != Guid.Empty
                && !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.SourceInstance)
                && m.SizeBytes >= 0 && IsFileName(m.PayloadFile) && m.Sha256?.Length == 64 ? m : null;
        }
        catch (JsonException) { return null; }
    }
    private static Task WriteManifestAsync(string directory, RecoveryPointManifest manifest, CancellationToken ct) { var path = Path.Combine(directory, "manifest.json"); var temp = path + ".partial"; return WriteAsync(); async Task WriteAsync() { await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct); File.Move(temp, path, true); } }
    private static async Task<string> HashAsync(string path, CancellationToken ct) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant(); }
    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? tags) => (tags ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static RecoveryPointCreateRequest CanonicalCreate(RecoveryPointCreateRequest request) => request with
    {
        SourceInstance = request.SourceInstance?.Trim() ?? "",
        Name = request.Name?.Trim() ?? "",
        DestinationRoot = string.IsNullOrWhiteSpace(request.DestinationRoot) ? "" : Path.GetFullPath(request.DestinationRoot),
        Description = request.Description?.Trim() ?? "",
        Tags = Normalize(request.Tags)
    };
    private static RecoveryRestoreRequest CanonicalRestore(RecoveryRestoreRequest request) => request with
    {
        TargetInstance = request.TargetInstance?.Trim() ?? "",
        TargetDirectory = string.IsNullOrWhiteSpace(request.TargetDirectory) ? "" : Path.GetFullPath(request.TargetDirectory)
    };
    private static RecoveryCloneRequest CanonicalClone(RecoveryCloneRequest request) => request with
    {
        Snapshot = CanonicalCreate(request.Snapshot),
        TargetInstance = request.TargetInstance?.Trim() ?? "",
        TargetDirectory = string.IsNullOrWhiteSpace(request.TargetDirectory) ? "" : Path.GetFullPath(request.TargetDirectory)
    };
    private static string Fingerprint<T>(T request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request))).ToLowerInvariant();
    private static bool IsFileName(string value) => !string.IsNullOrWhiteSpace(value) && Path.GetFileName(value) == value && !value.Contains("..", StringComparison.Ordinal);
    private static void ValidateCreate(RecoveryPointCreateRequest r) { if (string.IsNullOrWhiteSpace(r.SourceInstance) || string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.DestinationRoot)) throw new ArgumentException("Source instance, name, and destination are required."); }
    private static void ValidateTarget(RecoveryRestoreRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.TargetInstance)) throw new ArgumentException("Target instance is required.");
        if (!r.ImportInPlace && string.IsNullOrWhiteSpace(r.TargetDirectory)) throw new ArgumentException("Target directory is required for a managed import.");
        if (r.ImportInPlace && !string.IsNullOrWhiteSpace(r.TargetDirectory)) throw new ArgumentException("Import-in-place does not accept a target directory; WSL manages the registration location.");
    }
    private static long RequiredBytes(long estimate)
    {
        // An unknown estimate is not treated as zero. The conservative 1 GiB floor makes
        // free-space preflight meaningful while the runtime obtains filesystem/VHD evidence.
        var boundedEstimate = Math.Max(estimate, 1024L * 1024 * 1024);
        return checked(boundedEstimate + boundedEstimate / 10);
    }
    private static bool PathEquals(string a, string b) => StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(a), Path.GetFullPath(b));
    private static bool IsOwnedPointDirectory(string directory, RecoveryPointManifest manifest) => RecoveryPathSafety.IsOwnedPointDirectory(directory, manifest);
    private static bool IsSafeRecoveryTarget(string target)
    {
        try
        {
            var full = Path.GetFullPath(target);
            var parent = Path.GetDirectoryName(full);
            if (parent is null || !RecoveryPathSafety.IsNoReparsePointInExistingPath(parent)) return false;
            var info = new DirectoryInfo(full);
            return !info.Exists || !info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    private static void EnsureOwnedPoint(RecoveryPointSummary item)
    {
        if (!IsOwnedPointDirectory(item.DirectoryPath, item.Manifest))
            throw new WslOperationFailedException("Recovery point ownership or path safety could not be verified.", DistroNexusErrorCode.RecoveryPointInvalid, "ValidateRecoveryPoint");
    }
    private TargetReservation ReserveTarget(string targetDirectory, string targetInstance, string operationId)
    {
        var target = Path.GetFullPath(targetDirectory);
        var parent = Path.GetDirectoryName(target) ?? throw new ArgumentException("Target directory must have a parent.", nameof(targetDirectory));
        Directory.CreateDirectory(parent);
        if (!IsSafeRecoveryTarget(target)) throw new WslOperationFailedException("Restore target path is not safe for recovery.", DistroNexusErrorCode.RecoveryPointInvalid, "ReserveRecoveryTarget");
        if (Directory.Exists(target)) throw new WslOperationFailedException("Restore target directory already exists.", DistroNexusErrorCode.RecoveryTargetReserved, "ReserveRecoveryTarget");
        var name = Path.GetFileName(target);
        if (string.IsNullOrWhiteSpace(name)) throw new WslOperationFailedException("Restore target directory is invalid.", DistroNexusErrorCode.RecoveryPointInvalid, "ReserveRecoveryTarget");
        var reservation = Path.Combine(parent, $".{name}.distronexus-recovery.reservation");
        try
        {
            var directoryLease = CreateReservation(reservation, operationId);
            try
            {
                Directory.CreateDirectory(OperationsRoot);
                var instanceReservation = Path.Combine(OperationsRoot, $"instance-{ReservationKey(targetInstance)}.reservation");
                return new TargetReservation(directoryLease, CreateReservation(instanceReservation, operationId));
            }
            catch { directoryLease.Dispose(); throw; }
        }
        catch (IOException ex) { throw new WslOperationFailedException("Restore target is already reserved by another recovery operation.", ex, DistroNexusErrorCode.RecoveryTargetReserved, "ReserveRecoveryTarget"); }
    }
    private TargetReservation ReserveImportInPlaceTarget(string targetInstance, string operationId)
    {
        Directory.CreateDirectory(OperationsRoot);
        var instanceReservation = Path.Combine(OperationsRoot, $"instance-{ReservationKey(targetInstance)}.reservation");
        try { return new TargetReservation(null, CreateReservation(instanceReservation, operationId)); }
        catch (IOException ex) { throw new WslOperationFailedException("Restore target is already reserved by another recovery operation.", ex, DistroNexusErrorCode.RecoveryTargetReserved, "ReserveRecoveryTarget"); }
    }
    private static FileStream CreateReservation(string path, string operationId)
    {
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024, FileOptions.DeleteOnClose);
        using var writer = new StreamWriter(stream, leaveOpen: true);
        writer.Write(operationId); writer.Flush(); stream.Position = 0;
        return stream;
    }
    private static string ReservationKey(string instanceName) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(instanceName.Trim())));
    private sealed class TargetReservation(FileStream? directoryLease, FileStream instanceLease) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { directoryLease?.Dispose(); instanceLease.Dispose(); return ValueTask.CompletedTask; }
    }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }
    private async Task AddToCatalogAsync(string directory, CancellationToken ct) =>
        await MutateStateAsync(state => state with { Catalog = (state.Catalog ?? []).Append(Path.GetFullPath(directory)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() }, ct);
    private async Task RemoveFromCatalogAsync(string directory, CancellationToken ct) =>
        await MutateStateAsync(state => state with { Catalog = (state.Catalog ?? []).Where(x => !PathEquals(x, directory)).ToList() }, ct);

    // A single versioned state file avoids lost updates between catalog and retention writes.
    // The lock is process-visible, while File.Move provides atomic replacement for readers.
    private async Task<RecoveryState> ReadStateAsync(CancellationToken ct)
    {
        await using var lease = await AcquireStateLeaseAsync(ct);
        return await ReadStateUnsafeAsync(ct);
    }
    private async Task MutateStateAsync(Func<RecoveryState, RecoveryState> mutation, CancellationToken ct)
    {
        await using var lease = await AcquireStateLeaseAsync(ct);
        var state = mutation(await ReadStateUnsafeAsync(ct));
        await WriteStateUnsafeAsync(state, ct);
    }
    private async Task<RecoveryState> ReadStateUnsafeAsync(CancellationToken ct)
    {
        if (File.Exists(StatePath))
        {
            try
            {
                var state = JsonSerializer.Deserialize<RecoveryState>(await File.ReadAllTextAsync(StatePath, ct));
                if (state is { SchemaVersion: 1 }) return NormalizeState(state);
            }
            catch (JsonException) { }
        }
        // One-time compatible migration from the short-lived separate files.
        var catalog = new List<string>(); var retention = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try { if (File.Exists(CatalogPath)) catalog.AddRange(JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(CatalogPath, ct)) ?? []); } catch (JsonException) { }
        try { if (File.Exists(RetentionPath)) foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, int>>(await File.ReadAllTextAsync(RetentionPath, ct)) ?? []) retention[pair.Key] = pair.Value; } catch (JsonException) { }
        return NormalizeState(new RecoveryState(1, catalog, retention));
    }
    private async Task WriteStateUnsafeAsync(RecoveryState state, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        var temp = StatePath + ".partial-" + Guid.NewGuid().ToString("N");
        try { await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(NormalizeState(state)), ct); File.Move(temp, StatePath, true); }
        finally { TryDeleteFile(temp); }
    }
    private async Task<FileStream> AcquireStateLeaseAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        for (;;)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(StateLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.DeleteOnClose); }
            catch (IOException) { await Task.Delay(25, ct); }
        }
    }
    private static RecoveryState NormalizeState(RecoveryState state) => new(1,
        (state.Catalog ?? []).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        new Dictionary<string, int>((state.Retention ?? new()).Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0), StringComparer.OrdinalIgnoreCase));
    private sealed record RecoveryState(int SchemaVersion, List<string>? Catalog, Dictionary<string, int>? Retention);

    private async Task ReconcileOwnedOperationsAsync(CancellationToken ct)
    {
        if (!Directory.Exists(OperationsRoot)) return;
        foreach (var journal in Directory.EnumerateFiles(OperationsRoot, "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var journalInfo = new FileInfo(journal);
                if (journalInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || !RecoveryPathSafety.IsNoReparsePointInExistingPath(OperationsRoot)) continue;
                // A journal is diagnostic evidence, not authority to change a live WSL
                // registration. In particular, do not infer ownership after a process restart.
                _ = JsonSerializer.Deserialize<RecoveryOperationMarker>(await File.ReadAllTextAsync(journal, ct));
            }
            catch (JsonException) { /* retain malformed evidence for manual inspection */ }
            catch (IOException) { /* retry safely on the next read */ }
            catch (UnauthorizedAccessException) { /* retry safely on the next read */ }
        }
    }
    private static IEnumerable<FileInfo> EnumerateBackupArtifacts(string destination)
    {
        try
        {
            var root = Path.GetFullPath(destination);
            if (!RecoveryPathSafety.IsNoReparsePointInExistingPath(root)) return [];
            return Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path)).Where(file => !file.Attributes.HasFlag(FileAttributes.ReparsePoint)).ToArray();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }
    private static async Task WriteManualRecoveryEvidenceAsync(string marker, string journal, RecoveryOperationMarker record)
    {
        // Deliberately do not use the caller cancellation token: cancellation/failure must not
        // erase evidence needed to distinguish this operation from later WSL state.
        var payload = JsonSerializer.Serialize(record);
        await File.WriteAllTextAsync(marker, payload, CancellationToken.None);
        await File.WriteAllTextAsync(journal, payload, CancellationToken.None);
    }
    private static IReadOnlyList<string> ManualRecoveryInstructions() =>
    [
        "Review this operation journal and its matching marker before changing WSL state.",
        "Compare the target instance name and recorded registration id with the current WSL registration.",
        "Only perform a manual unregister or directory cleanup after confirming it belongs to this failed operation."
    ];
    private sealed record RecoveryOperationMarker(string OperationId, string TargetInstance, string TargetDirectory, string State,
        RecoveryRegistration? Registration = null, string? Failure = null, IReadOnlyList<string>? ManualRecoveryInstructions = null);
}
