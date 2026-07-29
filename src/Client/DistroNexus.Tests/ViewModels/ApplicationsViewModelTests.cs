using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Moq;

namespace DistroNexus.Tests.ViewModels;
public sealed class ApplicationsViewModelTests
{
    [Fact]
    public async Task UnavailableStatus_DisablesActionsAndExplainsState()
    {
        var module = Module(new WslgDiscoveryResult(new(false,"WSLg unavailable: unsupported.",[]),null,null,[])); var vm=new ApplicationsViewModel(module.Object) { DistributionName="Ubuntu" };
        await vm.RefreshCommand.ExecuteAsync(null); Assert.False(vm.IsAvailable); Assert.Contains("unavailable",vm.Status,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task SearchAndPin_UpdateVisibleApplications()
    {
        var module=Module(Result()); var vm=new ApplicationsViewModel(module.Object){DistributionName="Ubuntu"}; await vm.RefreshCommand.ExecuteAsync(null); vm.SearchText="Code"; var app=Assert.Single(vm.FilteredApplications); vm.SelectedApplication=app; await vm.TogglePinCommand.ExecuteAsync(null); Assert.True(vm.SelectedApplication!.IsPinned); module.Verify(x=>x.SetWslgApplicationPinAsync("a",app.ApplicationId,true,It.IsAny<CancellationToken>()));
    }
    [Fact]
    public async Task Reveal_DelegatesOnlyForSelectedApplication()
    {
        var module=Module(Result()); var vm=new ApplicationsViewModel(module.Object){DistributionName="Ubuntu"}; await vm.RefreshCommand.ExecuteAsync(null); vm.SelectedApplication=Assert.Single(vm.Applications); await vm.RevealEntryCommand.ExecuteAsync(null); module.Verify(x=>x.RevealWslgApplicationAsync("a","id",It.IsAny<CancellationToken>()));
    }
    [Fact]
    public async Task FailedActionResult_ClearsTheDiscoveryGrant()
    {
        var module=Module(Result()); module.Setup(x=>x.LaunchWslgApplicationAsync("a","id",It.IsAny<CancellationToken>())).ReturnsAsync(new WslgActionResult(false,"expired"));
        var vm=new ApplicationsViewModel(module.Object){DistributionName="Ubuntu"}; await vm.RefreshCommand.ExecuteAsync(null); vm.SelectedApplication=Assert.Single(vm.Applications);
        await vm.LaunchCommand.ExecuteAsync(null); await vm.RevealEntryCommand.ExecuteAsync(null);
        module.Verify(x=>x.RevealWslgApplicationAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()), Times.Never);
    }
    private static WslgDiscoveryResult Result() => new(new(true,"available",[]),"a",DateTimeOffset.UtcNow.AddMinutes(2),[new("id","Code",["Development"],false,null)]);
    private static Mock<IPowerShellModuleClient> Module(WslgDiscoveryResult result) { var mock=new Mock<IPowerShellModuleClient>(); mock.Setup(x=>x.DiscoverWslgApplicationsAsync("Ubuntu",It.IsAny<CancellationToken>())).ReturnsAsync(result); mock.Setup(x=>x.SetWslgApplicationPinAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync(new WslgActionResult(true,"ok")); mock.Setup(x=>x.RevealWslgApplicationAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new WslgActionResult(true,"ok")); return mock; }
}
