using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface ILifecyclePathOperationService
{
    Task<LifecycleOperationPreview> PreviewRemoveAsync(string name, bool keepFiles, CancellationToken cancellationToken = default);
    Task<LifecycleOperationPreview> PreviewMoveAsync(string name, string destination, CancellationToken cancellationToken = default);
    Task<LifecycleOperationPreview> PreviewRenameAsync(string name, string newName, CancellationToken cancellationToken = default);
    Task<LifecycleOperationPreview> PreviewExportAsync(string name, string destination, bool stopRunning, CancellationToken cancellationToken = default);
    Task<LifecycleOperationPreview> PreviewImportAsync(string name, string source, string installPath, CancellationToken cancellationToken = default);
    Task<LifecycleOperationResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default);
}
