using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace DistroNexus.Tests.Services;

public class MonitoringServiceTests
{
    [Fact]
    public void Parser_ComputesMemoryFilesystemAndProcessSnapshot()
    {
        var sample = MonitoringParser.Parse("cpu  10 0 5 50 0 0 0 0\n__DN_MEM__\nMemTotal: 100 kB\nMemAvailable: 40 kB\nSwapTotal: 20 kB\nSwapFree: 5 kB\n__DN_DISK__\n\n__DN_NET__\n\n__DN_FS__\n/dev/sda 100 30 70 30% /\n__DN_PROC__\n  PID STARTED USER %CPU %MEM COMMAND\n12 Tue Jan 02 12:00:00 2024 root 2.5 1.2 /usr/bin/test\n", DateTimeOffset.UtcNow, null);
        Assert.Equal(60 * 1024, sample.MemoryUsedBytes);
        Assert.Equal(30 * 1024, sample.FilesystemUsedBytes);
        Assert.Single(sample.Processes);
        Assert.Equal(12, sample.Processes[0].Pid);
    }

    [Fact]
    public void Parser_ComputesIoAndNetworkRatesAndThresholdWarnings()
    {
        const string first = "cpu  10 0 5 50 0 0 0 0\n__DN_MEM__\nMemTotal: 100 kB\nMemAvailable: 5 kB\nSwapTotal: 20 kB\nSwapFree: 5 kB\n__DN_DISK__\n8 0 sda 1 0 100 0 1 0 200 0 0 0 0 0\n__DN_NET__\nInter-|   Receive                                                |  Transmit\n face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\neth0: 1000 0 0 0 0 0 0 0 2000 0 0 0 0 0 0 0\n__DN_FS__\n/dev/sda 100 95 5 95% /\n__DN_PROC__\n";
        const string second = "cpu  20 0 10 55 0 0 0 0\n__DN_MEM__\nMemTotal: 100 kB\nMemAvailable: 5 kB\nSwapTotal: 20 kB\nSwapFree: 5 kB\n__DN_DISK__\n8 0 sda 1 0 120 0 1 0 230 0 0 0 0 0\n__DN_NET__\nInter-|   Receive                                                |  Transmit\n face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\neth0: 1200 0 0 0 0 0 0 0 2400 0 0 0 0 0 0 0\n__DN_FS__\n/dev/sda 100 95 5 95% /\n__DN_PROC__\n";
        var at = DateTimeOffset.UtcNow;
        var previous = MonitoringParser.Parse(first, at, null);
        var sample = MonitoringParser.Parse(second, at.AddSeconds(2), previous, new HostResourceLimits(4096, 1024, 2), new MonitoringThresholds(10, 90, 90));
        Assert.Equal(97_280, sample.MemoryUsedBytes);
        Assert.Equal(97_280, sample.FilesystemUsedBytes);
        Assert.Equal(4_096, sample.HostLimits!.MemoryLimitBytes);
        Assert.Equal(5_120, sample.DiskReadBytesPerSecond);
        Assert.Equal(7_680, sample.DiskWriteBytesPerSecond);
        Assert.Equal(100, sample.NetworkReceiveBytesPerSecond);
        Assert.Equal(200, sample.NetworkTransmitBytesPerSecond);
        Assert.Contains(sample.Warnings!, warning => warning.Metric == "filesystem");
    }

