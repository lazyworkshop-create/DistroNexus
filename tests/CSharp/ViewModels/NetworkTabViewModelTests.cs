using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.ViewModelTests.Helpers;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="NetworkTabViewModel"/> (C-02).
/// </summary>
public sealed class NetworkTabViewModelTests
{
    private static (Mock<INetworkService>, Mock<IDialogService>) CreateMocks()
    {
        var network = new Mock<INetworkService>();
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
        return (network, dialog);
    }

    private static NetworkTabViewModel CreateSut(
        WslInstanceViewModel instanceVm,
        Mock<INetworkService> network,
        Mock<IDialogService> dialog)
        => new(instanceVm, network.Object, dialog.Object);

    // ── Stopped instance ──────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenInstanceStopped_SetsShowStoppedPlaceholder()
    {
        var (network, dialog) = CreateMocks();
        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(state: "Stopped");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, network, dialog);
        await sut.InitializeAsync();

        sut.ShowStoppedPlaceholder.Should().BeTrue();
        network.Verify(n => n.GetInstanceIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        network.Verify(n => n.GetPortMappingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Running instance ──────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenInstanceRunning_PopulatesIpAndPortMappings()
    {
        var (network, dialog) = CreateMocks();
        network.Setup(n => n.GetInstanceIpAddressAsync("Ubuntu", It.IsAny<CancellationToken>()))
               .ReturnsAsync("172.24.32.1");
        network.Setup(n => n.GetPortMappingsAsync("Ubuntu", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(
               [
                   new PortMapping { Protocol = "TCP", LocalAddress = "0.0.0.0", Port = 8080, ProcessName = "node", HasWindowsProxy = false },
                   new PortMapping { Protocol = "TCP", LocalAddress = "127.0.0.1", Port = 3000, ProcessName = "dotnet", HasWindowsProxy = true }
               ]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu", state: "Running");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, network, dialog);
        await sut.InitializeAsync();

        sut.ShowStoppedPlaceholder.Should().BeFalse();
        sut.InstanceIp.Should().Be("172.24.32.1");
        sut.PortMappings.Should().HaveCount(2);
        sut.PortMappings[0].Protocol.Should().Be("TCP");
        sut.PortMappings[0].Port.Should().Be(8080);
        sut.PortMappings[1].HasWindowsProxy.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenInstanceRunning_SetsShowStoppedPlaceholderFalse()
    {
        var (network, dialog) = CreateMocks();
        network.Setup(n => n.GetInstanceIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("10.0.0.1");
        network.Setup(n => n.GetPortMappingsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu", state: "Running");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, network, dialog);
        await sut.InitializeAsync();

        sut.ShowStoppedPlaceholder.Should().BeFalse();
    }

    // ── PortMappingViewModel helpers ──────────────────────────────────────────

    [Fact]
    public void PortMappingViewModel_CopyText_FormatsAddressAndPort()
    {
        var pm = new PortMappingViewModel { LocalAddress = "0.0.0.0", Port = 9090 };

        pm.CopyText.Should().Be("0.0.0.0:9090");
    }

    [Fact]
    public async Task InitializeAsync_WhenProxiedPort_HasWindowsProxyTrue()
    {
        var (network, dialog) = CreateMocks();
        network.Setup(n => n.GetInstanceIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("10.0.0.1");
        network.Setup(n => n.GetPortMappingsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
               .ReturnsAsync([new PortMapping { Protocol = "TCP", LocalAddress = "127.0.0.1", Port = 443, HasWindowsProxy = true }]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(state: "Running");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, network, dialog);
        await sut.InitializeAsync();

        sut.PortMappings[0].HasWindowsProxy.Should().BeTrue();
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshNetworkCommand_RequeriesServicee()
    {
        var (network, dialog) = CreateMocks();
        network.Setup(n => n.GetInstanceIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("10.0.0.2");
        network.Setup(n => n.GetPortMappingsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
               .ReturnsAsync([]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(state: "Running");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, network, dialog);
        await sut.InitializeAsync();
        await sut.RefreshNetworkCommand.ExecuteAsync(null);

        // Called once by InitializeAsync → RefreshNetworkAsync, then again by explicit refresh
        network.Verify(n => n.GetInstanceIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Constructor validation ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenNullInstance_Throws()
    {
        var (network, dialog) = CreateMocks();

        var act = () => new NetworkTabViewModel(null!, network.Object, dialog.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("instance");
    }
}
