using System.Text.Json;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Manages instance backup schedules (JSON persistence) and delegates backup invocation to PS cmdlets.
/// </summary>
public class BackupService : IBackupService
{
    private readonly IPowerShellService _powerShellService;
    private readonly ILogger<BackupService> _logger;
    private readonly string _appDataDir;
    private readonly VersionedJsonStore<List<BackupSchedule>> _scheduleStore;
    private readonly VersionedJsonStore<List<BackupHealthRecord>> _healthStore;

    private const int VeryLongOperationTimeoutSeconds = 300;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented    = true,
        PropertyNameCaseInsensitive = true,
    };

    public BackupService(
        IPowerShellService powerShellService,
        ILogger<BackupService> logger,
        string? appDataDir = null)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _logger            = logger            ?? throw new ArgumentNullException(nameof(logger));
        _appDataDir        = appDataDir
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DistroNexus");
        _scheduleStore = new VersionedJsonStore<List<BackupSchedule>>(SchedulesFilePath, legacyReader: node =>
            node.Deserialize<List<BackupSchedule>>(_jsonOptions) ?? []);
        _healthStore = new VersionedJsonStore<List<BackupHealthRecord>>(Path.Combine(_appDataDir, "backup-health.json"), legacyReader: node => node.Deserialize<List<BackupHealthRecord>>(_jsonOptions) ?? []);
    }

    private string SchedulesFilePath => Path.Combine(_appDataDir, "backup-schedules.json");

    // ── IBackupService ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<BackupSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _scheduleStore.ReadAsync(cancellationToken);
            if (result.Error == StoreErrorKind.NotFound) return [];
            if (!result.Succeeded)
                throw new WslOperationFailedException(result.Message ?? "Backup schedules read failed.",
                    result.Error == StoreErrorKind.NewerSchema ? DistroNexusErrorCode.StoreSchemaUnsupported : DistroNexusErrorCode.StoreDocumentInvalid,
                    operation: "GetBackupSchedules");
            return result.Value!.Value;
        }
        catch (WslOperationFailedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read backup schedules from {Path}", SchedulesFilePath);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task SaveScheduleAsync(BackupSchedule schedule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var document = await _scheduleStore.ReadAsync(cancellationToken);
        var schedules = document.Error == StoreErrorKind.NotFound ? [] : document.Value?.Value ?? [];

        // Remove existing entry for same name (upsert)
        schedules.RemoveAll(s => string.Equals(s.Name, schedule.Name, StringComparison.OrdinalIgnoreCase));
        schedules.Add(schedule);

        await WriteSchedulesAsync(schedules, document.Value?.Revision ?? 0, cancellationToken);
        _logger.LogInformation("Saved backup schedule for instance '{Name}'", schedule.Name);
    }

    /// <inheritdoc/>
    public async Task RemoveScheduleAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        if (instanceName is null) throw new ArgumentNullException(nameof(instanceName));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new WslOperationFailedException(
                "Instance name must not be empty.",
                DistroNexusErrorCode.InstanceNotFound,
                operation: "RemoveBackupSchedule");

        var document = await _scheduleStore.ReadAsync(cancellationToken);
        var schedules = document.Error == StoreErrorKind.NotFound ? [] : document.Value?.Value ?? [];
        var existing  = schedules.FirstOrDefault(s =>
            string.Equals(s.Name, instanceName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            throw new WslOperationFailedException(
                $"No backup schedule found for instance '{instanceName}'.",
                DistroNexusErrorCode.ScheduleNotFound,
                operation: "RemoveBackupSchedule",
                instanceName: instanceName);

        schedules.Remove(existing);
        await WriteSchedulesAsync(schedules, document.Value?.Revision ?? 0, cancellationToken);
        _logger.LogInformation("Removed backup schedule for instance '{Name}'", instanceName);
    }

    /// <inheritdoc/>
    public async Task InvokeBackupAsync(
        string instanceName,
        string destination,
        int retentionCount,
        CancellationToken cancellationToken = default)
    {
        if (instanceName is null) throw new ArgumentNullException(nameof(instanceName));
        if (string.IsNullOrWhiteSpace(destination))
            throw new WslOperationFailedException(
                "Backup destination must not be empty.",
                DistroNexusErrorCode.BackupDestinationFull,
                operation: "InvokeBackup",
                instanceName: instanceName);

        var parameters = new Dictionary<string, object>
        {
            ["Name"]           = instanceName,
            ["Destination"]    = destination,
            ["RetentionCount"] = retentionCount
        };

        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Invoke-DistroNexusBackup",
            parameters,
            new ModuleCallOptions { TimeoutSeconds = VeryLongOperationTimeoutSeconds },
            cancellationToken);

        if (result.ExitCode != 0)
        {
            await RecordHealthAsync(new BackupHealthRecord(instanceName, DateTimeOffset.UtcNow, false, "DN-4006", result.Error), cancellationToken);
            throw new WslOperationFailedException(
                $"Backup failed for '{instanceName}': {result.Error}",
                DistroNexusErrorCode.BackupFailed,
                operation: "InvokeBackup",
                instanceName: instanceName);
        }

        await RecordHealthAsync(new BackupHealthRecord(instanceName, DateTimeOffset.UtcNow, true), cancellationToken);
        _logger.LogInformation("Backup completed for instance '{Name}' -> '{Destination}'", instanceName, destination);
    }

    public async Task<IReadOnlyList<BackupHealthRecord>> GetHealthHistoryAsync(CancellationToken cancellationToken = default)
    {
        var read = await _healthStore.ReadAsync(cancellationToken);
        return read.Succeeded ? read.Value!.Value.Where(x => x.CompletedAt >= DateTimeOffset.UtcNow.AddDays(-30)).OrderByDescending(x => x.CompletedAt).ToArray() : [];
    }

    /// <inheritdoc/>
    public Task RecordHealthAsync(BackupHealthRecord record, CancellationToken cancellationToken = default) =>
        AppendHealthRecordAsync(record, cancellationToken);

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task WriteSchedulesAsync(List<BackupSchedule> schedules, long revision, CancellationToken ct)
    {
        Directory.CreateDirectory(_appDataDir);
        var result = await _scheduleStore.WriteAsync(schedules, revision, ct);
        if (!result.Succeeded)
            throw new WslOperationFailedException(result.Message ?? "Backup schedule write failed.",
                result.Error == StoreErrorKind.RevisionConflict ? DistroNexusErrorCode.StoreRevisionConflict : DistroNexusErrorCode.StoreWriteFailed,
                operation: "SaveBackupSchedule");
    }
    private async Task AppendHealthRecordAsync(BackupHealthRecord record, CancellationToken ct)
    {
        var read = await _healthStore.ReadAsync(ct); var all = read.Succeeded ? read.Value!.Value : [];
        if (read.Error is not (StoreErrorKind.None or StoreErrorKind.NotFound))
            throw new WslOperationFailedException(read.Message ?? "Backup health history read failed.",
                read.Error == StoreErrorKind.NewerSchema ? DistroNexusErrorCode.StoreSchemaUnsupported : DistroNexusErrorCode.StoreDocumentInvalid,
                operation: "RecordBackupHealth");
        all = all.Where(x => x.CompletedAt >= DateTimeOffset.UtcNow.AddDays(-30)).Append(record with { Detail = SensitiveDataRedactor.Redact(record.Detail) }).ToList();
        var write = await _healthStore.WriteAsync(all, read.Value?.Revision ?? 0, ct);
        if (!write.Succeeded)
            throw new WslOperationFailedException(write.Message ?? "Backup health history write failed.",
                write.Error == StoreErrorKind.RevisionConflict ? DistroNexusErrorCode.StoreRevisionConflict : DistroNexusErrorCode.StoreWriteFailed,
                operation: "RecordBackupHealth");
    }
}
