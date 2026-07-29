using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class InstallWizardViewModelTests
{
    [Fact]
    public async Task QuickInstall_UsesOnlyTheTypedSettingsDefaultRoot()
    {
        var client = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        client.Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new GlobalSettings { DefaultInstallPath = "C:\\module-root" });
        client.Setup(x => x.GetPackagesAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var viewModel = new InstallWizardViewModel(client.Object, Mock.Of<ILogger<InstallWizardViewModel>>());

        await viewModel.InitializeCommand.ExecuteAsync(null);
        viewModel.ToggleQuickModeCommand.Execute(null);
        viewModel.SelectedDistribution = new DistroPackage { Id = "ubuntu", Name = "Ubuntu" };
        viewModel.GoNextCommand.Execute(null);

        Assert.Equal("C:\\module-root", viewModel.InstallPath);
        client.Verify(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
