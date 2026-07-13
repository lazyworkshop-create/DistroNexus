using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Owns recovery-point manifests and delegates WSL-specific work to a typed runtime adapter.</summary>
public interface IRecoveryPointService
{
    Task<IReadOnlyList<RecoveryPointSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<RecoveryOperationPreview> PreviewCreateAsync(RecoveryPointCreateRequest request, CancellationToken cancellationToken = default);
    Task<RecoveryPointSummary> CreateAsync(RecoveryPointCreateRequest request, string previewToken, CancellationToken cancellationToken = default, IProgress<RecoveryOperationProgress>? progress = null);
    Task<RecoveryOperationPreview> PreviewRestoreAsync(RecoveryRestoreRequest request, CancellationToken cancellationToken = default);
    Task RestoreAsync(RecoveryRestoreRequest request, string previewToken, CancellationToken cancellationToken = default, IProgress<RecoveryOperationProgress>? progress = null);
    Task<RecoveryOperationPreview> PreviewCloneAsync(RecoveryCloneRequest request, CancellationToken cancellationToken = default);
    Task RestoreCloneAsync(RecoveryCloneRequest request, string previewToken, CancellationToken cancellationToken = default, IProgress<RecoveryOperationProgress>? progress = null);
    Task<RecoveryPointVerification> VerifyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateNotesAsync(Guid id, string description, IReadOnlyList<string> tags, bool pinned, CancellationToken cancellationToken = default);
    /// <summary>Deletes a point only after explicit confirmation. Retention cleanup retains its separate implicit safeguards.</summary>
    Task DeleteAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default);
    Task ApplyRetentionAsync(string sourceInstance, int maximum, CancellationToken cancellationToken = default);
    Task<int?> GetRetentionAsync(string sourceInstance, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default);
}

public interface IRecoveryPointRuntime
{
    Task<RecoveryRuntimeSource> GetSourceAsync(string instanceName, CancellationToken cancellationToken = default);
    Task ExportAsync(string instanceName, string partialPayloadPath, RecoveryPointFormat format, CancellationToken cancellationToken = default);
    Task ImportAsync(string operationId, string instanceName, string payloadPath, string targetDirectory, RecoveryPointFormat format, bool importInPlace, CancellationToken cancellationToken = default);
    Task<bool> InstanceExistsAsync(string instanceName, CancellationToken cancellationToken = default);
    Task<bool> IsRegisteredAsync(string instanceName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns a stable identity and normalized base path for the current registration.  A
    /// missing value means ownership cannot be established and must never authorize cleanup.
    /// </summary>
    Task<RecoveryRegistration?> GetRegistrationAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult<RecoveryRegistration?>(null);
    Task<bool> IsRunningAsync(string instanceName, CancellationToken cancellationToken = default);
    Task StopAsync(string instanceName, CancellationToken cancellationToken = default);
    Task StartAsync(string instanceName, CancellationToken cancellationToken = default);
    /// <summary>Checks that the imported distribution can execute the fixed boot probe.</summary>
    Task<bool> VerifyBootAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

public sealed record RecoveryRuntimeSource(int WslVersion, long EstimatedBytes, bool IsRunning, bool SupportsVhdExport, bool SupportsImportInPlace);
public sealed record RecoveryRegistration(string RegistrationId, string BasePath);
