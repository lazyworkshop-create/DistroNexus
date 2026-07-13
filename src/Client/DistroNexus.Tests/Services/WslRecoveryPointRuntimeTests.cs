using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class WslRecoveryPointRuntimeTests
{
    [Theory]
    [InlineData(RecoveryPointFormat.Tar, "--export|Ubuntu|C:\\safe\\payload.partial")]
    [InlineData(RecoveryPointFormat.Vhdx, "--export|Ubuntu|C:\\safe\\payload.partial|--vhd")]
    public async Task Export_UsesTypedArguments(RecoveryPointFormat format, string expected)
    {
        var runner = new RecordingRunner();
        var runtime = new WslRecoveryPointRuntime(runner, new NoCapabilities());

        await runtime.ExportAsync("Ubuntu", @"C:\safe\payload.partial", format);

        Assert.Equal(expected.Split('|'), runner.Requests.Single().Arguments);
    }

    [Theory]
    [InlineData(RecoveryPointFormat.Tar, "--import|Clone|C:\\safe\\target|C:\\safe\\instance.tar")]
    [InlineData(RecoveryPointFormat.Vhdx, "--import|Clone|C:\\safe\\target|C:\\safe\\instance.vhdx|--vhd")]
    public async Task ManagedImport_UsesTypedArguments(RecoveryPointFormat format, string expected)
    {
        var runner = new RecordingRunner();
        var runtime = new WslRecoveryPointRuntime(runner, new NoCapabilities());

        await runtime.ImportAsync("0123456789abcdef0123456789abcdef", "Clone", format == RecoveryPointFormat.Tar ? @"C:\safe\instance.tar" : @"C:\safe\instance.vhdx", @"C:\safe\target", format, importInPlace: false);

        Assert.Equal(expected.Split('|'), runner.Requests.Single().Arguments);
    }

    [Fact]
    public async Task ImportInPlace_EmptyManagedTarget_UsesDirectTypedArguments()
    {
        var runner = new RecordingRunner();
        var runtime = new WslRecoveryPointRuntime(runner, new NoCapabilities());

        await runtime.ImportAsync("0123456789abcdef0123456789abcdef", "Clone", @"C:\safe\instance.vhdx", "", RecoveryPointFormat.Vhdx, importInPlace: true);

        Assert.Equal(["--import-in-place", "Clone", @"C:\safe\instance.vhdx"], runner.Requests.Single().Arguments);
    }

    [Fact]
    public async Task AdapterBoundary_HasNoAutomaticUnregisterOperation()
    {
        Assert.DoesNotContain(typeof(IRecoveryPointRuntime).GetMethods(), method => method.Name == "CleanupOwnedImportAsync");
        Assert.DoesNotContain(typeof(WslRecoveryPointRuntime).GetMethods(), method => method.Name == "CleanupOwnedImportAsync");
    }

    [Fact]
    public async Task GetSource_StoppedDistributionNeverRunsFilesystemProbe()
    {
        var runner = new RecordingRunner();
        var runtime = new WslRecoveryPointRuntime(runner, new Capabilities());

        var source = await runtime.GetSourceAsync("Ubuntu");

        Assert.False(source.IsRunning);
        Assert.Equal(1024L * 1024 * 1024, source.EstimatedBytes);
        Assert.Single(runner.Requests);
        Assert.Equal(["--list", "--running", "--quiet"], runner.Requests[0].Arguments);
    }

    private sealed class RecordingRunner(params string[] registered) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var output = request.Arguments.Contains("--list") ? string.Join(Environment.NewLine, registered) : "";
            return Task.FromResult(new ProcessResult(0, output, "", TimeSpan.Zero, false, false, false, 1));
        }
    }
    private sealed class NoCapabilities : IPlatformCapabilityService
    {
        public Task<PlatformCapabilitySnapshot> GetHostSnapshotAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InstanceCapabilitySnapshot> GetInstanceSnapshotAsync(string instanceName, bool forceRefresh = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void InvalidateHostCapabilities() { } public void InvalidateOptionalDependency(CapabilityId dependency) { } public void InvalidateInstance(string instanceName) { } public void InvalidateAll() { }
    }
    private sealed class Capabilities : IPlatformCapabilityService
    {
        public Task<PlatformCapabilitySnapshot> GetHostSnapshotAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null),
                new Dictionary<CapabilityId, CapabilityResult>(), new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow));
        public Task<InstanceCapabilitySnapshot> GetInstanceSnapshotAsync(string instanceName, bool forceRefresh = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new InstanceCapabilitySnapshot(new(instanceName, 2, null, null, null, null), new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow));
        public void InvalidateHostCapabilities() { } public void InvalidateOptionalDependency(CapabilityId dependency) { } public void InvalidateInstance(string instanceName) { } public void InvalidateAll() { }
    }
}
