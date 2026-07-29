using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class PackageDownloadBulkClearTests
{
    [Fact]
    public async Task ClearCompletedDownloads_ClearsInterruptedJobs()
    {
        var client = new Mock<IPowerShellModuleClient>();
        const string jobId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string token = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        client.Setup(x => x.PreviewPackageDownloadJobActionAsync(jobId, "clear", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageJobActionPreviewResult(token, DateTimeOffset.UtcNow.AddMinutes(1), jobId, "Package.JobPreviewReady"));
        client.Setup(x => x.ExecutePackageDownloadJobActionAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageJobActionResult(jobId, "Package.Cleared"));
        client.Setup(x => x.GetPackageDownloadJobsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var viewModel = new MainViewModel(Mock.Of<IServiceProvider>(), Mock.Of<ILogger<MainViewModel>>(), client.Object, Mock.Of<IDialogService>());
        viewModel.DownloadJobs.Add(new PackageDownloadJob(jobId, "ubuntu", "Ubuntu", "Interrupted", 0, "Package.Interrupted"));

        await viewModel.ClearCompletedDownloadsCommand.ExecuteAsync(null);

        client.Verify(x => x.PreviewPackageDownloadJobActionAsync(jobId, "clear", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.ExecutePackageDownloadJobActionAsync(token, It.IsAny<CancellationToken>()), Times.Once);
    }
}
