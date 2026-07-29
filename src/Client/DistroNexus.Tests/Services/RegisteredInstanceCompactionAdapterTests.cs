using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.WorkspaceBridge;

namespace DistroNexus.Tests.Services;

public sealed class RegisteredInstanceCompactionAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dn-compact-adapter-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunningInstance_TrimsBeforeStopThenRestartsAfterFixedDiskpart()
    {
        Directory.CreateDirectory(_root);
        var vhdx = Path.Combine(_root, "ext4.vhdx"); await File.WriteAllBytesAsync(vhdx, new byte[32]);
        var calls = new List<ProcessRequest>();
        var runner = new FakeRunner(calls, running: "Ubuntu");
        var adapter = Create(runner, vhdx);
        var state = await adapter.GetAsync("Ubuntu");

        var result = await adapter.CompactAsync(state!);

        Assert.True(result.Succeeded);
        Assert.True(result.Restarted);
        var trim = calls.FindIndex(request => request.Arguments.Contains("fstrim"));
        var terminate = calls.FindIndex(request => request.Arguments.SequenceEqual(["--terminate", "Ubuntu"]));
        var diskpart = calls.FindIndex(request => request.FileName == "diskpart.exe");
        var start = calls.FindIndex(request => request.Arguments.SequenceEqual(["--distribution", "Ubuntu", "--exec", "echo", "started"]));
        Assert.True(trim < terminate && terminate < diskpart && diskpart < start);
    }

    [Fact]
    public async Task StoppedInstance_NeverStartsOrTrimsBeforeCompaction()
    {
        Directory.CreateDirectory(_root);
        var vhdx = Path.Combine(_root, "ext4.vhdx"); await File.WriteAllBytesAsync(vhdx, new byte[32]);
        var calls = new List<ProcessRequest>();
        var runner = new FakeRunner(calls, running: null);
        var adapter = Create(runner, vhdx);
        var state = await adapter.GetAsync("Ubuntu");

        var result = await adapter.CompactAsync(state!);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(calls, request => request.Arguments.Contains("fstrim") || request.Arguments.Contains("--terminate") || request.Arguments.Contains("started"));
    }

    [Fact]
    public async Task RestartFailure_ReportsManualRecovery()
    {
        Directory.CreateDirectory(_root);
        var vhdx = Path.Combine(_root, "ext4.vhdx"); await File.WriteAllBytesAsync(vhdx, new byte[32]);
        var calls = new List<ProcessRequest>();
        var runner = new FakeRunner(calls, "Ubuntu", failStart: true);
        var adapter = Create(runner, vhdx);
        var state = await adapter.GetAsync("Ubuntu");

        var result = await adapter.CompactAsync(state!);

        Assert.False(result.Succeeded);
        Assert.Equal("Lifecycle.CompactionRestartRecoveryRequired", result.OutcomeCode);
        Assert.Equal("ManualRecoveryRequired", result.RecoveryAction);
    }

    [Fact]
    public async Task CancellationAfterStop_RestoresTheOriginallyRunningInstance()
    {
        Directory.CreateDirectory(_root);
        var vhdx = Path.Combine(_root, "ext4.vhdx"); await File.WriteAllBytesAsync(vhdx, new byte[32]);
        var calls = new List<ProcessRequest>();
        var runner = new FakeRunner(calls, "Ubuntu", cancelDiskpart: true);
        var adapter = Create(runner, vhdx);
        var state = await adapter.GetAsync("Ubuntu");

        var result = await adapter.CompactAsync(state!);

        Assert.Equal("Lifecycle.Cancelled", result.OutcomeCode);
        Assert.True(result.Restarted);
        Assert.Contains(calls, request => request.Arguments.Contains("started"));
    }

    private RegisteredInstanceCompactionAdapter Create(IProcessRunner runner, string vhdx) => new(runner, name => name == "Ubuntu" ? new("Ubuntu", "instance", vhdx, "vhdx", new FileInfo(vhdx).Length) : null, () => true);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class FakeRunner(List<ProcessRequest> calls, string? running, bool failStart = false, bool cancelDiskpart = false) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            calls.Add(request);
            if (request.Arguments.SequenceEqual(["--list", "--running", "--quiet"])) return Task.FromResult(Result(running ?? ""));
            if (cancelDiskpart && request.FileName == "diskpart.exe") return Task.FromCanceled<ProcessResult>(new CancellationToken(canceled: true));
            if (failStart && request.Arguments.Contains("started")) return Task.FromResult(new ProcessResult(1, "", "", TimeSpan.Zero, false, false, false, null));
            return Task.FromResult(Result(""));
        }
        private static ProcessResult Result(string stdout) => new(0, stdout, "", TimeSpan.Zero, false, false, false, null);
    }
}
