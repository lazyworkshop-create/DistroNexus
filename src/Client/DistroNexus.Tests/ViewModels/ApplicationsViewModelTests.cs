using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.Tests.ViewModels;
public sealed class ApplicationsViewModelTests
{
    [Fact]
    public async Task UnavailableStatus_DisablesActionsAndExplainsState()
    {
        var vm=new ApplicationsViewModel(new FakeService { Status=new(false,"WSLg unavailable: unsupported.",[]) }) { DistributionName="Ubuntu" };
        await vm.RefreshCommand.ExecuteAsync(null); Assert.False(vm.IsAvailable); Assert.Contains("unavailable",vm.Status,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task SearchAndPin_UpdateVisibleApplications()
    {
        var service=new FakeService(); var vm=new ApplicationsViewModel(service){DistributionName="Ubuntu"}; await vm.RefreshCommand.ExecuteAsync(null); vm.SearchText="Code"; var app=Assert.Single(vm.FilteredApplications); vm.SelectedApplication=app; await vm.TogglePinCommand.ExecuteAsync(null); Assert.True(vm.SelectedApplication!.IsPinned); Assert.Equal(app.Id,service.Pinned);
    }
    [Fact]
    public async Task Reveal_DelegatesOnlyForSelectedApplication()
    {
        var service=new FakeService(); var vm=new ApplicationsViewModel(service){DistributionName="Ubuntu"}; await vm.RefreshCommand.ExecuteAsync(null); vm.SelectedApplication=Assert.Single(vm.Applications); await vm.RevealEntryCommand.ExecuteAsync(null); Assert.Equal("id",service.Revealed);
    }
    private sealed class FakeService : IWslgApplicationService
    {
        public WslgApplicationStatus Status {get;set;}=new(true,"available",[]); public string? Pinned; public string? Revealed;
        public Task<WslgApplicationStatus> GetStatusAsync(string n,CancellationToken c=default)=>Task.FromResult(Status);
        public Task<IReadOnlyList<WslgApplication>> DiscoverAsync(string n,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<WslgApplication>>([new("id",n,"Code","/usr/bin/code",[],["Development"],"/usr/share/applications/code.desktop",null)]);
        public Task<WslgLaunchResult> LaunchAsync(WslgApplication a,CancellationToken c=default)=>Task.FromResult(new WslgLaunchResult(true,a.InstanceName,a.Executable,"ok"));
        public Task<byte[]?> GetIconAsync(WslgApplication a,CancellationToken c=default)=>Task.FromResult<byte[]?>(null);
        public Task<WslgLaunchResult> RevealAsync(WslgApplication a,CancellationToken c=default){Revealed=a.Id;return Task.FromResult(new WslgLaunchResult(true,a.InstanceName,"","ok"));}
        public Task SetPinnedAsync(string id,bool p,CancellationToken c=default){Pinned=id;return Task.CompletedTask;}
        public Task<IReadOnlySet<string>> GetPinsAsync(CancellationToken c=default)=>Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
