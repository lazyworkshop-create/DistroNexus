using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IPackageDownloadJobService
{
    Task<PackageJobStartPreviewResult> PreviewStartAsync(string packageId, CancellationToken cancellationToken = default);
    Task<PackageJobStartResult> StartAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageDownloadJob>> ListAsync(CancellationToken cancellationToken = default);
    Task<PackageJobActionPreviewResult> PreviewActionAsync(string jobId, string action, CancellationToken cancellationToken = default);
    Task<PackageJobActionResult> ExecuteActionAsync(string previewToken, CancellationToken cancellationToken = default);
}
