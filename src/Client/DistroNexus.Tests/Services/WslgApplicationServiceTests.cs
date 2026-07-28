using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class WslgApplicationServiceTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"DistroNexus-wslg-"+Guid.NewGuid().ToString("N"));
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
    [Fact]
    public async Task Launch_UsesWslExecArgumentList_NotShellText()
    {
        var runner=new CaptureRunner(); var service=new WslgApplicationService(runner,appDataDirectory:_root);
        var app=DesktopEntryParser.Parse("Ubuntu","/usr/share/applications/example.desktop","[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example \"two words\" ;not-shell\n")!;
        var result=await service.LaunchAsync(app);
        Assert.True(result.Succeeded); Assert.Equal("wsl.exe",runner.Request!.FileName); Assert.Equal(["--distribution","Ubuntu","--exec","/usr/bin/example","two words",";not-shell"],runner.Request.Arguments);
    }
    [Fact]
    public async Task Reveal_RejectsUnsafeDesktopPath()
    {
        var runner=new CaptureRunner(); var service=new WslgApplicationService(runner,appDataDirectory:_root);
        var result=await service.RevealAsync(new WslgApplication("id","Ubuntu","X","/usr/bin/x",[],[],"/tmp/x.desktop",null));
        Assert.False(result.Succeeded); Assert.Null(runner.Request);
    }
    [Fact]
    public async Task Reveal_RebindsEntryAndUsesExactDirectoryArgument()
    {
        var runner=new CaptureRunner(); var service=new WslgApplicationService(runner,appDataDirectory:_root); var app=DesktopEntryParser.Parse("Ubuntu","/usr/share/applications/example.desktop","[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example \"two words\" ;not-shell\n")!;
        var result=await service.RevealAsync(app); Assert.True(result.Succeeded); Assert.Equal(["--distribution","Ubuntu","--exec","/usr/bin/xdg-open","/usr/share/applications"],runner.Request!.Arguments);
    }
    [Fact]
    public async Task IconRetrieval_CachesPerInstanceAndAcceptsValidatedPng()
    {
        var runner=new IconRunner(); var service=new WslgApplicationService(runner,appDataDirectory:_root);
        var first=new WslgApplication("a","Ubuntu","X","/usr/bin/x",[],[],"/usr/share/applications/x.desktop","/usr/share/icons/x.png");
        var second=first with { InstanceName="Debian" };
        Assert.NotNull(await service.GetIconAsync(first)); Assert.NotNull(await service.GetIconAsync(first)); Assert.NotNull(await service.GetIconAsync(second)); Assert.Equal(2,runner.Base64Calls);
    }
    [Fact]
    public async Task UnknownCapability_ReturnsUnavailableWithoutWslProbe()
    {
        var runner=new CaptureRunner(); var cap=new CapabilityStub(CapabilityStatus.Unknown); var service=new WslgApplicationService(runner,cap,_root);
        var status=await service.GetStatusAsync("Ubuntu"); Assert.False(status.IsAvailable); Assert.Null(runner.Request);
    }
    [Fact]
    public async Task ExpiredGrant_ReturnsExpiredAndDoesNotLaunch()
    {
        var runner=new GrantRunner(); var clock=new TestClock(DateTimeOffset.UtcNow); var service=new WslgApplicationService(runner,appDataDirectory:_root,timeProvider:clock);
        var grant=await service.DiscoverWithGrantAsync("Ubuntu"); clock.Advance(TimeSpan.FromMinutes(3));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LaunchGrantedAsync(grant.DiscoveryToken!,grant.Applications.Single().ApplicationId));
        Assert.Equal("Wslg.DiscoveryGrantExpired",error.Message); Assert.Equal(0,runner.Launches);
    }
    [Fact]
    public async Task ChangedEntryDuringPin_ReturnsChangedWithoutPinPersistence()
    {
        var runner=new GrantRunner(); var service=new WslgApplicationService(runner,appDataDirectory:_root); var grant=await service.DiscoverWithGrantAsync("Ubuntu"); runner.Content="[Desktop Entry]\nType=Application\nName=Changed\nExec=/usr/bin/changed\n";
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SetGrantedPinnedAsync(grant.DiscoveryToken!,grant.Applications.Single().ApplicationId,true));
        Assert.Equal("Wslg.EntryChanged",error.Message); Assert.Empty(await service.GetPinsAsync()); Assert.Equal(0,runner.Launches);
    }
    [Fact]
    public async Task ForeignAndForgedGrantRequests_AreRejectedBeforeProcessAction()
    {
        var firstRunner=new GrantRunner(); var first=new WslgApplicationService(firstRunner,appDataDirectory:_root); var grant=await first.DiscoverWithGrantAsync("Ubuntu");
        var secondRunner=new GrantRunner(); var second=new WslgApplicationService(secondRunner,appDataDirectory:_root+"-foreign");
        var foreign=await Assert.ThrowsAsync<InvalidOperationException>(()=>second.LaunchGrantedAsync(grant.DiscoveryToken!,grant.Applications.Single().ApplicationId));
        var forged=await Assert.ThrowsAsync<InvalidOperationException>(()=>first.SetGrantedPinnedAsync(grant.DiscoveryToken!,"forged",true));
        Assert.Equal("Wslg.DiscoveryGrantInvalid",foreign.Message); Assert.Equal("Wslg.ApplicationNotFound",forged.Message); Assert.Equal(0,secondRunner.Launches); Assert.Empty(await first.GetPinsAsync());
    }
    [Fact]
    public async Task ChangedEntryDuringLaunchAndReveal_ReturnsChangedWithoutProcessAction()
    {
        var runner=new GrantRunner(); var service=new WslgApplicationService(runner,appDataDirectory:_root); var grant=await service.DiscoverWithGrantAsync("Ubuntu"); runner.Content="[Desktop Entry]\nType=Application\nName=Changed\nExec=/usr/bin/changed\n"; var id=grant.Applications.Single().ApplicationId;
        var launch=await service.LaunchGrantedAsync(grant.DiscoveryToken!,id); var reveal=await service.RevealGrantedAsync(grant.DiscoveryToken!,id);
        Assert.False(launch.Succeeded); Assert.False(reveal.Succeeded); Assert.Equal("Wslg.EntryChanged",launch.Diagnostic); Assert.Equal("Wslg.EntryChanged",reveal.Diagnostic); Assert.Equal(0,runner.Launches);
    }
    private sealed class CaptureRunner : IProcessRunner { public ProcessRequest? Request {get;private set;} public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default){Request=request;var text=request.Arguments.Contains("/usr/bin/stat") ? "regular file:100:7" : request.Arguments.Contains("/bin/cat") ? "[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example \"two words\" ;not-shell\n" : "";return Task.FromResult(new ProcessResult(0,text,"",TimeSpan.Zero,false,false,false,1));} }
    private sealed class IconRunner : IProcessRunner
    {
        public int Base64Calls; private static readonly byte[] Png=BuildPng();
        private static byte[] BuildPng() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAF/gL+W4uY9wAAAABJRU5ErkJggg==");
        public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
        { var output=request.Arguments.Contains("/usr/bin/base64") ? Convert.ToBase64String(Png) : request.Arguments.Contains("/usr/bin/stat") ? "regular file:24:7" : ""; if(request.Arguments.Contains("/usr/bin/base64"))Base64Calls++; return Task.FromResult(new ProcessResult(0,output,"",TimeSpan.Zero,false,false,false,1)); }
    }
    private sealed class GrantRunner : IProcessRunner
    { public string Content="[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example\n"; public int Launches;
      public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default) { var output=request.Arguments.Contains("/usr/bin/env") ? "WAYLAND_DISPLAY=wayland-0\n" : request.Arguments.Contains("/usr/bin/find") ? "/usr/share/applications/example.desktop\n" : request.Arguments.Contains("/usr/bin/stat") ? "regular file:100:7" : request.Arguments.Contains("/bin/cat") ? Content : ""; if(request.Arguments.Contains("/usr/bin/example")) Launches++; return Task.FromResult(new ProcessResult(0,output,"",TimeSpan.Zero,false,false,false,1)); } }
    private sealed class TestClock(DateTimeOffset now) : TimeProvider { private DateTimeOffset _now=now; public override DateTimeOffset GetUtcNow()=>_now; public void Advance(TimeSpan value)=>_now+=value; }
    private sealed class CapabilityStub(CapabilityStatus status) : IPlatformCapabilityService
    {
        private readonly PlatformCapabilitySnapshot _snapshot=new(new HostPlatformFacts("",new Version(1,0),"",false,null,null,null,null,null),new Dictionary<CapabilityId,CapabilityResult>{{CapabilityId.Wslg,new(CapabilityId.Wslg,status,"test",CapabilitySource.WslCli,DateTimeOffset.UtcNow)}},new Dictionary<CapabilityId,CapabilityResult>(),DateTimeOffset.UtcNow);
        public Task<PlatformCapabilitySnapshot> GetHostSnapshotAsync(bool forceRefresh=false,CancellationToken cancellationToken=default)=>Task.FromResult(_snapshot); public Task<InstanceCapabilitySnapshot> GetInstanceSnapshotAsync(string n,bool f=false,CancellationToken c=default)=>throw new NotImplementedException(); public void InvalidateHostCapabilities(){} public void InvalidateOptionalDependency(CapabilityId d){} public void InvalidateInstance(string n){} public void InvalidateAll(){}
    }
}
