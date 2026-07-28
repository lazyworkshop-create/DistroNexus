namespace DistroNexus.Tests.ViewModels;

public sealed class PackageDownloadDesktopBoundaryTests
{
    [Theory]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs")]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs")]
    [InlineData("src/Client/DistroNexus.Desktop/App.xaml.cs")]
    [InlineData("src/Client/DistroNexus.Desktop/MainWindow.xaml")]
    public void DesktopPackageDownloadSurface_DoesNotReferenceLegacyTaskOrTransferServices(string relativePath)
    {
        var source = ReadSource(relativePath);

        Assert.DoesNotContain("IDownloadTaskManager", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IDownloadService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("download:DownloadTask", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageManager_UsesOnlyTokenizedPackageJobMutations()
    {
        var source = ReadSource("src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs");

        Assert.Contains("_moduleClient.PreviewPackageDownloadJobStartAsync(package.Id)", source, StringComparison.Ordinal);
        Assert.Contains("_moduleClient.StartPackageDownloadJobAsync(preview.PreviewToken)", source, StringComparison.Ordinal);
        Assert.Contains("_moduleClient.PreviewPackageDownloadJobActionAsync(job.JobId, \"cancel\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetPackageCachePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("destinationPath", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainShell_UsesBoundedJobDtosAndTokenizedActions()
    {
        var source = ReadSource("src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs");

        Assert.Contains("GetPackageDownloadJobsAsync", source, StringComparison.Ordinal);
        Assert.Contains("PreviewPackageDownloadJobActionAsync", source, StringComparison.Ordinal);
        Assert.Contains("ExecutePackageDownloadJobActionAsync", source, StringComparison.Ordinal);
        Assert.Contains("ObservableCollection<PackageDownloadJob>", source, StringComparison.Ordinal);
        Assert.Contains("private async Task CancelDownloadAsync(string? jobId) => await ExecuteDownloadActionAsync(jobId, \"cancel\")", source, StringComparison.Ordinal);
        Assert.Contains("private async Task RetryDownloadAsync(string? jobId) => await ExecuteDownloadActionAsync(jobId, \"retry\")", source, StringComparison.Ordinal);
        Assert.Contains("await ExecuteDownloadActionAsync(job.JobId, \"clear\")", source, StringComparison.Ordinal);
        Assert.Contains("DownloadJobs.Clear()", source, StringComparison.Ordinal);
        Assert.Contains("foreach (var job in jobs) DownloadJobs.Add(job)", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
