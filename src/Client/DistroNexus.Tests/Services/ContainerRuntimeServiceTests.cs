using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.WorkspaceBridge;
using Moq;
using System.Text.Json;

namespace DistroNexus.Tests.Services;

public sealed class ContainerRuntimeServiceTests
{
    [Fact]
    public async Task Snapshot_IsolatesOneAdapterFailure()
    {
        var service = new ContainerRuntimeService([new FakeAdapter(ContainerRuntimeKind.DockerDesktop), new FakeAdapter(ContainerRuntimeKind.PodmanWsl, true)], new FakeSystemd(), new FakeRunner());
        var result = await service.GetSnapshotAsync("Ubuntu");
        Assert.Equal(2, result.Runtimes.Count);
        Assert.Equal(ContainerRuntimeAvailability.Available, result.Runtimes.Single(x => x.Kind == ContainerRuntimeKind.DockerDesktop).Availability);
        Assert.Equal(ContainerRuntimeAvailability.Degraded, result.Runtimes.Single(x => x.Kind == ContainerRuntimeKind.PodmanWsl).Availability);
        Assert.Empty(result.Containers[ContainerRuntimeKind.PodmanWsl]);
        Assert.Equal("DN-8101: Runtime diagnostics are unavailable. Review the selected runtime installation and retry.", result.Failures[ContainerRuntimeKind.PodmanWsl]);
        Assert.DoesNotContain("missing", result.Failures[ContainerRuntimeKind.PodmanWsl], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PodmanAction_AllowsOnlyPreviewedUserSocketOrServiceStartStop()
    {
        var systemd = new FakeSystemd();
        var service = new ContainerRuntimeService([], systemd, new FakeRunner());
        var preview = await service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start);
        Assert.Equal("podman.socket", preview.SystemdPreview.Unit.Value);
        Assert.Equal(SystemdScope.User, preview.SystemdPreview.Scope);
        Assert.True((await service.ExecutePodmanUserUnitAsync(preview)).Succeeded);
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Service, SystemdAction.Restart));
    }

    [Fact]
    public void PodmanJsonParsers_RejectMalformedAndMapReadOnlyInventory()
    {
        Assert.Empty(PodmanWslRuntimeAdapter.ParseContainers("not json"));
        var items = PodmanWslRuntimeAdapter.ParseContainers("[3,{\"Id\":\"abc\",\"Names\":\"web\",\"Image\":\"nginx\",\"State\":\"running\",\"Ports\":\"80\"}]");
        Assert.Equal(new ContainerSummary("abc", "web", "nginx", "running", "80"), Assert.Single(items));
    }

    [Fact]
    public void DockerNdjson_IsMappedWithoutRequiringAnArray()
    {
        var items = PodmanWslRuntimeAdapter.ParseContainers("{\"ID\":\"a\",\"Names\":\"web\",\"Image\":\"nginx\",\"State\":\"running\"}\n{\"ID\":\"b\",\"Names\":\"api\",\"Image\":\"api\",\"State\":\"exited\"}");
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task DegradedReachableRuntime_StillReturnsReadOnlyInventory()
    {
        var service = new ContainerRuntimeService([new InventoryAdapter()], new FakeSystemd(), new FakeRunner());
        var snapshot = await service.GetSnapshotAsync("Ubuntu");
        Assert.Equal(ContainerRuntimeAvailability.Degraded, Assert.Single(snapshot.Runtimes).Availability);
        Assert.Single(snapshot.Containers[ContainerRuntimeKind.PodmanWsl]);
    }

    [Fact]
    public async Task PodmanWslTimeout_IsUnavailableAndDoesNotAttemptInventory()
    {
        var runner = new RecordingRunner(_ => new ProcessResult(null, string.Empty, string.Empty, TimeSpan.Zero, true, false, false, null));
        var adapter = new PodmanWslRuntimeAdapter(runner);
        var result = await adapter.ProbeAsync("Ubuntu");
        Assert.Equal(ContainerRuntimeAvailability.Unavailable, result.Availability);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task PodmanWslProbe_UsesOnlyAllowListedUserSystemdQueriesAndReportsBothStates()
    {
        var runner = new RecordingRunner(request => new ProcessResult(0,
            request.Arguments.Contains("podman.socket") ? "active" : request.Arguments.Contains("podman.service") ? "inactive" : request.Arguments.Contains("connection") ? "unix:///run/user/1000/podman/podman.sock" : "5.5.1",
            string.Empty, TimeSpan.Zero, false, false, false, null));
        var result = await new PodmanWslRuntimeAdapter(runner).ProbeAsync("Ubuntu");
        Assert.Equal("socket=active;service=inactive", result.ServiceState);
        Assert.Equal("unix:///run/user/1000/podman/podman.sock", result.Endpoint);
        Assert.Contains(runner.Requests, r => r.Arguments.SequenceEqual(["--distribution", "Ubuntu", "--exec", "systemctl", "--user", "is-active", "podman.socket"]));
        Assert.Contains(runner.Requests, r => r.Arguments.SequenceEqual(["--distribution", "Ubuntu", "--exec", "systemctl", "--user", "is-active", "podman.service"]));
    }

    [Fact]
    public void RuntimeStatus_CanonicalizesHostileVersionAtCoreDtoBoundary()
    {
        var status = new ContainerRuntimeStatus(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "5.5.1 token=secret", null, "active", "healthy", "safe");
        Assert.Null(status.Version);
    }

    [Fact]
    public async Task RuntimeAdapters_UseBoundedInstanceCommands()
    {
        var runner = new RecordingRunner(_ => new ProcessResult(0, "[]", string.Empty, TimeSpan.Zero, false, false, false, null));
        await new PodmanWslRuntimeAdapter(runner).ListContainersAsync("Ubuntu");
        await new PodmanDesktopRuntimeAdapter(runner, () => true).ListImagesAsync("ignored");
        Assert.Equal("wsl.exe", runner.Requests[0].FileName);
        Assert.Equal(["--distribution", "Ubuntu", "--exec", "podman", "ps", "--all", "--format", "json"], runner.Requests[0].Arguments);
        Assert.Equal("podman.exe", runner.Requests[1].FileName);
        Assert.DoesNotContain(runner.Requests, request => request.FileName.Equals("docker.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PodmanDesktopDetection_RequiresDesktopInstallationFactBeforeCallingCli()
    {
        var runner = new RecordingRunner(_ => new ProcessResult(0, "5.0", string.Empty, TimeSpan.Zero, false, false, false, null));
        var status = await new PodmanDesktopRuntimeAdapter(runner, () => false).ProbeAsync("ignored");

        Assert.Equal(ContainerRuntimeAvailability.Unavailable, status.Availability);
        Assert.Equal("Podman Desktop is not installed on Windows.", status.Detail);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task PodmanDesktopDetection_ReportsInstalledDesktopWhenCapabilityAndCliAreAvailable()
    {
        var runner = new RecordingRunner(_ => new ProcessResult(0, "5.0", string.Empty, TimeSpan.Zero, false, false, false, null));
        var status = await new PodmanDesktopRuntimeAdapter(runner, () => true).ProbeAsync("ignored");

        Assert.Equal(ContainerRuntimeAvailability.Available, status.Availability);
        Assert.Equal("Podman Desktop on Windows.", status.Detail);
        Assert.Contains(runner.Requests, request => request.FileName == "podman.exe");
    }

    [Fact]
    public async Task PodmanDesktopDetection_ReportsUnavailableWhenInstalledDesktopCliCannotRun()
    {
        var runner = new RecordingRunner(_ => new ProcessResult(1, string.Empty, string.Empty, TimeSpan.Zero, false, false, false, null));
        var status = await new PodmanDesktopRuntimeAdapter(runner, () => true).ProbeAsync("ignored");

        Assert.Equal(ContainerRuntimeAvailability.Unavailable, status.Availability);
        Assert.Equal("Podman Desktop is installed but unavailable on Windows.", status.Detail);
    }

    [Fact]
    public async Task BridgeComposition_UsesActualPodmanAdapterAndFixedArguments()
    {
        var runner = new RecordingRunner(request => new ProcessResult(0, request.Arguments.Contains("json") ? "[{\"Id\":\"id\",\"Names\":\"web\",\"Image\":\"nginx\",\"State\":\"running\"}]" : "1.0", string.Empty, TimeSpan.Zero, false, false, false, null));
        var service = ContainerRuntimeBridgeComposition.Create(runner, Mock.Of<ISystemdService>());
        var snapshot = await service.GetSnapshotAsync("Ubuntu");
        Assert.NotEmpty(snapshot.Runtimes);
        Assert.NotEmpty(snapshot.Containers[ContainerRuntimeKind.PodmanWsl]);
        Assert.Contains(runner.Requests, r => r.FileName == "wsl.exe" && r.Arguments.SequenceEqual(["--distribution", "Ubuntu", "--exec", "podman", "ps", "--all", "--format", "json"]));
    }

    [Fact]
    public async Task Connection_RequiresSafeEndpointAndOneTimePreview()
    {
        var service = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner());
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("bad", new Uri("ssh://remote.example/podman.sock"))));
        var preview = await service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("local", new Uri("unix:///run/user/1000/podman/podman.sock")));
        Assert.True((await service.ConfigurePodmanConnectionAsync(preview)).Succeeded);
        Assert.Equal("PreviewRequired", (await service.ConfigurePodmanConnectionAsync(preview)).OutcomeCode);
    }

    [Fact]
    public async Task ConnectionPreview_DisclosesCreateOrReplaceWithoutLeakingUnsafeExistingEndpoint()
    {
        var create = new ContainerRuntimeService([], new FakeSystemd(), new RecordingRunner(request => request.Arguments.Contains("inspect")
            ? new ProcessResult(125, string.Empty, string.Empty, TimeSpan.Zero, false, false, false, null)
            : new ProcessResult(0, "5.0", string.Empty, TimeSpan.Zero, false, false, false, null)));
        var request = new PodmanConnectionRequest("local", new Uri("unix:///run/user/1000/podman/podman.sock"));
        var created = await create.PreviewPodmanConnectionAsync("Ubuntu", request);
        Assert.Equal("Create", created.Operation);
        Assert.Null(created.ExistingEndpoint);
        Assert.Contains(created.Effects, effect => effect.StartsWith("Create Podman connection 'local'", StringComparison.Ordinal));

        var replace = new ContainerRuntimeService([], new FakeSystemd(), new RecordingRunner(request => request.Arguments.Contains("inspect")
            ? new ProcessResult(0, "unix:///run/user/1000/podman/podman.sock", string.Empty, TimeSpan.Zero, false, false, false, null)
            : new ProcessResult(0, "5.0", string.Empty, TimeSpan.Zero, false, false, false, null)));
        var replacement = await replace.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("local", new Uri("http://127.0.0.1:8080")));
        Assert.Equal("Replace", replacement.Operation);
        Assert.Equal("unix:///run/user/1000/podman/podman.sock", replacement.ExistingEndpoint);
        Assert.Contains(replacement.Effects, effect => effect.StartsWith("Replace Podman connection 'local'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConnectionStateDrift_BlocksReplaceBeforeMutation()
    {
        var inspectCount = 0;
        var runner = new RecordingRunner(request =>
        {
            if (!request.Arguments.Contains("inspect")) return new ProcessResult(0, "5.0", string.Empty, TimeSpan.Zero, false, false, false, null);
            return new ProcessResult(0, Interlocked.Increment(ref inspectCount) == 1 ? "unix:///run/user/1000/podman/podman.sock" : "unix:///run/user/2000/podman/podman.sock", string.Empty, TimeSpan.Zero, false, false, false, null);
        });
        var service = new ContainerRuntimeService([], new FakeSystemd(), runner);
        var preview = await service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("local", new Uri("http://127.0.0.1:8080")));

        var result = await service.ConfigurePodmanConnectionAsync(preview);

        Assert.False(result.Succeeded);
        Assert.Equal("ConnectionStateDrift", result.OutcomeCode);
        Assert.Contains("DN-8103", result.Guidance);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Contains("add"));
    }

    [Fact]
    public async Task ConnectionEndpointValidation_RejectsUnsafeFormsAndNeverReportsSecrets()
    {
        var unsafeEndpoints = new[]
        {
            "http://user:secret@127.0.0.1:8080",
            "http://127.0.0.1:8080/?token=secret",
            "http://127.0.0.1:8080/#secret",
            "unix:///tmp/podman.sock"
        };
        foreach (var endpoint in unsafeEndpoints)
            await Assert.ThrowsAsync<ArgumentException>(() => new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner()).PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("local", new Uri(endpoint))));

        var adapter = new PodmanWslRuntimeAdapter(new RecordingRunner(request => new ProcessResult(0, request.Arguments.Contains("connection") ? "http://user:secret@127.0.0.1:8080/?token=secret" : "5.0", string.Empty, TimeSpan.Zero, false, false, false, null)));
        var status = await adapter.ProbeAsync("Ubuntu");
        var json = JsonSerializer.Serialize(status);
        Assert.Null(status.Endpoint);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoreIssuedConnectionGrant_RejectsForgedMismatchedAndReplayedTokens()
    {
        var service = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner());
        var request = new PodmanConnectionRequest("local", new Uri("unix:///run/user/1000/podman/podman.sock"));
        var preview = await service.PreviewPodmanConnectionAsync("Ubuntu", request);
        Assert.Equal("PreviewRequired", (await service.ConfigurePodmanConnectionAsync("forged", "Ubuntu", request)).OutcomeCode);
        Assert.Equal("PreviewRequired", (await service.ConfigurePodmanConnectionAsync(preview.Token, "Other", request)).OutcomeCode);
        Assert.True((await service.ConfigurePodmanConnectionAsync(preview.Token, "Ubuntu", request)).Succeeded);
        Assert.Equal("PreviewRequired", (await service.ConfigurePodmanConnectionAsync(preview.Token, "Ubuntu", request)).OutcomeCode);
    }

    [Fact]
    public async Task UserUnitGrants_PruneExpiredEntriesRejectExpiredPreviewAndEnforceCapacity()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-23T00:00:00Z"));
        var service = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner(), clock);
        var previews = new List<PodmanServicePreview>();
        for (var i = 0; i < 32; i++) previews.Add(await service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start));

        var limit = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Service, SystemdAction.Stop));
        Assert.StartsWith("DN-8102:", limit.Message, StringComparison.Ordinal);

        clock.Advance(TimeSpan.FromMinutes(2).Add(TimeSpan.FromTicks(1)));
        var fresh = await service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Service, SystemdAction.Stop);
        Assert.False((await service.ExecutePodmanUserUnitAsync(previews[0])).Succeeded);
        Assert.True((await service.ExecutePodmanUserUnitAsync(fresh)).Succeeded);
    }

    [Fact]
    public async Task ConnectionGrants_PruneExpiredEntriesRejectExpiredPreviewAndEnforceCapacity()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-23T00:00:00Z"));
        var service = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner(), clock);
        var previews = new List<PodmanConnectionPreview>();
        for (var i = 0; i < 32; i++) previews.Add(await service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest($"local{i}", new Uri("unix:///run/user/1000/podman/podman.sock"))));

        var limit = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("overflow", new Uri("unix:///run/user/1000/podman/podman.sock"))));
        Assert.StartsWith("DN-8102:", limit.Message, StringComparison.Ordinal);

        clock.Advance(TimeSpan.FromMinutes(2).Add(TimeSpan.FromTicks(1)));
        var fresh = await service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("fresh", new Uri("unix:///run/user/1000/podman/podman.sock")));
        Assert.Equal("PreviewRequired", (await service.ConfigurePodmanConnectionAsync(previews[0])).OutcomeCode);
        Assert.True((await service.ConfigurePodmanConnectionAsync(fresh)).Succeeded);
    }

    [Fact]
    public async Task ParallelPreviewGrantCapacity_IsAtomicForBothGrantKinds()
    {
        var service = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner());
        var units = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start)));
        Assert.Equal(32, units.DistinctBy(x => x.SystemdPreview.PreviewToken).Count());
        var unitOverflow = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Service, SystemdAction.Stop));
        Assert.StartsWith("DN-8102:", unitOverflow.Message, StringComparison.Ordinal);

        var connections = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner());
        var previews = await Task.WhenAll(Enumerable.Range(0, 32).Select(i => connections.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest($"local{i}", new Uri("unix:///run/user/1000/podman/podman.sock")))));
        Assert.Equal(32, previews.DistinctBy(x => x.Token).Count());
        var connectionOverflow = await Assert.ThrowsAsync<InvalidOperationException>(() => connections.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("overflow", new Uri("unix:///run/user/1000/podman/podman.sock"))));
        Assert.StartsWith("DN-8102:", connectionOverflow.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MixedPreviewGrantKinds_ShareTheSingleAggregateCapacity()
    {
        var service = new ContainerRuntimeService([], new FakeSystemd(), new FakeRunner());
        for (var i = 0; i < 16; i++)
        {
            await service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start);
            await service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest($"local{i}", new Uri("unix:///run/user/1000/podman/podman.sock")));
        }

        var unitOverflow = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Service, SystemdAction.Stop));
        var connectionOverflow = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewPodmanConnectionAsync("Ubuntu", new PodmanConnectionRequest("overflow", new Uri("unix:///run/user/1000/podman/podman.sock"))));

        Assert.StartsWith("DN-8102:", unitOverflow.Message, StringComparison.Ordinal);
        Assert.StartsWith("DN-8102:", connectionOverflow.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BridgeStatusHandler_SerializesCompleteReadOnlySnapshotAndRedactedFailure()
    {
        var snapshot = new ContainerRuntimeSnapshot(
            [new(ContainerRuntimeKind.DockerDesktop, ContainerRuntimeAvailability.Available, "4.0", "unix:///docker.sock", "active", "healthy", "available"),
             new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Degraded, "5.0", "unix:///run/user/1000/podman/podman.sock", "inactive", "degraded", "reachable"),
             new(ContainerRuntimeKind.PodmanDesktop, ContainerRuntimeAvailability.Unavailable, null, null, "unavailable", "unavailable", "absent")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>> { [ContainerRuntimeKind.DockerDesktop] = [new("c1", "web", "nginx", "running", "80")], [ContainerRuntimeKind.PodmanWsl] = [], [ContainerRuntimeKind.PodmanDesktop] = [] },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>> { [ContainerRuntimeKind.DockerDesktop] = [new("i1", "nginx", "latest", "10 MB")], [ContainerRuntimeKind.PodmanWsl] = [], [ContainerRuntimeKind.PodmanDesktop] = [] },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>> { [ContainerRuntimeKind.DockerDesktop] = [new("web", "running", 1)], [ContainerRuntimeKind.PodmanWsl] = [], [ContainerRuntimeKind.PodmanDesktop] = [] },
            new Dictionary<ContainerRuntimeKind, string> { [ContainerRuntimeKind.PodmanWsl] = "DN-8101: Runtime diagnostics are unavailable. Review the selected runtime installation and retry." });
        var service = new Mock<IContainerRuntimeService>();
        service.Setup(x => x.GetSnapshotAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        var response = await ContainerRuntimeBridgeHandler.GetStatusAsync(service.Object, "Ubuntu");
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });

        Assert.Contains("DockerDesktop", json);
        Assert.Contains("PodmanWsl", json);
        Assert.Contains("PodmanDesktop", json);
        Assert.Contains("nginx", json);
        Assert.Contains("latest", json);
        Assert.Contains("running", json);
        Assert.Contains("DN-8101", json);
        Assert.DoesNotContain("raw adapter exception", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HostileAdapterEndpoint_IsRemovedBeforeBridgeSerialization()
    {
        var service = new ContainerRuntimeService([new HostileEndpointAdapter()], new FakeSystemd(), new FakeRunner());
        var response = await ContainerRuntimeBridgeHandler.GetStatusAsync(service, "Ubuntu");
        var json = JsonSerializer.Serialize(response);

        Assert.Null(response.Runtimes.Single().Endpoint);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeAdapter(ContainerRuntimeKind kind, bool fails = false) : IContainerRuntimeAdapter
    {
        public ContainerRuntimeKind Kind => kind;
        public Task<ContainerRuntimeStatus> ProbeAsync(string instanceName, CancellationToken cancellationToken = default) => fails ? throw new InvalidOperationException("missing") : Task.FromResult(new ContainerRuntimeStatus(kind, ContainerRuntimeAvailability.Available, "1", null, "active", "healthy", "ok"));
        public Task<IReadOnlyList<ContainerSummary>> ListContainersAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContainerSummary>>([]);
        public Task<IReadOnlyList<ImageSummary>> ListImagesAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ImageSummary>>([]);
        public Task<IReadOnlyList<ComposeProjectSummary>> ListProjectsAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ComposeProjectSummary>>([]);
    }
    private sealed class InventoryAdapter : IContainerRuntimeAdapter
    {
        public ContainerRuntimeKind Kind => ContainerRuntimeKind.PodmanWsl;
        public Task<ContainerRuntimeStatus> ProbeAsync(string i, CancellationToken c = default) => Task.FromResult(new ContainerRuntimeStatus(Kind, ContainerRuntimeAvailability.Degraded, "1", null, "inactive", "degraded", "reachable"));
        public Task<IReadOnlyList<ContainerSummary>> ListContainersAsync(string i, CancellationToken c = default) => Task.FromResult<IReadOnlyList<ContainerSummary>>([new("id", "web", "nginx", "running", null)]);
        public Task<IReadOnlyList<ImageSummary>> ListImagesAsync(string i, CancellationToken c = default) => Task.FromResult<IReadOnlyList<ImageSummary>>([]);
        public Task<IReadOnlyList<ComposeProjectSummary>> ListProjectsAsync(string i, CancellationToken c = default) => Task.FromResult<IReadOnlyList<ComposeProjectSummary>>([]);
    }
    private sealed class HostileEndpointAdapter : IContainerRuntimeAdapter
    {
        public ContainerRuntimeKind Kind => ContainerRuntimeKind.PodmanDesktop;
        public Task<ContainerRuntimeStatus> ProbeAsync(string i, CancellationToken c = default) => Task.FromResult(new ContainerRuntimeStatus(Kind, ContainerRuntimeAvailability.Available, "1", "http://user:secret@127.0.0.1:8080/?token=secret#fragment", "unknown", "healthy", "unsafe"));
        public Task<IReadOnlyList<ContainerSummary>> ListContainersAsync(string i, CancellationToken c = default) => Task.FromResult<IReadOnlyList<ContainerSummary>>([]);
        public Task<IReadOnlyList<ImageSummary>> ListImagesAsync(string i, CancellationToken c = default) => Task.FromResult<IReadOnlyList<ImageSummary>>([]);
        public Task<IReadOnlyList<ComposeProjectSummary>> ListProjectsAsync(string i, CancellationToken c = default) => Task.FromResult<IReadOnlyList<ComposeProjectSummary>>([]);
    }
    private sealed class FakeSystemd : ISystemdService
    {
        private int _previewNumber;
        public Task<IReadOnlyList<SystemdServiceInfo>> ListAsync(string instanceName, SystemdScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemdServiceInfo>>([]);
        public Task<IReadOnlyList<SystemdJournalEntry>> GetJournalAsync(string instanceName, SystemdUnitName unit, SystemdScope scope, string? search = null, int lineLimit = 200, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemdJournalEntry>>([]);
        public Task<SystemdServiceDetails?> GetDetailsAsync(string instanceName, SystemdUnitName unit, SystemdScope scope, CancellationToken cancellationToken = default) => Task.FromResult<SystemdServiceDetails?>(null);
        public Task<SystemdOperationPreview> PreviewAsync(string instanceName, SystemdUnitName unit, SystemdAction action, SystemdScope scope, CancellationToken cancellationToken = default) => Task.FromResult(new SystemdOperationPreview(instanceName, unit, action, scope, false, [], [], $"token-{Interlocked.Increment(ref _previewNumber)}"));
        public Task<SystemdOperationResult> ExecuteAsync(SystemdOperationPreview preview, CancellationToken cancellationToken = default) => Task.FromResult(new SystemdOperationResult(true, "Succeeded", null));
    }
    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
    private sealed class FakeRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ProcessResult(0, request.Arguments.Contains("inspect") ? "unix:///run/user/1000/podman/podman.sock" : "1.0", string.Empty, TimeSpan.Zero, false, false, false, null));
    }
    private sealed class RecordingRunner(Func<ProcessRequest, ProcessResult> result) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result(request));
        }
    }
}
