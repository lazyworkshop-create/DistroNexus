using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class InstanceConfigurationModuleRoutingTests
{
    [Fact]
    public async Task Save_UsesOnlyTypedReadPreviewRecoveryAndExecuteOperations()
    {
        var client = new Mock<IPowerShellModuleClient>(); var now = DateTimeOffset.UtcNow;
        client.Setup(x => x.GetInstanceConfigurationAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceConfigurationReadResult("Ubuntu", 1, new Dictionary<string, string> { ["boot.systemd"] = "false" }, "fp", "Instance.ConfigRead"));
        client.Setup(x => x.GetHostCapabilitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null), new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Systemd] = new(CapabilityId.Systemd, CapabilityStatus.Supported, "test", CapabilitySource.WslCli, now) }, new Dictionary<CapabilityId, CapabilityResult>(), now));
        client.Setup(x => x.GetInstanceCapabilitiesAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceCapabilitySnapshot(new("Ubuntu", 2, null, null, null, null), new Dictionary<CapabilityId, CapabilityResult>(), now));
        client.Setup(x => x.PreviewInstanceConfigurationAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceConfigurationPreviewResult(new string('a', 64), now.AddMinutes(1), "Ubuntu", ["boot.systemd"], "Instance.ConfigPreview"));
        client.Setup(x => x.GetInstanceConfigurationRecoveryOfferAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceConfigurationRecoveryResult("Ubuntu", "Unavailable", null, "Instance.ConfigRecoveryUnavailable"));
        client.Setup(x => x.SaveInstanceConfigurationAsync(new string('a', 64), It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceConfigurationSaveResult("Ubuntu", false, "None", "Instance.ConfigSaved"));
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var instance = new WslInstanceViewModel(new WslInstance { Name = "Ubuntu", Version = 2 }, Mock.Of<ILogger>(), client.Object, Mock.Of<IDialogService>());
        var vm = new ConfigurationTabViewModel(instance, client.Object, dialogs.Object);
        await vm.InitializeAsync(); vm.Systemd = true; await vm.SaveCommand.ExecuteAsync(null);
        client.Verify(x => x.PreviewInstanceConfigurationAsync("Ubuntu", It.Is<IReadOnlyDictionary<string, string?>>(c => c["boot.systemd"] == "true"), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.SaveInstanceConfigurationAsync(new string('a', 64), It.IsAny<CancellationToken>()), Times.Once);
    }
}
