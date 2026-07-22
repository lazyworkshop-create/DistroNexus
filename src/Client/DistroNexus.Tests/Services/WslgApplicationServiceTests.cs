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
    private sealed class CaptureRunner : IProcessRunner { public ProcessRequest? Request {get;private set;} public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default){Request=request;var text=request.Arguments.Contains("/usr/bin/stat") ? "regular file:100:7" : request.Arguments.Contains("/bin/cat") ? "[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example \"two words\" ;not-shell\n" : "";return Task.FromResult(new ProcessResult(0,text,"",TimeSpan.Zero,false,false,false,1));} }
    private sealed class IconRunner : IProcessRunner
    {
        public int Base64Calls; private static readonly byte[] Png=BuildPng();
        private static byte[] BuildPng() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAF/gL+W4uY9wAAAABJRU5ErkJggg==");
        public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
        { var output=request.Arguments.Contains("/usr/bin/base64") ? Convert.ToBase64String(Png) : request.Arguments.Contains("/usr/bin/stat") ? "regular file:24:7" : ""; if(request.Arguments.Contains("/usr/bin/base64"))Base64Calls++; return Task.FromResult(new ProcessResult(0,output,"",TimeSpan.Zero,false,false,false,1)); }
    }
    private sealed class CapabilityStub(CapabilityStatus status) : IPlatformCapabilityService
    {
        private readonly PlatformCapabilitySnapshot _snapshot=new(new HostPlatformFacts("",new Version(1,0),"",false,null,null,null,null,null),new Dictionary<CapabilityId,CapabilityResult>{{CapabilityId.Wslg,new(CapabilityId.Wslg,status,"test",CapabilitySource.WslCli,DateTimeOffset.UtcNow)}},new Dictionary<CapabilityId,CapabilityResult>(),DateTimeOffset.UtcNow);
        public Task<PlatformCapabilitySnapshot> GetHostSnapshotAsync(bool forceRefresh=false,CancellationToken cancellationToken=default)=>Task.FromResult(_snapshot); public Task<InstanceCapabilitySnapshot> GetInstanceSnapshotAsync(string n,bool f=false,CancellationToken c=default)=>throw new NotImplementedException(); public void InvalidateHostCapabilities(){} public void InvalidateOptionalDependency(CapabilityId d){} public void InvalidateInstance(string n){} public void InvalidateAll(){}
    }
}
