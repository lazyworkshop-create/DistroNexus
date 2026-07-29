using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public class DistributionConfigurationServiceTests
{
    [Fact]
    public async Task Read_UsesFixedCatBoundaryAndProjectsUnknownKeys()
    {
        var runner = new RecordingRunner(OkDocument("[boot]\nsystemd=true\n[custom]\nx=y\n"));
        var result = await new DistributionConfigurationService(runner).ReadAsync("Ubuntu Dev");
        Assert.Equal("true", result.Settings.Values["boot.systemd"]); Assert.Equal(1, result.UnknownKeyCount);
        Assert.Equal("wsl.exe", runner.Requests.Single().FileName);
        Assert.Equal(["--distribution", "Ubuntu Dev", "--exec", "/bin/sh"], runner.Requests.Single().Arguments.Take(4));
    }

    [Fact]
    public async Task Save_DefaultUserRequiresGetentAndNeverInterpolatesValueIntoCommand()
    {
        var original = "[user]\ndefault=old\n";
        var runner = new RecordingRunner(
            OkDocument(original), // caller read
            OkDocument(original), // optimistic re-read
            new(2, "", "not found", TimeSpan.Zero, false, false, false, 1));
        var service = new DistributionConfigurationService(runner); var read = await service.ReadAsync("Ubuntu");
        var ex = await Assert.ThrowsAsync<ConfigurationValidationException>(() => service.SaveAsync("Ubuntu",
            new Dictionary<string, string?> { ["user.default"] = "missing-user" }, read.Fingerprint));
        Assert.Contains(ex.Diagnostics, d => d.Code == "config.userNotFound");
        Assert.Equal(["--distribution", "Ubuntu", "--exec", "/usr/bin/getent", "passwd", "missing-user"], runner.Requests[2].Arguments);
    }

    [Theory]
    [InlineData("bad;name")]
    [InlineData("")]
    public async Task Read_RejectsUnsafeDistributionNames(string name)
    {
        var runner = new RecordingRunner();
        await Assert.ThrowsAsync<ArgumentException>(() => new DistributionConfigurationService(runner).ReadAsync(name));
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task Read_Base64ProtocolPreservesBomMixedNewlinesAndNonAscii()
    {
        var bytes = System.Text.Encoding.UTF8.Preamble.ToArray().Concat(System.Text.Encoding.UTF8.GetBytes("# 中文\r\n[boot]\nsystemd=true\r\n")).ToArray();
        var runner = new RecordingRunner(Ok("DNX_DATA\n" + Convert.ToBase64String(bytes) + "\n"));
        var result = await new DistributionConfigurationService(runner).ReadAsync("Ubuntu");
        Assert.Equal(bytes, result.Source.ToBytes());
    }

    [Theory]
    [InlineData(true, false, false, "config.read.timeout")]
    [InlineData(false, true, false, "config.read.truncated")]
    [InlineData(false, false, true, "config.read.start")]
    public async Task Read_MapsTransportFailuresWithoutParsingLocalizedStderr(bool timeout, bool truncated, bool startFailed, string code)
    {
        var result = new ProcessResult(startFailed ? null : 0, "", "任意本地化错误", TimeSpan.Zero, timeout, false, truncated, null,
            startFailed ? ProcessFailureKind.StartFailed : ProcessFailureKind.None);
        var ex = await Assert.ThrowsAsync<ConfigurationTransportException>(() => new DistributionConfigurationService(new RecordingRunner(result)).ReadAsync("Ubuntu"));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task Save_PassesExpectedFingerprintToHelperAndMapsBoundaryConflict()
    {
        const string original = "[boot]\nsystemd=false\n";
        var runner = new RecordingRunner(OkDocument(original), OkDocument(original),
            new ProcessResult(73, "DNX_CONFLICT\n", "", TimeSpan.Zero, false, false, false, 1));
        var service = new DistributionConfigurationService(runner); var read = await service.ReadAsync("Ubuntu");
        await Assert.ThrowsAsync<ConfigurationConflictException>(() => service.SaveAsync("Ubuntu",
            new Dictionary<string, string?> { ["boot.systemd"] = "true" }, read.Fingerprint));
        Assert.Equal(read.Fingerprint, runner.Requests[^1].Arguments[^1]);
        Assert.DoesNotContain("systemd=true", runner.Requests[^1].Arguments);
    }

    [Fact]
    public async Task Save_ReturnsHelperBackupMetadataAndKeepsContentOutOfArguments()
    {
        const string original = "[boot]\nsystemd=false\n";
        var runner = new RecordingRunner(OkDocument(original), OkDocument(original), Ok("/etc/wsl.conf.distronexus.20260712.bak\n"));
        var service = new DistributionConfigurationService(runner); var read = await service.ReadAsync("Ubuntu");
        var saved = await service.SaveAsync("Ubuntu", new Dictionary<string, string?> { ["boot.systemd"] = "true" }, read.Fingerprint);
        Assert.Equal("/etc/wsl.conf.distronexus.20260712.bak", saved.BackupPath);
        Assert.Equal(RestartScope.Instance, saved.RestartScope);
        Assert.DoesNotContain("systemd=true", runner.Requests[^1].Arguments);
    }

    [Theory]
    [InlineData(1, "config.write.failed")]
    [InlineData(null, "config.write.start")]
    public async Task Save_HelperFailuresAreSurfacedWithoutReportingBackup(int? exitCode, string code)
    {
        const string original = "[boot]\nsystemd=false\n";
        var failure = new ProcessResult(exitCode, "", "failed", TimeSpan.Zero, false, false, false, 1,
            exitCode is null ? ProcessFailureKind.StartFailed : ProcessFailureKind.None);
        var runner = new RecordingRunner(OkDocument(original), OkDocument(original), failure);
        var service = new DistributionConfigurationService(runner); var read = await service.ReadAsync("Ubuntu");
        var ex = await Assert.ThrowsAsync<ConfigurationTransportException>(() => service.SaveAsync("Ubuntu", new Dictionary<string, string?> { ["boot.systemd"] = "true" }, read.Fingerprint));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task ConcurrentSaves_SerializesHelperWritesAndSurfacesSecondBoundaryConflict()
    {
        const string original = "[boot]\nsystemd=false\n";
        var runner = new SerializingRunner(original); var service = new DistributionConfigurationService(runner);
        var read = await service.ReadAsync("Ubuntu");
        var first = service.SaveAsync("Ubuntu", new Dictionary<string, string?> { ["boot.systemd"] = "true" }, read.Fingerprint);
        await runner.FirstWriteEntered.Task;
        var second = service.SaveAsync("Ubuntu", new Dictionary<string, string?> { ["boot.systemd"] = "true" }, read.Fingerprint);
        await Task.Delay(25); Assert.Equal(1, runner.WriteCalls);
        runner.ReleaseFirstWrite.TrySetResult();
        await first;
        await Assert.ThrowsAsync<ConfigurationConflictException>(() => second);
        Assert.Equal(2, runner.WriteCalls);
    }

    private static ProcessResult Ok(string output) => new(0, output, "", TimeSpan.Zero, false, false, false, 1);
    private static ProcessResult OkDocument(string content) => Ok("DNX_DATA\n" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)) + "\n");
    private sealed class RecordingRunner(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        { Requests.Add(request); return Task.FromResult(_results.Dequeue()); }
    }

    private sealed class SerializingRunner(string content) : IProcessRunner
    {
        public TaskCompletionSource FirstWriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int WriteCalls { get; private set; }
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            if (!request.Arguments.Contains("--user")) return OkDocument(content);
            WriteCalls++;
            if (WriteCalls == 1)
            {
                FirstWriteEntered.TrySetResult();
                await ReleaseFirstWrite.Task.WaitAsync(cancellationToken);
                return Ok("/etc/wsl.conf.backup\n");
            }
            return new ProcessResult(73, "DNX_CONFLICT\n", "", TimeSpan.Zero, false, false, false, 1);
        }
    }
}
