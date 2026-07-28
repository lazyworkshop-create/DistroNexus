using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class IntegrationsContainerRuntimeTests
{
    [Fact]
    public async Task Initialize_ProjectsDegradedAndPartialInventory()
    {
        var runtime = Runtime(new ContainerRuntimeSnapshot(
            [new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Degraded, null, null, "inactive", "degraded", "safe")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>> { [ContainerRuntimeKind.PodmanWsl] = [new("id", "web", "nginx", "running", null)] },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>> { [ContainerRuntimeKind.PodmanWsl] = [] },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>> { [ContainerRuntimeKind.PodmanWsl] = [] },
            new Dictionary<ContainerRuntimeKind, string> { [ContainerRuntimeKind.PodmanWsl] = "Runtime diagnostics failed." }));
        var vm = New(runtime.Object, Mock.Of<IDialogService>());
        await vm.InitializeAsync();
        Assert.Contains("Podman in WSL: Degraded", vm.ContainerRuntimeSummary);
        Assert.Contains("Runtime diagnostics failed.", vm.ContainerRuntimeInventory);
        Assert.False(vm.IsContainerActionsEnabled);
    }

    [Fact]
    public async Task Initialize_RendersBoundedStructuredRows()
    {
        var containers = Enumerable.Range(0, 12).Select(i => new ContainerSummary($"id{i}", $"web{i}", "nginx", "running", null)).ToArray();
        var snapshot = new ContainerRuntimeSnapshot([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "1", null, "active", "healthy", "ok")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>> { [ContainerRuntimeKind.PodmanWsl] = containers },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>> { [ContainerRuntimeKind.PodmanWsl] = [new("i", "nginx", "latest", null)] },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>> { [ContainerRuntimeKind.PodmanWsl] = [new("web", "running", 1)] }, new Dictionary<ContainerRuntimeKind, string>());
        var vm = New(Runtime(snapshot).Object, Mock.Of<IDialogService>());
        await vm.InitializeAsync();
        Assert.Contains("Container web0: nginx (running)", vm.ContainerRuntimeInventory);
        Assert.Contains("Image nginx:latest", vm.ContainerRuntimeInventory);
        Assert.Contains("Compose web: running (1)", vm.ContainerRuntimeInventory);
        Assert.DoesNotContain("Container web10", vm.ContainerRuntimeInventory);
    }

    [Fact]
    public async Task Initialize_UnsupportedSystemd_DisablesPodmanServiceControlsButKeepsConnectionAvailable()
    {
        var runtime = Runtime(Empty());
        var vm = New(runtime.Object, Mock.Of<IDialogService>(), CapabilityStatus.Unsupported);

        await vm.InitializeAsync();
        await vm.StartPodmanSocketCommand.ExecuteAsync(null);

        Assert.False(vm.IsPodmanServiceControlsEnabled);
        Assert.True(vm.IsPodmanConnectionEnabled);
        Assert.Contains("systemd", vm.PodmanPrerequisiteMessage, StringComparison.OrdinalIgnoreCase);
        runtime.Verify(x => x.PreviewPodmanUserUnitAsync(It.IsAny<string>(), It.IsAny<PodmanUserUnit>(), It.IsAny<SystemdAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Initialize_AbsentPodman_DisablesAllPodmanControls()
    {
        var unavailable = new ContainerRuntimeSnapshot([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Unavailable, null, null, "unavailable", "unavailable", "absent")], new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
        var runtime = Runtime(unavailable);
        var vm = New(runtime.Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();
        await vm.ConfigurePodmanConnectionCommand.ExecuteAsync(null);

        Assert.False(vm.IsPodmanServiceControlsEnabled);
        Assert.False(vm.IsPodmanConnectionEnabled);
        Assert.Contains("not installed or reachable", vm.PodmanPrerequisiteMessage, StringComparison.OrdinalIgnoreCase);
        runtime.Verify(x => x.PreviewPodmanConnectionAsync(It.IsAny<string>(), It.IsAny<PodmanConnectionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Initialize_SupportedSystemdAndReachablePodman_EnablesAllPodmanControls()
    {
        var vm = New(Runtime(Empty()).Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.True(vm.IsPodmanServiceControlsEnabled);
        Assert.True(vm.IsPodmanConnectionEnabled);
        Assert.True(string.IsNullOrEmpty(vm.PodmanPrerequisiteMessage));
    }

    [Fact]
    public async Task Initialize_RendersPodmanSocketAndServiceStatesFromCoreStatus()
    {
        var snapshot = new ContainerRuntimeSnapshot([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "1", null, "socket=active;service=inactive", "healthy", "ok")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
        var vm = New(Runtime(snapshot).Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.Contains("socket: Active; service: Inactive", vm.ContainerRuntimeInventory);
    }

    [Fact]
    public async Task Initialize_SuppressesHostilePodmanStateValues()
    {
        var snapshot = new ContainerRuntimeSnapshot([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "1", null, "socket=active;service=active;token=secret", "healthy", "ok")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
        var vm = New(Runtime(snapshot).Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.Contains("socket: Unavailable; service: Unavailable", vm.ContainerRuntimeInventory);
        Assert.DoesNotContain("token=secret", vm.ContainerRuntimeInventory);
    }

    [Fact]
    public async Task Initialize_DoesNotRenderHostileRuntimeEndpoint()
    {
        var snapshot = new ContainerRuntimeSnapshot([new(ContainerRuntimeKind.PodmanDesktop, ContainerRuntimeAvailability.Available, "1", "http://user:secret@127.0.0.1:8080/?token=secret#fragment", "unknown", "healthy", "safe")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
        var vm = New(Runtime(snapshot).Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.DoesNotContain("secret", vm.ContainerRuntimeInventory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", vm.ContainerRuntimeInventory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PodmanAction_DeclineDoesNotExecute()
    {
        var runtime = Runtime(Empty());
        runtime.Setup(x => x.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodmanServicePreview(new SystemdOperationPreview("Ubuntu", new SystemdUnitName("podman.socket"), SystemdAction.Start, SystemdScope.User, false, ["start"], [], "t"), PodmanUserUnit.Socket, SystemdAction.Start));
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var vm = New(runtime.Object, dialogs.Object);
        await vm.InitializeAsync();
        await vm.StartPodmanSocketCommand.ExecuteAsync(null);
        runtime.Verify(x => x.ExecutePodmanUserUnitAsync(It.IsAny<PodmanServicePreview>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PodmanAction_AcceptExecutesAndShowsStructuredPostconditionGuidance()
    {
        var runtime = Runtime(Empty());
        var preview = new PodmanServicePreview(new SystemdOperationPreview("Ubuntu", new SystemdUnitName("podman.socket"), SystemdAction.Start, SystemdScope.User, false, ["start"], [], "t"), PodmanUserUnit.Socket, SystemdAction.Start);
        runtime.Setup(x => x.PreviewPodmanUserUnitAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        runtime.Setup(x => x.ExecutePodmanUserUnitAsync(preview, It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdOperationResult(false, "PostconditionFailed", null, "DN-8003: expected active state was not observed."));
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        dialogs.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var vm = New(runtime.Object, dialogs.Object);
        await vm.InitializeAsync();
        await vm.StartPodmanSocketCommand.ExecuteAsync(null);
        runtime.Verify(x => x.ExecutePodmanUserUnitAsync(preview, It.IsAny<CancellationToken>()), Times.Once);
        dialogs.Verify(x => x.ShowAlertAsync(DistroNexus.Desktop.Properties.Resources.ErrorTitle, "DN-8003: expected active state was not observed."), Times.Once);
        runtime.Verify(x => x.GetSnapshotAsync("Ubuntu", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static Mock<IContainerRuntimeService> Runtime(ContainerRuntimeSnapshot snapshot) { var mock = new Mock<IContainerRuntimeService>(); mock.Setup(x => x.GetSnapshotAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(snapshot); return mock; }
    private static ContainerRuntimeSnapshot Empty() => new([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "1", null, "socket=inactive;service=inactive", "healthy", "reachable")], new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
    private static IntegrationsTabViewModel New(IContainerRuntimeService runtime, IDialogService dialogs, CapabilityStatus systemdStatus = CapabilityStatus.Supported)
    {
        var docker = new Mock<IDockerIntegrationService>(); docker.Setup(x => x.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var capabilities = new Mock<IPlatformCapabilityService>();
        capabilities.Setup(x => x.GetInstanceSnapshotAsync("Ubuntu", false, It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceCapabilitySnapshot(new InstancePlatformFacts("Ubuntu", 2, null, null, systemdStatus == CapabilityStatus.Supported, systemdStatus == CapabilityStatus.Supported), new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.InstanceSystemd] = new(CapabilityId.InstanceSystemd, systemdStatus, "test", CapabilitySource.InstanceCli, DateTimeOffset.UtcNow) }, DateTimeOffset.UtcNow));
        return new IntegrationsTabViewModel(new WslInstanceViewModel(new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>()), docker.Object, dialogs, capabilities.Object, runtime);
    }
}
