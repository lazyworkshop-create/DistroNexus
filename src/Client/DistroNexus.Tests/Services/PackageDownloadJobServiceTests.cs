using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class PackageDownloadJobServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dn-package-jobs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task Start_RejectsPreviewWhenPackageProvenanceDrifts()
    {
        var provenance = "catalog|revision-1";
        var catalog = Catalog(_ => provenance);
        var service = new PackageDownloadJobService(catalog.Object, new ControlledDownload(), _root);

        var preview = await service.PreviewStartAsync("ubuntu");
        provenance = "catalog|revision-2";

        var result = await service.StartAsync(preview.PreviewToken!);

        Assert.Null(result.JobId);
        Assert.Equal("Package.JobStateChanged", result.OutcomeCode);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("version")]
    [InlineData("url")]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("provenance")]
    public async Task Start_RejectsPreviewWhenAnyAuthorizedMaterialDrifts(string field)
    {
        var package = Package();
        var provenance = "catalog|revision-1";
        var catalog = new Mock<ICatalogService>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDistributionByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => package);
        catalog.Setup(x => x.GetPackageDownloadProvenanceAsync(It.IsAny<DistroPackage>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => provenance);
        var service = new PackageDownloadJobService(catalog.Object, new ControlledDownload(), _root);

        var preview = await service.PreviewStartAsync("ubuntu");
        switch (field)
        {
            case "id": package.Id = "ubuntu-replacement"; break;
            case "version": package.Version = "24.10"; break;
            case "url": package.DownloadUrl = "https://example.test/ubuntu?revision=2"; break;
            case "hash": package.Sha256 = new string('b', 64); break;
            case "size": package.FileSize = 5; break;
            case "provenance": provenance = "catalog|revision-2"; break;
        }

        var result = await service.StartAsync(preview.PreviewToken!);

        Assert.Null(result.JobId);
        Assert.Equal("Package.JobStateChanged", result.OutcomeCode);
    }

    [Fact]
    public async Task FreshService_PreviewRecoversPriorActiveJobAndRemovesItsExactPartialDestination()
    {
        var download = new ControlledDownload();
        var first = New(download);
        var preview = await first.PreviewStartAsync("ubuntu");
        await first.StartAsync(preview.PreviewToken!);
        var destination = await download.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await File.WriteAllTextAsync(destination, "partial");

        var second = New(new ControlledDownload());
        var nextPreview = await second.PreviewStartAsync("ubuntu");
        var recovered = Assert.Single(await second.ListAsync());

        Assert.NotNull(nextPreview.PreviewToken);
        Assert.Equal("Interrupted", recovered.State);
        Assert.False(File.Exists(destination));
        download.Cancel();
    }

    [Theory]
    [InlineData(DownloadMode.Cancelled)]
    [InlineData(DownloadMode.SizeMismatch)]
    [InlineData(DownloadMode.HashMismatch)]
    public async Task FailedOrCancelledTransfers_RemovePartialDestination(DownloadMode mode)
    {
        var download = new ControlledDownload(mode);
        var service = New(download);
        var preview = await service.PreviewStartAsync("ubuntu");
        var start = await service.StartAsync(preview.PreviewToken!);
        var destination = await download.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        if (mode == DownloadMode.Cancelled)
        {
            var cancel = await service.PreviewActionAsync(start.JobId!, "cancel");
            await service.ExecuteActionAsync(cancel.PreviewToken!);
        }

        await download.Finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitForAsync(() => !File.Exists(destination));
        var job = Assert.Single(await service.ListAsync());

        Assert.False(File.Exists(destination));
        Assert.Equal(mode == DownloadMode.Cancelled ? "Cancelled" : "Failed", job.State);
    }

    [Fact]
    public async Task List_IsBoundedToTwoHundredJobs()
    {
        var catalog = Catalog(_ => "catalog|revision-1");
        var service = new PackageDownloadJobService(catalog.Object, new ControlledDownload(DownloadMode.Fail), _root);

        for (var index = 0; index < 201; index++)
        {
            var packageId = "ubuntu-" + index;
            var preview = await service.PreviewStartAsync(packageId);
            var start = await service.StartAsync(preview.PreviewToken!);
            Assert.NotNull(start.JobId);
        }

        Assert.Equal(200, (await service.ListAsync()).Count);
    }

    [Fact]
    public async Task FixedCancelRetryAndClearActions_RequireAndConsumeDistinctPreviewGrants()
    {
        var download = new ControlledDownload(DownloadMode.Fail);
        var service = New(download);
        var preview = await service.PreviewStartAsync("ubuntu");
        var started = await service.StartAsync(preview.PreviewToken!);
        await download.Finished.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var retry = await service.PreviewActionAsync(started.JobId!, "retry");
        var retried = await service.ExecuteActionAsync(retry.PreviewToken!);
        Assert.Equal("Package.Retried", retried.OutcomeCode);

        await WaitForAsync(async () => (await service.ListAsync()).Single().State == "Failed");
        var clear = await service.PreviewActionAsync(started.JobId!, "clear");
        var cleared = await service.ExecuteActionAsync(clear.PreviewToken!);

        Assert.Equal("Package.Cleared", cleared.OutcomeCode);
        Assert.Empty(await service.ListAsync());
        var invalid = await service.PreviewActionAsync(started.JobId!, "delete");
        Assert.Equal("Package.JobUnavailable", invalid.OutcomeCode);
    }

    private PackageDownloadJobService New(IDownloadService download) => new(Catalog(_ => "catalog|revision-1").Object, download, _root);

    private static Mock<ICatalogService> Catalog(Func<string, string> provenance)
    {
        var catalog = new Mock<ICatalogService>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDistributionByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => Package(id));
        catalog.Setup(x => x.GetPackageDownloadProvenanceAsync(It.IsAny<DistroPackage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DistroPackage package, CancellationToken _) => provenance(package.Id));
        return catalog;
    }

    private static DistroPackage Package(string id = "ubuntu") => new()
    {
        Id = id, Name = "Ubuntu", Version = "24.04", DownloadUrl = "https://example.test/ubuntu", Sha256 = new string('a', 64), FileSize = 4,
    };

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (condition()) return;
            await Task.Delay(50);
        }

        Assert.True(condition(), "Timed out waiting for the expected asynchronous state.");
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }

        Assert.True(await condition(), "Timed out waiting for the expected asynchronous state.");
    }

    public enum DownloadMode { Cancelled, SizeMismatch, HashMismatch, Fail }

    private sealed class ControlledDownload : IDownloadService
    {
        private readonly DownloadMode _mode;
        public ControlledDownload(DownloadMode mode = DownloadMode.Cancelled) => _mode = mode;
        public TaskCompletionSource<string> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> DownloadFileAsync(string url, string destination, IProgress<(long BytesRead, long TotalBytes)>? progress = null, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, _mode == DownloadMode.SizeMismatch ? "bad" : "good", CancellationToken.None);
            Started.TrySetResult(destination);
            try
            {
                if (_mode == DownloadMode.Cancelled) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return _mode != DownloadMode.Fail;
            }
            finally { Finished.TrySetResult(true); }
        }

        public Task<bool> VerifyChecksumAsync(string filePath, string expectedHash, CancellationToken cancellationToken = default) => Task.FromResult(_mode != DownloadMode.HashMismatch);
        public Task<long> GetRemoteFileSizeAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(4L);
        public void Cancel() => Finished.TrySetResult(true);
    }
}