    [Fact]
    public async Task Terminate_ReprobesAndNeverEscalatesToKill()
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var runner = new RecordingRunner(
            new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1));
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var preview = await session.PreviewProcessActionAsync(new MonitoredProcess(44, start, "user", 0, 0, "app", []), MonitoringProcessAction.Terminate);
        var result = await session.ExecuteProcessActionAsync(preview);
        Assert.True(result.Succeeded);
        Assert.Equal("Monitor.TermSentProcessStillRunning", result.OutcomeCode);
        Assert.DoesNotContain(runner.Requests.SelectMany(x => x.Arguments), value => value == "KILL");
    }

    [Fact]
    public async Task MonitoringHealthCheck_ProjectsActiveThresholdWarning()
    {
        var registry = new MonitoringWarningRegistry();
        registry.Update("Ubuntu", [new MonitoringWarning("cpu", 95, 90, "CPU use is above the configured threshold.")]);
        var result = await new MonitoringHealthCheck(registry).CheckAsync(new HealthCheckContext(null!, []), default);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("monitor.Ubuntu.cpu", finding.Id);
        Assert.Equal(HealthSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void CreateSession_DoesNotStartStoppedDistribution()
    {
        var runner = new RecordingRunner();
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Stopped" }, TimeSpan.FromSeconds(1));
        Assert.Equal("Monitor.InstanceStopped", session.UnavailableReason);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task StoppedSession_StartIsANoOpAndNeverIssuesProbe()
    {
        var runner = new RecordingRunner();
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Stopped" }, TimeSpan.FromSeconds(1));

        await session.StartAsync();

        Assert.False(session.IsRunning);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task SessionCreatedWhileRunning_DoesNotProbeIfItStopsBeforeStart()
    {
        var instance = new WslInstance { Name = "d", State = "Running" };
        var runner = new RecordingRunner();
        await using var session = new MonitoringService(runner).CreateSession(instance, TimeSpan.FromSeconds(1));
        instance.State = "Stopped";

        await session.StartAsync();

        Assert.False(session.IsRunning);
        Assert.Equal("Monitor.InstanceStopped", session.UnavailableReason);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task StoppedSession_PreservesOfflineVhdxMeasurementWithoutProbe()
    {
        var runner = new RecordingRunner();
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Stopped", Size = 1234 }, TimeSpan.FromSeconds(1));
        var sample = Assert.Single(session.Samples);
        Assert.Equal(1234, sample.VhdxPhysicalBytes);
        Assert.Null(sample.EstimatedReclaimableBytes);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public void Parser_UsesReadOnlyHostVhdxSizeAndMarksReclaimableAsEstimate()
    {
        var sample = MonitoringParser.Parse(ProbeOutput, DateTimeOffset.UtcNow, null, hostVhdxPhysicalBytes: 100 * 1024);
        Assert.Equal(100 * 1024, sample.VhdxPhysicalBytes);
        Assert.Equal(70 * 1024, sample.EstimatedReclaimableBytes);
        Assert.DoesNotContain("vhdxPhysical", sample.UnavailableMetrics.Keys);
    }

    [Fact]
    public void Parser_CollectsBoundedListeningPortsAndDegradesWhenSsIsUnavailable()
    {
        var withPorts = ProbeOutput.Replace("PID STARTED USER %CPU %MEM COMMAND", "PID STARTED USER %CPU %MEM COMMAND\n12 Tue Jan 02 12:00:00 2024 root 2.5 1.2 /usr/bin/test").Replace("__DN_PROC__", "__DN_PORTS__\ntcp LISTEN 0 4096 127.0.0.1:8080 0.0.0.0:* users:((\"test\",pid=12,fd=3))\nudp UNCONN 0 0 *:53 0.0.0.0:*\n__DN_PROC__");
        var sample = MonitoringParser.Parse(withPorts, DateTimeOffset.UtcNow, null);
        Assert.Collection(sample.ListeningPorts!, p => { Assert.Equal("TCP", p.Protocol); Assert.Equal(8080, p.Port); }, p => { Assert.Equal("UDP", p.Protocol); Assert.Equal(53, p.Port); });
        Assert.Equal([8080], sample.Processes.Single().ListeningPorts);

        var unavailable = MonitoringParser.Parse(ProbeOutput.Replace("__DN_PROC__", "__DN_PORTS__\n__DN_PORTS_UNAVAILABLE__\n__DN_PROC__"), DateTimeOffset.UtcNow, null);
        Assert.Empty(unavailable.ListeningPorts!);
        Assert.Equal("unavailable", unavailable.UnavailableMetrics["listeningPorts"]);
    }

    [Fact]
    public async Task Session_StopsAfterRuntimeReportsInstanceStoppedAndDoesNotRestartItself()
    {
        var instance = new WslInstance { Name = "d", State = "Running", Size = 100 * 1024 };
        var runner = new StateTransitionRunner();
        var nextTick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = new MonitoringSession(instance, TimeSpan.FromSeconds(1), runner, new TestConfigurationService(), null, cancellationToken => new ValueTask<bool>(nextTick.Task.WaitAsync(cancellationToken)));
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SampleAvailable += (_, sample) =>
        {
            if (sample.UnavailableMetrics.TryGetValue("runtime", out var reason) && reason == "Monitor.InstanceStopped")
                stopped.TrySetResult();
        };

        await session.StartAsync();
        nextTick.TrySetResult(true);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await session.StopAsync();

        Assert.False(session.IsRunning);
        Assert.Equal("Monitor.InstanceStopped", session.UnavailableReason);
        Assert.Equal(3, runner.Requests.Count);
    }

    [Fact]
    public async Task Kill_RequiresTermThenReprobeEligibility()
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var session = new MonitoringService(new RecordingRunner()).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var process = new MonitoredProcess(44, start, "user", 0, 0, "app", []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PreviewProcessActionAsync(process, MonitoringProcessAction.Kill));
    }

    [Fact]
    public async Task Stop_ClearsHealthWarningProjection()
    {
        var registry = new MonitoringWarningRegistry();
        registry.Update("d", [new MonitoringWarning("cpu", 99, 90, "test")]);
        await using var session = new MonitoringService(new RecordingRunner(), new TestConfigurationService(), registry)
            .CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        await session.StopAsync();
        Assert.Empty(registry.ActiveWarnings);
    }

    [Fact]
    public async Task ProcessAction_RejectsPidReuseBeforeSignal()
    {
        var runner = new RecordingRunner(new ProcessResult(0, "Wed Jan 03 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1));
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var process = new MonitoredProcess(44, DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks, "user", 0, 0, "app", []);
        var preview = await session.PreviewProcessActionAsync(process, MonitoringProcessAction.Terminate);
        var result = await session.ExecuteProcessActionAsync(preview);
        Assert.False(result.Succeeded); Assert.Equal("Monitor.ProcessIdentityChanged", result.OutcomeCode);
        Assert.Equal(3, runner.Requests.Count);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("TERM"));
    }

    [Fact]
    public async Task ProcessAction_UsesRunningStateThenExactIdentityImmediatelyBeforeSignal()
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var runner = new RecordingRunner(new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1), new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1), new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1));
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var preview = await session.PreviewProcessActionAsync(new MonitoredProcess(44, start, "user", 0, 0, "app", []), MonitoringProcessAction.Renice);

        var result = await session.ExecuteProcessActionAsync(preview);

        Assert.True(result.Succeeded);
        Assert.Collection(runner.Requests,
            request => Assert.Equal(["--list", "--running", "--quiet"], request.Arguments),
            request => Assert.Equal(["--list", "--running", "--quiet"], request.Arguments),
            request => Assert.Equal(["--distribution", "d", "--exec", "ps", "-o", "lstart=", "-p", "44"], request.Arguments),
            request => Assert.Equal(["--distribution", "d", "--exec", "renice", "5", "-p", "44"], request.Arguments));
    }

    [Theory]
    [MemberData(nameof(UnhealthySignalResults))]
    public async Task Renice_DoesNotReportSentWhenSignalResultIsUnhealthy(ProcessResult unhealthy)
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var runner = new RecordingRunner(new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1), unhealthy);
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var preview = await session.PreviewProcessActionAsync(new MonitoredProcess(44, start, "user", 0, 0, "app", []), MonitoringProcessAction.Renice);

        var result = await session.ExecuteProcessActionAsync(preview);

        Assert.False(result.Succeeded);
        Assert.Equal("Monitor.ProcessSignalFailed", result.OutcomeCode);
        Assert.DoesNotContain("Sent", result.OutcomeCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Terminated", result.OutcomeCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Terminate_DoesNotProvideKillGuidanceWhenSignalTimesOut()
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var runner = new RecordingRunner(
            new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "", "timed out", TimeSpan.Zero, true, false, false, 1));
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var preview = await session.PreviewProcessActionAsync(new MonitoredProcess(44, start, "user", 0, 0, "app", []), MonitoringProcessAction.Terminate);

        var result = await session.ExecuteProcessActionAsync(preview);

        Assert.False(result.Succeeded);
        Assert.Equal("Monitor.ProcessSignalFailed", result.OutcomeCode);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("KILL"));
        Assert.Equal(4, runner.Requests.Count);
    }

    [Fact]
    public async Task Kill_DoesNotReportSentWhenSignalIsCancelled()
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var process = new MonitoredProcess(44, start, "user", 0, 0, "app", []);
        var runner = new RecordingRunner(
            new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "", "cancelled", TimeSpan.Zero, false, true, false, 1));
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var termPreview = await session.PreviewProcessActionAsync(process, MonitoringProcessAction.Terminate);
        var term = await session.ExecuteProcessActionAsync(termPreview);
        Assert.Equal("Monitor.TermSentProcessStillRunning", term.OutcomeCode);
        var killPreview = await session.PreviewProcessActionAsync(process, MonitoringProcessAction.Kill);

        var result = await session.ExecuteProcessActionAsync(killPreview);

        Assert.False(result.Succeeded);
        Assert.Equal("Monitor.ProcessSignalFailed", result.OutcomeCode);
        Assert.Contains(runner.Requests, request => request.Arguments.Contains("KILL"));
    }

    [Fact]
    public async Task ConcurrentStarts_AreSingleFlightAndCreateOnlyOneProbeLoop()
    {
        var runner = new BlockingProbeRunner();
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => session.StartAsync()));
        await runner.ProbeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, runner.ExecCalls);
        Assert.Equal(1, runner.MaxConcurrentExec);
        runner.Release();
        await session.StopAsync();
    }

    [Fact]
    public async Task StreamAsync_ConsumesPublishedSampleAndHonorsCancellation()
    {
        var runner = new RecordingRunner(new ProcessResult(0, ProbeOutput, "", TimeSpan.Zero, false, false, false, 1));
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = session.StreamAsync(cancellation.Token).GetAsyncEnumerator();

        await session.StartAsync();
        Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.NotEqual(default, enumerator.Current.CapturedAt);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync().AsTask());
    }

    [Fact]
    public async Task ProcessAction_DoesNotIssueAnyWslCommandAfterInstanceStops()
    {
        var instance = new WslInstance { Name = "d", State = "Running" };
        var runner = new RecordingRunner();
        var session = new MonitoringService(runner).CreateSession(instance, TimeSpan.FromSeconds(1));
        var process = new MonitoredProcess(44, 1, "user", 0, 0, "app", []);
        var preview = await session.PreviewProcessActionAsync(process, MonitoringProcessAction.Terminate);
        instance.State = "Stopped";

        var result = await session.ExecuteProcessActionAsync(preview);

        Assert.False(result.Succeeded);
        Assert.Equal("Monitor.InstanceStopped", result.OutcomeCode);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("--exec"));
    }

    [Fact]
    public async Task CachedRunningInstance_ThatStoppedExternally_NeverExecutesMonitoringProbe()
    {
        var runner = new RuntimeStateRunner(Array.Empty<string>());
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var published = new TaskCompletionSource<MonitoringSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SampleAvailable += (_, sample) => published.TrySetResult(sample);

        await session.StartAsync();
        var unavailable = await published.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("Monitor.InstanceStopped", unavailable.UnavailableMetrics["runtime"]);
        Assert.False(session.IsRunning);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("--exec"));
    }

    [Fact]
    public async Task CachedRunningInstance_ThatStopsBeforeAction_NeverExecutesSignalOrIdentityProbe()
    {
        var runner = new RuntimeStateRunner(["d"], []);
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var preview = await session.PreviewProcessActionAsync(new MonitoredProcess(44, 1, "user", 0, 0, "app", []), MonitoringProcessAction.Terminate);

        var result = await session.ExecuteProcessActionAsync(preview);

        Assert.False(result.Succeeded);
        Assert.Equal("Monitor.InstanceStopped", result.OutcomeCode);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("--exec"));
    }

    [Fact]
    public async Task SlowTick_CoalescesAndNeverOverlapsLinuxProbe()
    {
        var runner = new BlockingProbeRunner();
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        await session.StartAsync();
        await runner.ProbeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        Assert.Equal(1, runner.ExecCalls);
        Assert.Equal(1, runner.MaxConcurrentExec);

        runner.Release();
        await session.StopAsync();
    }

    [Fact]
    public async Task Samples_EvictOldestAfterThreeHundredEntries()
    {
        var runner = new SequentialProbeRunner();
        var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        var probe = session.GetType().GetMethod("ProbeOnceAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        for (var index = 0; index < 301; index++)
            Assert.True(await (Task<bool>)probe.Invoke(session, [CancellationToken.None])!);

        Assert.Equal(300, session.Samples.Count);
        // The probe fixture contributes 55 idle/user ticks; entry 1 was evicted after 301 samples.
        Assert.Equal(57, session.Samples[0].CounterState!["cpu_total"]);
    }

    [Fact]
    public void Parser_MarksCounterResetRatherThanPublishingNegativeRates()
    {
        const string first = "cpu  100 0 0 0 0 0 0 0\n__DN_MEM__\nMemTotal: 10 kB\nMemAvailable: 5 kB\n__DN_DISK__\n8 0 sda 1 0 100 0 1 0 100 0 0 0 0 0\n__DN_NET__\nInter-| x\n face |x\neth0: 100 0 0 0 0 0 0 0 100 0 0 0 0 0 0 0\n__DN_FS__\n/dev/sda 10 5 5 50% /\n__DN_PROC__\n";
        const string reset = "cpu  10 0 0 0 0 0 0 0\n__DN_MEM__\nMemTotal: 10 kB\nMemAvailable: 5 kB\n__DN_DISK__\n8 0 sda 1 0 10 0 1 0 10 0 0 0 0 0\n__DN_NET__\nInter-| x\n face |x\neth0: 10 0 0 0 0 0 0 0 10 0 0 0 0 0 0 0\n__DN_FS__\n/dev/sda 10 5 5 50% /\n__DN_PROC__\n";
        var at = DateTimeOffset.UtcNow;
        var before = MonitoringParser.Parse(first, at, null);
        var after = MonitoringParser.Parse(reset, at.AddSeconds(1), before);

        Assert.Null(after.DiskReadBytesPerSecond);
        Assert.Null(after.NetworkReceiveBytesPerSecond);
        Assert.Equal("counter reset", after.UnavailableMetrics["disk"]);
        Assert.Equal("counter reset", after.UnavailableMetrics["network"]);
    }

    [Fact]
    public void AddHealthCenter_ComposesMonitoringWarningRegistryWithoutDesktopRegistration()
    {
        var services = new ServiceCollection();
        services.AddHealthCenter();
        using var provider = services.BuildServiceProvider();

        var source = provider.GetRequiredService<IMonitoringWarningSource>();
        var sink = provider.GetRequiredService<IMonitoringWarningSink>();

        Assert.Same(source, sink);
        Assert.IsType<MonitoringWarningRegistry>(source);
    }

    [Fact]
    public async Task Session_CoalescesAndBoundsSamples()
    {
        var runner = new RecordingRunner(new ProcessResult(0, ProbeOutput, "", TimeSpan.Zero, false, false, false, 1));
        await using var session = new MonitoringService(runner).CreateSession(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
        await session.StartAsync();
        await session.StopAsync();
        Assert.InRange(session.Samples.Count, 1, 300);
        Assert.Equal(2, runner.Requests.Count);
    }

    [Fact]
    public async Task Automation_StoppedInstanceReturnsOnlyOfflineSnapshotAndNeverStartsWsl()
    {
        var runner = new RecordingRunner();
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-monitoring-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var automation = new MonitoringAutomationService(new MonitoringService(runner), runner, root);
            var result = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Stopped", Size = 123 }, TimeSpan.FromSeconds(1));
            Assert.NotEmpty(result.SnapshotToken);
            Assert.Empty(result.Sample.Processes);
            Assert.Equal(123, result.Sample.VhdxPhysicalBytes);
            Assert.Empty(runner.Requests);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Automation_PidIdentityDriftRejectsPreviewBeforeSignal()
    {
        var start = DateTime.ParseExact("Tue Jan 02 12:00:00 2024", "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture).Ticks;
        var runner = new AutomationRunner(start, drift: true);
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-monitoring-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var automation = new MonitoringAutomationService(new MonitoringService(runner), runner, root);
            var snapshot = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            var preview = await automation.PreviewAsync(snapshot.SnapshotToken, 44, MonitoringProcessAction.Terminate);
            var result = await automation.ExecuteAsync(preview.PreviewToken);
            Assert.False(result.Succeeded);
            Assert.Equal("Monitor.ProcessIdentityChanged", result.OutcomeCode);
            Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("TERM"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Automation_ExpiredSnapshotAndReplayPreviewHaveStableCodes()
    {
        var runner = new AutomationRunner(0, drift: false);
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-monitoring-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var automation = new MonitoringAutomationService(new MonitoringService(runner), runner, root);
            var snapshot = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            ExpireGrant(root, snapshot.SnapshotToken);
            var expired = await Assert.ThrowsAsync<InvalidOperationException>(() => automation.PreviewAsync(snapshot.SnapshotToken, 44, MonitoringProcessAction.Renice));
            Assert.Equal("Monitor.GrantExpired", expired.Message);

            var fresh = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            var preview = await automation.PreviewAsync(fresh.SnapshotToken, 44, MonitoringProcessAction.Renice);
            Assert.True((await automation.ExecuteAsync(preview.PreviewToken)).Succeeded);
            var replay = await Assert.ThrowsAsync<InvalidOperationException>(() => automation.ExecuteAsync(preview.PreviewToken));
            Assert.Equal("Monitor.PreviewReplayed", replay.Message);
            ExpireGrant(root, preview.PreviewToken);
            await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "monitoring-grants"), "*.used"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Automation_ParallelPreviewConsumptionSignalsAtMostOnce()
    {
        var runner = new AutomationRunner(0, drift: false);
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-monitoring-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var automation = new MonitoringAutomationService(new MonitoringService(runner), runner, root);
            var snapshot = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            var preview = await automation.PreviewAsync(snapshot.SnapshotToken, 44, MonitoringProcessAction.Renice);
            var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { try { return (await automation.ExecuteAsync(preview.PreviewToken)).Succeeded; } catch (InvalidOperationException ex) { return ex.Message == "Monitor.PreviewReplayed"; } }));
            Assert.Equal(2, outcomes.Count(x => x));
            Assert.Equal(1, runner.Requests.Count(r => r.Arguments.Contains("renice")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Automation_KillRequiresSuccessfulTermAndNewSnapshot()
    {
        var runner = new AutomationRunner(0, drift: false);
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-monitoring-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var automation = new MonitoringAutomationService(new MonitoringService(runner), runner, root);
            var first = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => automation.PreviewAsync(first.SnapshotToken, 44, MonitoringProcessAction.Kill));
            Assert.Equal("Monitor.KillRequiresTermAndReprobe", blocked.Message);
            var term = await automation.PreviewAsync(first.SnapshotToken, 44, MonitoringProcessAction.Terminate);
            Assert.Equal("Monitor.TermSentProcessStillRunning", (await automation.ExecuteAsync(term.PreviewToken)).OutcomeCode);
            var second = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
            var kill = await automation.PreviewAsync(second.SnapshotToken, 44, MonitoringProcessAction.Kill);
            Assert.True((await automation.ExecuteAsync(kill.PreviewToken)).Succeeded);
            Assert.Single(runner.Requests.Where(r => r.Arguments.Contains("KILL")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Automation_PublicProjectionRedactsAndBoundsProcessData()
    {
        var processes = Enumerable.Range(2, 25).Select(pid => new MonitoredProcess(pid, pid, "u", 0, 0, "bad\r\n\t" + new string('x', 300), Enumerable.Range(1, 20).ToArray())).ToArray();
        var ports = Enumerable.Range(1, 130).Select(port => new ListeningPort("TCP", "\t127.0.0.1", port)).ToArray();
        var sample = new MonitoringSample(DateTimeOffset.UtcNow, null, null, null, null, null, null, null, null, null, null, null, null, null, processes, new Dictionary<string, string>(), CounterState: new Dictionary<string, long> { ["secret"] = 1 }, ListeningPorts: ports);
        var method = typeof(MonitoringAutomationService).GetMethod("Sanitize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var publicSample = (MonitoringSample)method.Invoke(null, [sample])!;
        Assert.Equal(20, publicSample.Processes.Count);
        Assert.Equal(128, publicSample.ListeningPorts!.Count);
        Assert.All(publicSample.Processes, process => { Assert.True(process.Command.Length <= 256); Assert.DoesNotContain(process.Command, char.IsControl); Assert.True(process.ListeningPorts.Count <= 16); });
        Assert.Null(publicSample.CounterState);
    }

    [Fact]
    public async Task Automation_TombstonesCountTowardBoundedGrantCapacity()
    {
        var runner = new AutomationRunner(0, drift: false);
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-monitoring-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var automation = new MonitoringAutomationService(new MonitoringService(runner), runner, root);
            for (var i = 0; i < 64; i++)
            {
                var snapshot = await automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1));
                var preview = await automation.PreviewAsync(snapshot.SnapshotToken, 44, MonitoringProcessAction.Renice);
                await automation.ExecuteAsync(preview.PreviewToken);
            }
            var full = await Assert.ThrowsAsync<InvalidOperationException>(() => automation.GetSnapshotAsync(new WslInstance { Name = "d", State = "Running" }, TimeSpan.FromSeconds(1)));
            Assert.Equal("Monitor.GrantInvalid", full.Message);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private const string ProbeOutput = "cpu  10 0 5 50 0 0 0 0\n__DN_MEM__\nMemTotal: 100 kB\nMemAvailable: 40 kB\nSwapTotal: 20 kB\nSwapFree: 5 kB\n__DN_DISK__\n\n__DN_NET__\n\n__DN_FS__\n/dev/sda 100 30 70 30% /\n__DN_PROC__\n  PID STARTED USER %CPU %MEM COMMAND\n";
    public static IEnumerable<object[]> UnhealthySignalResults()
    {
        yield return [new ProcessResult(1, "", "exit failed", TimeSpan.Zero, false, false, false, 1)];
        yield return [new ProcessResult(0, "", "timed out", TimeSpan.Zero, true, false, false, 1)];
        yield return [new ProcessResult(0, "", "cancelled", TimeSpan.Zero, false, true, false, 1)];
        yield return [new ProcessResult(0, "", "truncated", TimeSpan.Zero, false, false, true, 1)];
        yield return [new ProcessResult(null, "", "start failed", TimeSpan.Zero, false, false, false, null, ProcessFailureKind.StartFailed)];
    }
    private sealed class RecordingRunner(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Arguments.SequenceEqual(["--list", "--running", "--quiet"]))
                return Task.FromResult(new ProcessResult(0, "d\n", "", TimeSpan.Zero, false, false, false, 1));
            return Task.FromResult(_results.Count == 0 ? new ProcessResult(0, ProbeOutput, "", TimeSpan.Zero, false, false, false, 1) : _results.Dequeue());
        }
    }
    private sealed class TestConfigurationService : IWslConfigurationService
    {
        public Task<ConfigurationDocument<WslConfigurationSettings>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string>()), LosslessIniDocument.Parse([]), [], 0, "", RestartScope.None, ""));
        public Task<ConfigurationPreview> PreviewAsync(IReadOnlyDictionary<string, string?> values, string expectedFingerprint, IReadOnlySet<string> availableCapabilities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ConfigurationSaveResult> SaveAsync(IReadOnlyDictionary<string, string?> values, string expectedFingerprint, IReadOnlySet<string>? availableCapabilities = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class RuntimeStateRunner(params IReadOnlyList<string>[] states) : IProcessRunner
    {
        private readonly Queue<IReadOnlyList<string>> _states = new(states);
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var names = _states.Count == 0 ? [] : _states.Dequeue();
            return Task.FromResult(new ProcessResult(0, string.Join("\n", names), "", TimeSpan.Zero, false, false, false, 1));
        }
    }
    private sealed class StateTransitionRunner : IProcessRunner
    {
        private int _runningStateChecks;
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Arguments.SequenceEqual(["--list", "--running", "--quiet"]))
                return Task.FromResult(new ProcessResult(0, Interlocked.Increment(ref _runningStateChecks) == 1 ? "d\n" : "", "", TimeSpan.Zero, false, false, false, 1));
            return Task.FromResult(new ProcessResult(0, ProbeOutput, "", TimeSpan.Zero, false, false, false, 1));
        }
    }
    private sealed class BlockingProbeRunner : IProcessRunner
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        public TaskCompletionSource ProbeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ExecCalls { get; private set; }
        public int MaxConcurrentExec { get; private set; }
        public void Release() => _release.TrySetResult();
        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            if (!request.Arguments.Contains("--exec")) return new(0, "d\n", "", TimeSpan.Zero, false, false, false, 1);
            ExecCalls++; var active = Interlocked.Increment(ref _active); MaxConcurrentExec = Math.Max(MaxConcurrentExec, active); ProbeEntered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _active);
            return new(0, ProbeOutput, "", TimeSpan.Zero, false, false, false, 1);
        }
    }
    private sealed class SequentialProbeRunner : IProcessRunner
    {
        private int _number;
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            if (!request.Arguments.Contains("--exec")) return Task.FromResult(new ProcessResult(0, "d\n", "", TimeSpan.Zero, false, false, false, 1));
            var number = Interlocked.Increment(ref _number);
            return Task.FromResult(new ProcessResult(0, ProbeOutput.Replace("cpu  10", $"cpu  {number}"), "", TimeSpan.Zero, false, false, false, 1));
        }
    }
    private sealed class AutomationRunner(long start, bool drift) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Arguments.SequenceEqual(["--list", "--running", "--quiet"])) return Task.FromResult(new ProcessResult(0, "d\n", "", TimeSpan.Zero, false, false, false, 1));
            if (request.Arguments.Contains("ps")) return Task.FromResult(new ProcessResult(0, drift ? "Wed Jan 03 12:00:00 2024" : "Tue Jan 02 12:00:00 2024", "", TimeSpan.Zero, false, false, false, 1));
            const string output = "cpu  10 0 5 50 0 0 0 0\n__DN_MEM__\nMemTotal: 100 kB\nMemAvailable: 40 kB\n__DN_DISK__\n\n__DN_NET__\n\n__DN_FS__\n/dev/sda 100 30 70 30% /\n__DN_PROC__\nPID STARTED USER %CPU %MEM COMMAND\n44 Tue Jan 02 12:00:00 2024 user 0 0 safe\n";
            return Task.FromResult(new ProcessResult(0, output, "", TimeSpan.Zero, false, false, false, 1));
        }
    }
    private static void ExpireGrant(string root, string token)
    {
        var basePath = Path.Combine(root, "monitoring-grants", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
        var path = File.Exists(basePath) ? basePath : basePath + ".used";
        var json = JsonNode.Parse(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser))!.AsObject();
        json["ExpiresAt"] = DateTimeOffset.UtcNow.AddMinutes(-1);
        File.WriteAllBytes(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(json.ToJsonString()), null, DataProtectionScope.CurrentUser));
    }
}
