using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Manages instance backup schedules and on-demand backup invocation.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Returns all persisted backup schedules.
    /// </summary>
    Task<List<BackupSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists (creates or updates) a backup schedule.
    /// </summary>
    /// <param name="schedule">The schedule to save.</param>
    Task SaveScheduleAsync(BackupSchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the backup schedule for the given instance name and unregisters the Task Scheduler task.
    /// </summary>
    /// <param name="instanceName">The WSL instance name.</param>
    Task RemoveScheduleAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an on-demand backup for the given instance.
    /// Delegates to the <c>Invoke-DistroNexusBackup</c> PowerShell cmdlet.
    /// </summary>
    /// <param name="instanceName">The WSL instance name.</param>
    /// <param name="destination">Destination directory for the backup TAR.</param>
    /// <param name="retentionCount">Maximum number of backup files to retain.</param>
    Task InvokeBackupAsync(string instanceName, string destination, int retentionCount, CancellationToken cancellationToken = default);
}
