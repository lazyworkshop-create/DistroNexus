using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class IntegrationsContainerRuntimeTests
{
    [Fact]
    public async Task Initialize_UsesOnlyTypedModuleClientSnapshots()
    {
        var client = Client(Empty());
        var vm = New(client.Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.True(vm.IsPodmanServiceControlsEnabled);
        Assert.True(vm.IsPodmanConnectionEnabled);
        client.Verify(x => x.GetInstanceCapabilitiesAsync("Ubuntu", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.GetContainerRuntimeStatusAsync("Ubuntu", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Initialize_UnsupportedSystemd_DisablesUnitActionsButKeepsConnectionAvailable()
    {
        var client = Client(Empty(), CapabilityStatus.Unsupported);
        var vm = New(client.Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();
        await vm.StartPodmanSocketCommand.ExecuteAsync(null);

        Assert.False(vm.IsPodmanServiceControlsEnabled);
        Assert.True(vm.IsPodmanConnectionEnabled);
        client.Verify(x => x.GetPodmanUserUnitPreviewAsync(It.IsAny<string>(), It.IsAny<PodmanUserUnit>(), It.IsAny<SystemdAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Initialize_RendersBoundedAndSanitizedRuntimeInventory()
    {
        var containers = Enumerable.Range(0, 12).Select(i => new ContainerSummary($"id{i}", $"web{i}", "nginx", "running", null)).ToArray();
        var snapshot = new ContainerRuntimeSnapshot([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "1", "http://user:secret@127.0.0.1:8080/?token=secret", "socket=active;service=inactive", "healthy", "ok")],
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>> { [ContainerRuntimeKind.PodmanWsl] = containers },
            new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
        var vm = New(Client(snapshot).Object, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.Contains("Container web0: nginx (running)", vm.ContainerRuntimeInventory);
        Assert.DoesNotContain("Container web10", vm.ContainerRuntimeInventory);
        Assert.DoesNotContain("secret", vm.ContainerRuntimeInventory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("socket: Active; service: Inactive", vm.ContainerRuntimeInventory);
    }

    [Fact]
    public async Task PodmanAction_DeclineDoesNotExecute()
    {
        var client = Client(Empty());
        var preview = new DistroNexusPodmanUserUnitPreview("issued", "Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start, ["start"]);
        client.Setup(x => x.GetPodmanUserUnitPreviewAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var vm = New(client.Object, dialogs.Object);

        await vm.InitializeAsync();
        await vm.StartPodmanSocketCommand.ExecuteAsync(null);

        client.Verify(x => x.InvokePodmanUserUnitAsync(It.IsAny<DistroNexusPodmanUserUnitPreview>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PodmanAction_AcceptExecutesTypedPreviewAndRefreshes()
    {
        var client = Client(Empty());
        var preview = new DistroNexusPodmanUserUnitPreview("issued", "Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start, ["start"]);
        client.Setup(x => x.GetPodmanUserUnitPreviewAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        client.Setup(x => x.InvokePodmanUserUnitAsync(preview, It.IsAny<CancellationToken>())).ReturnsAsync(new DistroNexusPodmanUserUnitResult(true, "Succeeded"));
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = New(client.Object, dialogs.Object);

        await vm.InitializeAsync();
        await vm.StartPodmanSocketCommand.ExecuteAsync(null);

        client.Verify(x => x.InvokePodmanUserUnitAsync(preview, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.GetContainerRuntimeStatusAsync("Ubuntu", It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Connection_UsesTypedPreviewAndExecute()
    {
        var client = Client(Empty());
        var preview = new DistroNexusPodmanConnectionPreview("issued", "Ubuntu", "local", "unix:///run/user/1000/podman/podman.sock", "Create", null, ["configure"]);
        client.Setup(x => x.GetPodmanConnectionPreviewAsync("Ubuntu", It.IsAny<PodmanConnectionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        client.Setup(x => x.InvokePodmanConnectionAsync(preview, It.IsAny<CancellationToken>())).ReturnsAsync(new PodmanConnectionResult(true, "Succeeded"));
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = New(client.Object, dialogs.Object);

        await vm.InitializeAsync();
        await vm.ConfigurePodmanConnectionCommand.ExecuteAsync(null);

        client.Verify(x => x.GetPodmanConnectionPreviewAsync("Ubuntu", It.IsAny<PodmanConnectionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.InvokePodmanConnectionAsync(preview, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_HasNoDirectContainerOrPlatformCapabilityDependency()
    {
        var dependencies = typeof(IntegrationsTabViewModel).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType);

        Assert.DoesNotContain(typeof(IContainerRuntimeService), dependencies);
        Assert.DoesNotContain(typeof(IPlatformCapabilityService), dependencies);
        Assert.Contains(typeof(IPowerShellModuleClient), dependencies);
    }

    private static Mock<IPowerShellModuleClient> Client(ContainerRuntimeSnapshot snapshot, CapabilityStatus systemdStatus = CapabilityStatus.Supported)
    {
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.GetContainerRuntimeStatusAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        client.Setup(x => x.GetInstanceCapabilitiesAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceCapabilitySnapshot(
            new InstancePlatformFacts("Ubuntu", 2, null, null, systemdStatus == CapabilityStatus.Supported, systemdStatus == CapabilityStatus.Supported),
            new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.InstanceSystemd] = new(CapabilityId.InstanceSystemd, systemdStatus, "test", CapabilitySource.InstanceCli, DateTimeOffset.UtcNow) }, DateTimeOffset.UtcNow));
        return client;
    }

    private static ContainerRuntimeSnapshot Empty() => new([new(ContainerRuntimeKind.PodmanWsl, ContainerRuntimeAvailability.Available, "1", null, "socket=inactive;service=inactive", "healthy", "reachable")], new Dictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>>(), new Dictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>>(), new Dictionary<ContainerRuntimeKind, string>());
    private static IntegrationsTabViewModel New(IPowerShellModuleClient client, IDialogService dialogs)
    {
        var docker = new Mock<IDockerIntegrationService>();
        docker.Setup(x => x.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var instance = new WslInstanceViewModel(new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());
        return new IntegrationsTabViewModel(instance, docker.Object, dialogs, client);
    }
}
