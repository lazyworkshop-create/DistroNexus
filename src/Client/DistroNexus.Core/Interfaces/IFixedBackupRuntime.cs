using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Closed backup authority used only by fixed Bridge routes.</summary>
public interface IFixedBackupRuntime
{
    Task<IReadOnlyList<BackupScheduleSummary>> GetSchedulesAsync(CancellationToken cancellationToken = default);
    Task<BackupOperationPreview> PreviewScheduleAsync(BackupScheduleRequest request, CancellationToken cancellationToken = default);
    Task<BackupOperationPreview> PreviewScheduleRemovalAsync(string instanceName, CancellationToken cancellationToken = default);
    Task<BackupOperationPreview> PreviewBackupAsync(string instanceName, int retentionCount, CancellationToken cancellationToken = default);
    Task<BackupOperationResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<BackupOperationResult> RunScheduledAsync(string scheduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupNotification>> ConsumeNotificationsAsync(CancellationToken cancellationToken = default);
}
