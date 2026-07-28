using System.Collections.Concurrent;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DistroNexus.Tests.Services;

public class PlatformCapabilityServiceTests
{
    [Fact]
    public async Task HostDependencyProbe_DetectsWindowsTerminalWithoutInvokingIt()
    {
        var runner = new FakeRunner(request => request.FileName == "where.exe"
            ? Ok("C:\\Tools\\" + request.Arguments[0])
            : Ok("WSL version: 2.4.11.0"));

        var snapshot = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();

        Assert.Equal(CapabilityStatus.Supported, snapshot.OptionalDependencies[CapabilityId.WindowsTerminal].Status);
        Assert.DoesNotContain(runner.Requests, request => string.Equals(request.FileName, "wt.exe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("--version", StringComparer.OrdinalIgnoreCase)
            && string.Equals(request.FileName, "wt.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddPlatformCapabilities_RegistersSingletonsWithoutRunningAProbe()
    {
        var services = new ServiceCollection();
        var runner = new FakeRunner(_ => throw new InvalidOperationException("DI construction must not probe"));
        services.AddSingleton<IProcessRunner>(runner);
        services.AddPlatformCapabilities();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IPlatformCapabilityService>();
        var second = provider.GetRequiredService<IPlatformCapabilityService>();

        Assert.Same(first, second);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task HostSnapshot_UsesCliFactsAndIsolatesMissingOptionalDependencies()
    {
        var runner = new FakeRunner(request => request.FileName switch
        {
            "wsl.exe" when request.Arguments.SequenceEqual(["--version"]) => Ok("WSL version: 2.4.11.0\nKernel version: 6.6.36.3\nWSLg version: 1.0.65"),
            "wsl.exe" => Ok("The most recent version of WSL is installed."),
            "whoami.exe" => Ok("Mandatory Label,S-1-16-12288"),
            "where.exe" when request.Arguments[0] == "usbipd.exe" => Exit(1),
            "where.exe" => Ok("C:\\Tools\\" + request.Arguments[0]),
            "wt.exe" => Ok("Windows Terminal 1.22.10352.0"),
            "code.cmd" => Ok("1.99.3"),
            "com.docker.cli.exe" => Ok("Docker Desktop 4.40.0"),
            "podman.exe" => Ok("podman version 5.4.2"),
            "schtasks.exe" => Ok("task"),
            _ => throw new InvalidOperationException(request.FileName)
        });

        var snapshot = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();

        Assert.Equal(new Version(2, 4, 11, 0), snapshot.Host.WslVersion);
        Assert.Equal(new Version(6, 6, 36, 3), snapshot.Host.KernelVersion);
        Assert.Equal(CapabilitySource.WslCli, snapshot.Capabilities[CapabilityId.Wsl].Source);
        Assert.Equal(CapabilityStatus.Unavailable, snapshot.OptionalDependencies[CapabilityId.UsbIpd].Status);
        Assert.Equal(CapabilityStatus.Supported, snapshot.OptionalDependencies[CapabilityId.WindowsTerminal].Status);
        Assert.True(snapshot.Host.IsElevated);
    }

    [Fact]
    public async Task HostSnapshot_MalformedCliOutputNeverBecomesVersionBasedSuccess()
    {
        var runner = HostRunner(Ok("localized or malformed"));
        var snapshot = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();

        Assert.Equal(CapabilityStatus.Unknown, snapshot.Capabilities[CapabilityId.SparseVhd].Status);
        Assert.Equal("Capability.Feature.MalformedHelp", snapshot.Capabilities[CapabilityId.SparseVhd].ReasonCode);
        Assert.Null(snapshot.Host.WslVersion);
    }

    [Fact]
    public async Task HostSnapshot_AuthoritativeHelpTakesPrecedenceOverWindowsBuild()
    {
        var runner = new FakeRunner(request => request.Arguments switch
        {
            ["--version"] => Ok("WSL version: 2.4.11.0"),
            ["--help"] => Ok("Usage: wsl.exe [options]\n--install"),
            ["--manage", "--help"] => Ok("Usage: wsl.exe --manage\n--set-sparse"),
            _ when request.FileName == "where.exe" => Exit(1),
            _ => Ok()
        });
        var snapshot = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();

        Assert.Equal(CapabilityStatus.Supported, snapshot.Capabilities[CapabilityId.SparseVhd].Status);
        Assert.Equal(CapabilityStatus.Unsupported, snapshot.Capabilities[CapabilityId.ImportInPlace].Status);
        Assert.Equal(CapabilitySource.WslCli, snapshot.Capabilities[CapabilityId.ImportInPlace].Source);
    }

    [Fact]
    public async Task HostSnapshot_ClassifiesPermissionDeniedSeparately()
    {
        var runner = HostRunner(Exit(5, error: "Access is denied"));
        var snapshot = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();

        Assert.Equal(CapabilityStatus.RequiresElevation, snapshot.Capabilities[CapabilityId.Wsl].Status);
        Assert.Equal(CapabilitySource.WslCli, snapshot.Capabilities[CapabilityId.Wsl].Source);
        Assert.NotEqual(default, snapshot.Capabilities[CapabilityId.Wsl].CheckedAt);
    }

    [Fact]
    public async Task HostSnapshot_CliReportedUpdateProducesRequiresUpdate()
    {
        var runner = new FakeRunner(request => request.Arguments switch
        {
            ["--version"] => Ok("WSL version: 2.4.11.0"),
            ["--status"] => Ok("An update is available."),
            _ when request.FileName == "where.exe" => Exit(1),
            _ => Ok()
        });
        var result = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();
        Assert.Equal(CapabilityStatus.RequiresUpdate, result.Capabilities[CapabilityId.Wsl].Status);
        Assert.Equal("Capability.Wsl.UpdateAvailable", result.Capabilities[CapabilityId.Wsl].ReasonCode);
    }

    [Fact]
    public async Task DependencyProbes_DistinguishProductVersionPermissionAndMalformedOutput()
    {
        var runner = new FakeRunner(request => request.FileName switch
        {
            "wsl.exe" when request.Arguments[0] == "--version" => Ok("WSL version: 2.4.11.0"),
            "where.exe" => Ok("C:\\bin\\" + request.Arguments[0]),
            "wt.exe" => Ok("Windows Terminal 1.22.0"),
            "code.cmd" => Ok("not a version"),
            "com.docker.cli.exe" => Ok("Docker Desktop 4.40.0"),
            "podman.exe" => Exit(5, error: "Access is denied"),
            "usbipd.exe" => Ok("usbipd-win 4.3.0"),
            "schtasks.exe" => Exit(5, error: "Access is denied"),
            _ => Ok()
        });
        var values = (await new PlatformCapabilityService(runner).GetHostSnapshotAsync()).OptionalDependencies;

        Assert.Equal(CapabilityStatus.Supported, values[CapabilityId.WindowsTerminal].Status);
        Assert.Equal(CapabilityStatus.Unknown, values[CapabilityId.VisualStudioCode].Status);
        Assert.Equal(CapabilityStatus.Supported, values[CapabilityId.DockerDesktop].Status);
        Assert.Equal(CapabilityStatus.RequiresElevation, values[CapabilityId.Podman].Status);
        Assert.Equal(CapabilityStatus.Unknown, values[CapabilityId.UsbIpd].Status);
        Assert.Equal(CapabilityStatus.RequiresElevation, values[CapabilityId.TaskScheduler].Status);
        Assert.Contains(runner.Requests, x => x.FileName == "com.docker.cli.exe");
        Assert.DoesNotContain(runner.Requests, x => x.FileName == "docker.exe");
    }

    [Fact]
    public async Task ConcurrentHostCallersShareSingleFlightProbes()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner(async (request, _) =>
        {
            await gate.Task;
            return request.FileName == "wsl.exe" && request.Arguments[0] == "--version"
                ? Ok("WSL version: 2.4.11.0") : request.FileName == "where.exe" ? Exit(1) : Ok();
        });
        var service = new PlatformCapabilityService(runner);

        var calls = Enumerable.Range(0, 12).Select(_ => service.GetHostSnapshotAsync()).ToArray();
        gate.SetResult();
        await Task.WhenAll(calls);

        Assert.Equal(1, runner.Requests.Count(x => x.FileName == "wsl.exe" && x.Arguments.SequenceEqual(["--version"])));
        Assert.Equal(1, runner.Requests.Count(x => x.FileName == "where.exe" && x.Arguments[0] == "usbipd.exe"));
    }

    [Fact]
    public async Task DependencyInvalidationRefreshesOnlyRequestedDependency()
    {
        var runner = HostRunner(Ok("WSL version: 2.4.11.0"));
        var service = new PlatformCapabilityService(runner);
        await service.GetHostSnapshotAsync();

        service.InvalidateOptionalDependency(CapabilityId.Podman);
        await service.GetHostSnapshotAsync();

        Assert.Equal(2, runner.Requests.Count(x => x.FileName == "where.exe" && x.Arguments[0] == "podman.exe"));
        Assert.Equal(1, runner.Requests.Count(x => x.FileName == "where.exe" && x.Arguments[0] == "com.docker.cli.exe"));
        Assert.Equal(1, runner.Requests.Count(x => x.FileName == "wsl.exe" && x.Arguments[0] == "--version"));
    }

    [Fact]
    public async Task DependencyCacheExpiresWithoutRefreshingStableHostFacts()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-11T00:00:00Z"));
        var runner = HostRunner(Ok("WSL version: 2.4.11.0"));
        var service = new PlatformCapabilityService(runner, clock);
        await service.GetHostSnapshotAsync();

        clock.Advance(TimeSpan.FromMinutes(5));
        await service.GetHostSnapshotAsync();

        Assert.Equal(2, runner.Requests.Count(x => x.FileName == "where.exe" && x.Arguments[0] == "com.docker.cli.exe"));
        Assert.Equal(1, runner.Requests.Count(x => x.FileName == "wsl.exe" && x.Arguments[0] == "--version"));
    }

    [Fact]
    public async Task InstanceSnapshot_UsesStructuredDistributionArgumentsAndTypedFacts()
    {
        var runner = new FakeRunner(request => request.Arguments switch
        {
            ["--list", "--verbose"] => Ok("* Ubuntu-Dev   Running   2"),
            ["--distribution", "Ubuntu-Dev", "--exec", "cat", "/etc/os-release"] => Ok("ID=ubuntu\nVERSION_ID=24.04"),
            ["--distribution", "Ubuntu-Dev", "--exec", "systemctl", "is-system-running"] => Ok("running"),
            _ => throw new InvalidOperationException(string.Join('|', request.Arguments))
        });

        var snapshot = await new PlatformCapabilityService(runner).GetInstanceSnapshotAsync("Ubuntu-Dev");

        Assert.Equal(2, snapshot.Instance.WslVersion);
        Assert.Equal("ubuntu", snapshot.Instance.DistributionId);
        Assert.True(snapshot.Instance.SystemdRunning);
        Assert.All(runner.Requests, x => Assert.Equal("wsl.exe", x.FileName));
        Assert.Contains(runner.Requests, x => x.Arguments.SequenceEqual(["--distribution", "Ubuntu-Dev", "--exec", "cat", "/etc/os-release"]));
        Assert.Equal(ProcessOutputEncoding.Utf16LittleEndian,
            runner.Requests.Single(x => x.Arguments.SequenceEqual(["--list", "--verbose"])).OutputEncoding);
    }

    [Fact]
    public async Task InstanceListParsing_DoesNotMatchNamePrefix()
    {
        var runner = new FakeRunner(request => request.Arguments[0] == "--list"
            ? Ok("Ubuntu-Dev Running 2\nUbuntu Stopped 1")
            : request.Arguments.Contains("cat") ? Ok("ID=ubuntu") : Ok("offline"));
        var snapshot = await new PlatformCapabilityService(runner).GetInstanceSnapshotAsync("Ubuntu");
        Assert.Equal(1, snapshot.Instance.WslVersion);
    }

    [Theory]
    [InlineData(1, "", "There is no distribution with the supplied name.", CapabilityStatus.Unavailable, "Capability.Instance.DistributionAbsent")]
    [InlineData(1, "", "Access is denied", CapabilityStatus.RequiresElevation, "Capability.Instance.SystemdPermissionDenied")]
    [InlineData(1, "", "System has not been booted with systemd as init system", CapabilityStatus.Unsupported, "Capability.Instance.SystemdDisabled")]
    [InlineData(1, "gibberish", "", CapabilityStatus.Unknown, "Capability.Instance.SystemdProbeFailed")]
    [InlineData(1, "degraded", "", CapabilityStatus.Supported, "Capability.Instance.SystemdRunning")]
    public async Task SystemdClassification_IsHonest(int exit, string output, string error, CapabilityStatus expected, string reason)
    {
        var runner = new FakeRunner(request => request.Arguments[0] == "--list" ? Ok("Ubuntu Running 2") :
            request.Arguments.Contains("cat") ? Ok("ID=ubuntu") : Exit(exit, output, error));
        var value = (await new PlatformCapabilityService(runner).GetInstanceSnapshotAsync("Ubuntu")).Capabilities[CapabilityId.InstanceSystemd];
        Assert.Equal(expected, value.Status);
        Assert.Equal(reason, value.ReasonCode);
    }

    [Fact]
    public async Task UnexpectedOptionalProbeFailureIsIsolatedAsUnknown()
    {
        var runner = new FakeRunner(request => request.FileName switch
        {
            "wsl.exe" when request.Arguments[0] == "--version" => Ok("WSL version: 2.4.11.0"),
            "where.exe" when request.Arguments[0] == "podman.exe" => throw new InvalidOperationException("fixture failure"),
            "where.exe" => Exit(1),
            _ => Ok()
        });

        var snapshot = await new PlatformCapabilityService(runner).GetHostSnapshotAsync();

        Assert.Equal(CapabilityStatus.Unknown, snapshot.OptionalDependencies[CapabilityId.Podman].Status);
        Assert.Equal(CapabilityStatus.Unavailable, snapshot.OptionalDependencies[CapabilityId.DockerDesktop].Status);
    }

    [Fact]
    public async Task InstanceNamesAreCachedCaseInsensitivelyAndCanBeInvalidated()
    {
        var runner = new FakeRunner(request => request.Arguments[0] == "--list" ? Ok("Ubuntu Running 2") :
            request.Arguments.Contains("cat") ? Ok("ID=ubuntu") : Ok("running"));
        var service = new PlatformCapabilityService(runner);

        await service.GetInstanceSnapshotAsync("Ubuntu");
        await service.GetInstanceSnapshotAsync("ubuntu");
        service.InvalidateInstance("UBUNTU");
        await service.GetInstanceSnapshotAsync("Ubuntu");

        Assert.Equal(6, runner.Requests.Count);
    }

    [Fact]
    public async Task CallerCancellationDoesNotCancelSharedProbe()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeRunner(async (request, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return request.FileName == "where.exe" ? Exit(1) : Ok("WSL version: 2.4.11.0");
        });
        var service = new PlatformCapabilityService(runner);
        using var cts = new CancellationTokenSource();
        var cancelled = service.GetHostSnapshotAsync(cancellationToken: cts.Token);
        await started.Task;
        var survivor = service.GetHostSnapshotAsync();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        release.SetResult();

        var result = await survivor;
        Assert.NotNull(result);
        Assert.Equal(1, runner.Requests.Count(x => x.FileName == "wsl.exe" && x.Arguments[0] == "--version"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidationOrForceRefreshDuringFlight_DoesNotAllowOldResultToRepopulate(bool forceRefresh)
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listCalls = 0;
        var runner = new FakeRunner(async (request, _) =>
        {
            if (request.Arguments.SequenceEqual(["--list", "--verbose"]))
            {
                var call = Interlocked.Increment(ref listCalls);
                if (call == 1) { firstStarted.SetResult(); await releaseFirst.Task; return Ok("Ubuntu Running 1"); }
                return Ok("Ubuntu Running 2");
            }
            return request.Arguments.Contains("cat") ? Ok("ID=ubuntu") : Ok("running");
        });
        var service = new PlatformCapabilityService(runner);
        var old = service.GetInstanceSnapshotAsync("Ubuntu");
        await firstStarted.Task;
        if (!forceRefresh) service.InvalidateInstance("Ubuntu");
        var fresh = service.GetInstanceSnapshotAsync("Ubuntu", forceRefresh);
        Assert.Equal(2, (await fresh).Instance.WslVersion);
        releaseFirst.SetResult();
        Assert.Equal(1, (await old).Instance.WslVersion);

        Assert.Equal(2, (await service.GetInstanceSnapshotAsync("ubuntu")).Instance.WslVersion);
        Assert.Equal(2, listCalls);
    }

    [Fact]
    public async Task SoleCallerCancellationThenForceRefresh_DoesNotReuseStrandedFlight()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listCalls = 0;
        var runner = new FakeRunner(async (request, _) =>
        {
            if (request.Arguments.SequenceEqual(["--list", "--verbose"]) && Interlocked.Increment(ref listCalls) == 1)
            { firstStarted.SetResult(); await releaseFirst.Task; return Ok("Ubuntu Running 1"); }
            return request.Arguments.SequenceEqual(["--list", "--verbose"]) ? Ok("Ubuntu Running 2") :
                request.Arguments.Contains("cat") ? Ok("ID=ubuntu") : Ok("running");
        });
        var service = new PlatformCapabilityService(runner);
        using var cts = new CancellationTokenSource();
        var cancelled = service.GetInstanceSnapshotAsync("Ubuntu", cancellationToken: cts.Token);
        await firstStarted.Task;
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(2, (await service.GetInstanceSnapshotAsync("Ubuntu", forceRefresh: true)).Instance.WslVersion);
        releaseFirst.SetResult();
        Assert.Equal(2, (await service.GetInstanceSnapshotAsync("Ubuntu")).Instance.WslVersion);
    }

    private static FakeRunner HostRunner(ProcessResult wslVersion) => new(request => request.FileName switch
    {
        "wsl.exe" when request.Arguments[0] == "--version" => wslVersion,
        "wsl.exe" => Ok(),
        "where.exe" => Exit(1),
        _ => Ok()
    });

    private static ProcessResult Ok(string output = "") => new(0, output, "", TimeSpan.Zero, false, false, false, 1);
    private static ProcessResult Exit(int code, string output = "", string error = "") => new(code, output, error, TimeSpan.Zero, false, false, false, 1);

    private sealed class FakeRunner : IProcessRunner
    {
        private readonly Func<ProcessRequest, CancellationToken, Task<ProcessResult>> _handler;
        public ConcurrentBag<ProcessRequest> Requests { get; } = [];

        public FakeRunner(Func<ProcessRequest, ProcessResult> handler) : this((request, _) => Task.FromResult(handler(request))) { }
        public FakeRunner(Func<ProcessRequest, CancellationToken, Task<ProcessResult>> handler) => _handler = handler;

        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return await _handler(request, cancellationToken);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
