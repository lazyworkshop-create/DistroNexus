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
        var service = new Mock<IPowerShellModuleClient>();
        service.Setup(x => x.GetSystemdServicesAsync("Ubuntu", SystemdScope.System, It.IsAny<CancellationToken>())).ReturnsAsync([
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
        var service = new Mock<IPowerShellModuleClient>();
        service.Setup(x => x.GetSystemdServicePreviewAsync("Ubuntu", item.Name, SystemdAction.Stop, SystemdScope.User, It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdOperationPreview("Ubuntu", item.Name, SystemdAction.Stop, SystemdScope.User, false, [], [], "token"));
        service.Setup(x => x.InvokeSystemdServiceAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdOperationResult(true, "Succeeded", item));
        service.Setup(x => x.GetSystemdServicesAsync("Ubuntu", It.IsAny<SystemdScope>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var dialogs = Dialogs(true); var vm = new ServicesTabViewModel(Instance(), service.Object, dialogs.Object);
        await vm.StopCommand.ExecuteAsync(item);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        service.Verify(x => x.InvokeSystemdServiceAsync("token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Services_JournalSeverityFilter_AppliesAfterBoundedServiceQuery()
    {
        var item = new SystemdServiceInfo(new SystemdUnitName("ssh.service"), "ssh", "active", "running", "enabled", "loaded", SystemdScope.System);
        var service = new Mock<IPowerShellModuleClient>();
        service.Setup(x => x.GetSystemdServiceDetailsAsync("Ubuntu", item.Name, item.Scope, It.IsAny<CancellationToken>())).ReturnsAsync(new SystemdServiceDetails(item, [], null));
        service.Setup(x => x.GetSystemdServiceJournalAsync("Ubuntu", item.Name, item.Scope, "", 200, It.IsAny<CancellationToken>())).ReturnsAsync([new SystemdJournalEntry("", "Info", "ready"), new SystemdJournalEntry("", "Error", "failed")]);
        var vm = new ServicesTabViewModel(Instance(), service.Object, Dialogs().Object) { JournalSeverity = "Error" };
        await vm.LoadDetailsCommand.ExecuteAsync(item);
        var entry = Assert.Single(vm.Journal); Assert.Equal("Error", entry.Severity);
        service.Verify(x => x.GetSystemdServiceJournalAsync("Ubuntu", item.Name, item.Scope, "", 200, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NetworkMode_OnlyAppliesAfterConfirmedCorePreviewToken()
    {
        var client = NetworkClient(); client.Setup(x => x.GetNetworkModeAsync(It.IsAny<WslNetworkingMode>(), It.IsAny<CancellationToken>())).ReturnsAsync((WslNetworkingMode m, CancellationToken _) => new NetworkingModeGuidance(m, m == WslNetworkingMode.Nat, true, ["test"], RestartScope.Wsl)); client.Setup(x => x.GetNetworkModePreviewAsync(WslNetworkingMode.Nat, It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkModePreview(WslNetworkingMode.Nat, new ConfigurationPreview("", "[wsl2]", [], RestartScope.Wsl), new NetworkingModeGuidance(WslNetworkingMode.Nat, true, true, [], RestartScope.Wsl), "token")); client.Setup(x => x.SetNetworkModeAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        var vm = NetworkVm(client.Object, Dialogs(true).Object);
        await vm.InitializeAsync();
        Assert.Single(vm.AvailableModes); await vm.PreviewAndApplyNetworkingModeCommand.ExecuteAsync(null);
        client.Verify(x => x.SetNetworkModeAsync("token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Network_WslProbeAndUnavailableStatusRemainExplicit()
    {
        var vm = NetworkVm(NetworkClient().Object, Dialogs().Object);
        vm.ProbeKind = NetworkProbeKind.WslInstance; await vm.RunProbeCommand.ExecuteAsync(null);
        Assert.Contains("ToolUnavailable", vm.ProbeResult);
    }

    [Fact]
    public async Task Network_ProjectsWindowsCollisionIntoPortRow()
    {
        var client = NetworkClient(); client.Setup(x => x.GetPortMappingsAsync("Ubuntu", null, It.IsAny<CancellationToken>())).ReturnsAsync([new PortMapping { Port = 8080, Protocol = "TCP", LocalAddress = "127.0.0.1" }]);
        var vm = NetworkVm(client.Object, Dialogs().Object);
        await vm.InitializeAsync();
        var row = Assert.Single(vm.PortMappings);
        Assert.False(row.HasWindowsCollision);
    }

    [Fact]
    public async Task NetworkSettings_UsesOnePreviewTokenAfterExplicitConfirmation()
    {
        var config = NetworkClient();
        var settings = new NetworkSettings(false, false, false, false, false, "3000");
        config.Setup(x => x.GetNetworkSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        config.Setup(x => x.GetNetworkSettingsPreviewAsync(settings, It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkSettingsPreview(settings, new ConfigurationPreview("", "[wsl2]", ["wsl2.ignoredPorts"], RestartScope.Wsl), "settings-token"));
        config.Setup(x => x.SetNetworkSettingsAsync("settings-token", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        var vm = NetworkVm(config.Object, Dialogs(true).Object);
        await vm.InitializeAsync();
        await vm.PreviewAndApplyNetworkSettingsCommand.ExecuteAsync(null);
        config.Verify(x => x.SetNetworkSettingsAsync("settings-token", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("restart", vm.NetworkSettingsEvidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BrowserCommand_OnlyPassesSafeLoopbackEndpointToModule()
    {
        var client = NetworkClient();
        var vm = NetworkVm(client.Object, Dialogs().Object);
        vm.OpenInBrowserCommand.Execute(new PortMappingViewModel { LocalAddress = "10.0.0.4", Port = 8080 });
        client.Verify(x => x.OpenNetworkLoopbackAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        await vm.OpenInBrowserCommand.ExecuteAsync(new PortMappingViewModel { LocalAddress = "::1", Port = 8080 });
        client.Verify(x => x.OpenNetworkLoopbackAsync("::1", 8080, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static NetworkTabViewModel NetworkVm(IPowerShellModuleClient client, IDialogService dialogs)
    {
        return new(Instance(), dialogs, client);
    }
    private static Mock<IPowerShellModuleClient> NetworkClient() { var client = new Mock<IPowerShellModuleClient>(); client.Setup(x => x.GetInstanceIpAddressAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null); client.Setup(x => x.GetPortMappingsAsync("Ubuntu", null, It.IsAny<CancellationToken>())).ReturnsAsync([]); client.Setup(x => x.GetNetworkStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FirewallStatus(FirewallStatusAvailability.Unavailable, "test")); client.Setup(x => x.GetFirewallRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]); client.Setup(x => x.GetNetworkSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkSettings()); client.Setup(x => x.GetNetworkModeAsync(It.IsAny<WslNetworkingMode>(), It.IsAny<CancellationToken>())).ReturnsAsync((WslNetworkingMode mode, CancellationToken _) => new NetworkingModeGuidance(mode, mode == WslNetworkingMode.Nat, true, [], RestartScope.None)); client.Setup(x => x.ProbeNetworkAsync(It.IsAny<NetworkProbeRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkProbeResult(new(NetworkProbeKind.WslInstance, "localhost"), NetworkProbeOutcome.ToolUnavailable, "test")); return client; }
    private static Mock<IDialogService> Dialogs(bool confirm = false) { var result = new Mock<IDialogService>(); result.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirm); return result; }
    private static WslInstanceViewModel Instance() => new(new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IServiceProvider>());
}
