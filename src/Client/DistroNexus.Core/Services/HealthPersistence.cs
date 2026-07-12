using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>
/// Projects the backup service's authoritative persisted history for Health Center. It deliberately
/// does not maintain a second file: recording and reading always use the same atomic store.
/// </summary>
public sealed class BackupHealthSource : IBackupHealthSource
{
    public const long DefaultFreeSpaceWarningThresholdBytes = 2L * 1024 * 1024 * 1024;
    private readonly IBackupService _backups;
    private readonly long _freeSpaceWarningThresholdBytes;
    public BackupHealthSource(IBackupService backups, long freeSpaceWarningThresholdBytes = DefaultFreeSpaceWarningThresholdBytes)
    {
        _backups = backups;
        _freeSpaceWarningThresholdBytes = freeSpaceWarningThresholdBytes > 0 ? freeSpaceWarningThresholdBytes : throw new ArgumentOutOfRangeException(nameof(freeSpaceWarningThresholdBytes));
    }
    public async Task<IReadOnlyDictionary<string, BackupHealthState>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _backups.GetSchedulesAsync(cancellationToken).ConfigureAwait(false);
        var records = await _backups.GetHealthHistoryAsync(cancellationToken).ConfigureAwait(false);
        return schedules.ToDictionary(s => s.Name, s =>
        {
            var own = records.Where(x => string.Equals(x.InstanceName, s.Name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CompletedAt).ToArray();
            var failures = own.TakeWhile(x => !x.Succeeded).Count();
            var exists = Directory.Exists(s.Destination); long? free = null;
            try { if (exists) free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(s.Destination))!).AvailableFreeSpace; } catch (IOException) { }
            var lowSpace = free is not null && free < _freeSpaceWarningThresholdBytes;
            var detail = !exists ? "Destination is unavailable."
                : lowSpace ? $"Destination free space is below the configured {FormatBytes(_freeSpaceWarningThresholdBytes)} warning threshold."
                : failures > 0 ? $"{failures} consecutive backup failure(s)."
                : "Destination and recent backup history are healthy.";
            return new BackupHealthState(exists, free, failures, detail, _freeSpaceWarningThresholdBytes);
        }, StringComparer.Ordinal);
    }
    public async Task RecordAsync(BackupHealthRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _backups.RecordHealthAsync(record with { Detail = SensitiveDataRedactor.Redact(record.Detail) }, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatBytes(long value) => (value / (1024d * 1024 * 1024)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " GiB";
}

public sealed class EmptyStructuredErrorProvider : IStructuredErrorProvider
{
    public Task<IReadOnlyList<StructuredErrorRecord>> GetRecentAsync(int maximumEntries, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StructuredErrorRecord>>([]);
}
