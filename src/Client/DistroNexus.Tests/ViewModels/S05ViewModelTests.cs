using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.Desktop.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class S05ViewModelTests
{
    [Fact]
    public async Task Services_UsesSelectedScopeAndFiltersRunningItems()
    {
        var service = new Mock<ISystemdService>();
        service.Setup(x => x.ListAsync("Ubuntu", SystemdScope.System, It.IsAny<CancellationToken>())).ReturnsAsync([
            new SystemdServiceInfo(new SystemdUnitName("ssh.service"), "ssh", "active", "running", "enabled", "loaded", SystemdScope.System),
            new SystemdServiceInfo(new SystemdUnitName("cron.service"), "cron", "inactive", "dead", "enabled", "loaded", SystemdScope.System)]);
        var vm = new ServicesTabViewModel(Instance(), service.Object, Dialogs().Object) { RunningOnly = true };
        await vm.InitializeAsync();
        Assert.Single(vm.Items); Assert.Equal("ssh.service", vm.Items[0].Name.Value);
    }

    [Fact]
    public async Task Services_ActionUsesPreviewThenExplicitConfirmation()
    {
        var item = new SystemdServiceInfo(new SystemdUnitName("ssh.service"), "ssh", "active", "running", "enabled", "loaded", SystemdScope.User);
        var service = new Mock<ISystemdService>();
        service.Setup(x => x.PreviewAsync("Ubuntu", item.Name, SystemdAction.Stop, SystemdScope.User, It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdOperationPreview("Ubuntu", item.Name, SystemdAction.Stop, SystemdScope.User, false, [], [], "token"));
        service.Setup(x => x.ExecuteAsync(It.IsAny<SystemdOperationPreview>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdOperationResult(true, "Succeeded", item));
        service.Setup(x => x.ListAsync("Ubuntu", It.IsAny<SystemdScope>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var dialogs = Dialogs(true); var vm = new ServicesTabViewModel(Instance(), service.Object, dialogs.Object);
        await vm.StopCommand.ExecuteAsync(item);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        service.Verify(x => x.ExecuteAsync(It.IsAny<SystemdOperationPreview>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Services_JournalSeverityFilter_AppliesAfterBoundedServiceQuery()
    {
        var item = new SystemdServiceInfo(new SystemdUnitName("ssh.service"), "ssh", "active", "running", "enabled", "loaded", SystemdScope.System);
        var service = new Mock<ISystemdService>();
        service.Setup(x => x.GetDetailsAsync("Ubuntu", item.Name, item.Scope, It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdServiceDetails(item, [], null));
        service.Setup(x => x.GetJournalAsync("Ubuntu", item.Name, item.Scope, "", 200, It.IsAny<CancellationToken>())).ReturnsAsync([new SystemdJournalEntry("", "Info", "ready"), new SystemdJournalEntry("", "Error", "failed")]);
        var vm = new ServicesTabViewModel(Instance(), service.Object, Dialogs().Object) { JournalSeverity = "Error" };
        await vm.LoadDetailsCommand.ExecuteAsync(item);
        var entry = Assert.Single(vm.Journal); Assert.Equal("Error", entry.Severity);
        service.Verify(x => x.GetJournalAsync("Ubuntu", item.Name, item.Scope, "", 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NetworkMode_OnlyAppliesAfterConfirmedCorePreviewToken()
    {
        var config = new Mock<INetworkConfigurationService>();
        config.Setup(x => x.GetGuidanceAsync(It.IsAny<WslNetworkingMode>(), It.IsAny<CancellationToken>())).ReturnsAsync((WslNetworkingMode m, CancellationToken _) => new NetworkingModeGuidance(m, m == WslNetworkingMode.Nat, true, ["test"], RestartScope.Wsl));
        config.Setup(x => x.PreviewModeAsync(WslNetworkingMode.Nat, It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkModePreview(WslNetworkingMode.Nat, new ConfigurationPreview("", "[wsl2]", [], RestartScope.Wsl), new NetworkingModeGuidance(WslNetworkingMode.Nat, true, true, [], RestartScope.Wsl), "token"));
        config.Setup(x => x.ApplyModeAsync(WslNetworkingMode.Nat, "token", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        var vm = NetworkVm(config.Object, Dialogs(true).Object);
        await vm.InitializeAsync();
        Assert.Single(vm.AvailableModes); await vm.PreviewAndApplyNetworkingModeCommand.ExecuteAsync(null);
        config.Verify(x => x.ApplyModeAsync(WslNetworkingMode.Nat, "token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Network_WslProbeAndUnavailableStatusRemainExplicit()
    {
        var vm = NetworkVm(Mock.Of<INetworkConfigurationService>(), Dialogs().Object);
        vm.ProbeKind = NetworkProbeKind.WslInstance; await vm.RunProbeCommand.ExecuteAsync(null);
        Assert.Contains("ToolUnavailable", vm.ProbeResult);
    }

    [Fact]
    public async Task Network_ProjectsWindowsCollisionIntoPortRow()
    {
        var network = new Mock<INetworkService>(); network.Setup(x => x.GetInstanceIpAddressAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync("172.20.0.2"); network.Setup(x => x.GetPortMappingsAsync("Ubuntu", null, It.IsAny<CancellationToken>())).ReturnsAsync([new PortMapping { Port = 8080, Protocol = "TCP", LocalAddress = "127.0.0.1" }]);
        var status = new Mock<INetworkStatusAdapter>(); status.Setup(x => x.GetFirewallStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FirewallStatus(FirewallStatusAvailability.Available, "available")); status.Setup(x => x.GetPortCollisionsAsync(It.IsAny<IReadOnlyList<PortMapping>>(), It.IsAny<CancellationToken>())).ReturnsAsync([new PortCollisionStatus(8080, "TCP", true, "Windows owns the port.")]);
        var firewall = new Mock<IFirewallOperationBroker>(); firewall.Setup(x => x.ListOwnedAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var configuration = new Mock<INetworkConfigurationService>(); configuration.Setup(x => x.GetGuidanceAsync(It.IsAny<WslNetworkingMode>(), It.IsAny<CancellationToken>())).ReturnsAsync((WslNetworkingMode mode, CancellationToken _) => new NetworkingModeGuidance(mode, mode == WslNetworkingMode.Nat, true, [], RestartScope.None));
        var vm = new NetworkTabViewModel(Instance(), network.Object, Dialogs().Object, new DistroNexus.Core.Services.NetworkDiagnosticsService(), firewall.Object, configuration.Object, status.Object, Mock.Of<IBrowserLauncher>());
        await vm.InitializeAsync();
        var row = Assert.Single(vm.PortMappings);
        Assert.True(row.HasWindowsCollision); Assert.Equal("Windows owns the port.", row.ConflictGuidance);
    }

    [Fact]
    public async Task NetworkSettings_UsesOnePreviewTokenAfterExplicitConfirmation()
    {
        var config = new Mock<INetworkConfigurationService>();
        config.Setup(x => x.GetGuidanceAsync(It.IsAny<WslNetworkingMode>(), It.IsAny<CancellationToken>())).ReturnsAsync((WslNetworkingMode m, CancellationToken _) => new NetworkingModeGuidance(m, m == WslNetworkingMode.Nat, true, [], RestartScope.Wsl));
        var settings = new NetworkSettings(false, false, false, false, false, "3000");
        config.Setup(x => x.ReadSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        config.Setup(x => x.PreviewSettingsAsync(settings, It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkSettingsPreview(settings, new ConfigurationPreview("", "[wsl2]", ["wsl2.ignoredPorts"], RestartScope.Wsl), "settings-token"));
        config.Setup(x => x.ApplySettingsAsync(settings, "settings-token", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        var vm = NetworkVm(config.Object, Dialogs(true).Object);
        await vm.InitializeAsync();
        await vm.PreviewAndApplyNetworkSettingsCommand.ExecuteAsync(null);
        config.Verify(x => x.ApplySettingsAsync(settings, "settings-token", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("restart", vm.NetworkSettingsEvidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrowserCommand_OnlyPassesSafeLoopbackEndpointToLauncher()
    {
        var launcher = new Mock<IBrowserLauncher>();
        var vm = NetworkVm(Mock.Of<INetworkConfigurationService>(), Dialogs().Object, launcher.Object);
        vm.OpenInBrowserCommand.Execute(new PortMappingViewModel { LocalAddress = "10.0.0.4", Port = 8080 });
        launcher.Verify(x => x.Open(It.IsAny<Uri>()), Times.Never);
        vm.OpenInBrowserCommand.Execute(new PortMappingViewModel { LocalAddress = "::1", Port = 8080 });
        launcher.Verify(x => x.Open(new Uri("http://[::1]:8080/")), Times.Once);
    }

    private static NetworkTabViewModel NetworkVm(INetworkConfigurationService config, IDialogService dialogs, IBrowserLauncher? launcher = null)
    {
        var network = new Mock<INetworkService>(); network.Setup(x => x.GetInstanceIpAddressAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null); network.Setup(x => x.GetPortMappingsAsync("Ubuntu", null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var status = new Mock<INetworkStatusAdapter>(); status.Setup(x => x.GetFirewallStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FirewallStatus(FirewallStatusAvailability.Unavailable, "test")); status.Setup(x => x.GetPortCollisionsAsync(It.IsAny<IReadOnlyList<PortMapping>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var firewall = new Mock<IFirewallOperationBroker>(); firewall.Setup(x => x.ListOwnedAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return new(Instance(), network.Object, dialogs, new DistroNexus.Core.Services.NetworkDiagnosticsService(), firewall.Object, config, status.Object, launcher ?? Mock.Of<IBrowserLauncher>());
    }
    private static Mock<IDialogService> Dialogs(bool confirm = false) { var result = new Mock<IDialogService>(); result.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirm); return result; }
    private static WslInstanceViewModel Instance() => new(new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(), Mock.Of<ISettingsService>(), Mock.Of<ILogger>(), Mock.Of<ITagService>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());
}
