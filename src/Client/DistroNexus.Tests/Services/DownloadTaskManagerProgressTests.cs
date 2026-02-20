using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

public class DownloadTaskManagerProgressTests
{
    [Fact]
    public async Task AddTask_WhenProgressReported_ShouldCalculateSpeedAndFormattedProgress()
    {
        var mockDownloadService = new Mock<IDownloadService>();
        var mockCatalogService = new Mock<ICatalogService>();
        var mockSettingsService = new Mock<ISettingsService>();
        var mockLogger = new Mock<ILogger<DownloadTaskManager>>();

        mockSettingsService.Setup(service => service.LoadSettings()).Returns(new GlobalSettings
        {
            MaxConcurrentDownloads = 1,
            AutoRetryDownloads = false,
            MaxRetryAttempts = 0,
            PackageCachePath = Path.GetTempPath()
        });

        mockDownloadService
            .Setup(service => service.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<(long BytesRead, long TotalBytes)>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, IProgress<(long BytesRead, long TotalBytes)>? progress, CancellationToken _) =>
            {
                progress!.Report((128 * 1024, 1024 * 1024));
                await Task.Delay(650);
                progress.Report((640 * 1024, 1024 * 1024));
                await Task.Delay(650);
                progress.Report((1024 * 1024, 1024 * 1024));
                return true;
            });

        mockCatalogService.Setup(service => service.GetDistributionByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DistroPackage());

        var manager = new DownloadTaskManager(
            mockDownloadService.Object,
            mockCatalogService.Object,
            mockSettingsService.Object,
            mockLogger.Object);

        var package = new DistroPackage
        {
            Id = "pkg-speed",
            Name = "Speed Package",
            DownloadUrl = "https://example.com/pkg-speed.wsl",
            FileSize = 1024 * 1024
        };

        var task = manager.AddTask(package, Path.Combine(Path.GetTempPath(), "pkg-speed.wsl"));
        await WaitForCompletionAsync(task);

        Assert.Equal(DownloadStatus.Completed, task.Status);
        Assert.True(task.BytesPerSecond > 0);
        Assert.Contains("/", task.FormattedProgress);
    }

    [Fact]
    public async Task AddTask_WhenNoByteDeltaBetweenReports_ShouldSetSpeedToZero()
    {
        var mockDownloadService = new Mock<IDownloadService>();
        var mockCatalogService = new Mock<ICatalogService>();
        var mockSettingsService = new Mock<ISettingsService>();
        var mockLogger = new Mock<ILogger<DownloadTaskManager>>();

        mockSettingsService.Setup(service => service.LoadSettings()).Returns(new GlobalSettings
        {
            MaxConcurrentDownloads = 1,
            AutoRetryDownloads = false,
            MaxRetryAttempts = 0,
            PackageCachePath = Path.GetTempPath()
        });

        mockDownloadService
            .Setup(service => service.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<(long BytesRead, long TotalBytes)>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, IProgress<(long BytesRead, long TotalBytes)>? progress, CancellationToken _) =>
            {
                progress!.Report((512 * 1024, 1024 * 1024));
                await Task.Delay(650);
                progress.Report((1024 * 1024, 1024 * 1024));
                await Task.Delay(650);
                progress.Report((1024 * 1024, 1024 * 1024));
                return true;
            });

        mockCatalogService.Setup(service => service.GetDistributionByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DistroPackage());

        var manager = new DownloadTaskManager(
            mockDownloadService.Object,
            mockCatalogService.Object,
            mockSettingsService.Object,
            mockLogger.Object);

        var package = new DistroPackage
        {
            Id = "pkg-stall",
            Name = "Stall Package",
            DownloadUrl = "https://example.com/pkg-stall.wsl",
            FileSize = 1024 * 1024
        };

        var task = manager.AddTask(package, Path.Combine(Path.GetTempPath(), "pkg-stall.wsl"));
        await WaitForCompletionAsync(task);
        await WaitForExpectedSpeedAsync(task, expectedBytesPerSecond: 0);

        Assert.Equal(DownloadStatus.Completed, task.Status);
        Assert.Equal(0, task.BytesPerSecond);
    }

    private static async Task WaitForExpectedSpeedAsync(DownloadTask task, long expectedBytesPerSecond)
    {
        var timeout = DateTime.UtcNow.AddSeconds(3);

        while (DateTime.UtcNow < timeout)
        {
            if (task.BytesPerSecond == expectedBytesPerSecond)
            {
                return;
            }

            await Task.Delay(50);
        }
    }

    private static async Task WaitForCompletionAsync(DownloadTask task)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < timeout)
        {
            if (task.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Download task did not reach terminal state in time.");
    }
}
