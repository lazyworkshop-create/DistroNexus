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
    }

    private string SchedulesFilePath => Path.Combine(_appDataDir, "backup-schedules.json");

    // ── IBackupService ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<BackupSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SchedulesFilePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(SchedulesFilePath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<BackupSchedule>>(json, _jsonOptions);
            return list ?? [];
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

        var schedules = await GetSchedulesAsync(cancellationToken);

        // Remove existing entry for same name (upsert)
        schedules.RemoveAll(s => string.Equals(s.Name, schedule.Name, StringComparison.OrdinalIgnoreCase));
        schedules.Add(schedule);

        await WriteSchedulesAsync(schedules, cancellationToken);
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

        var schedules = await GetSchedulesAsync(cancellationToken);
        var existing  = schedules.FirstOrDefault(s =>
            string.Equals(s.Name, instanceName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            throw new WslOperationFailedException(
                $"No backup schedule found for instance '{instanceName}'.",
                DistroNexusErrorCode.ScheduleNotFound,
                operation: "RemoveBackupSchedule",
                instanceName: instanceName);

        schedules.Remove(existing);
        await WriteSchedulesAsync(schedules, cancellationToken);
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
            throw new WslOperationFailedException(
                $"Backup failed for '{instanceName}': {result.Error}",
                DistroNexusErrorCode.BackupFailed,
                operation: "InvokeBackup",
                instanceName: instanceName);

        _logger.LogInformation("Backup completed for instance '{Name}' -> '{Destination}'", instanceName, destination);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task WriteSchedulesAsync(List<BackupSchedule> schedules, CancellationToken ct)
    {
        Directory.CreateDirectory(_appDataDir);
        var json = JsonSerializer.Serialize(schedules, _jsonOptions);
        await File.WriteAllTextAsync(SchedulesFilePath, json, ct);
    }
}
