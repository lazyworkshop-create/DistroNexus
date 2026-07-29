using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;
public sealed class WslgHealthCheckTests
{
    [Fact]
    public async Task SupportedHost_ReportsPerInstanceEnvironmentAndStaleShortcut()
    {
        var runner=new EnvRunner(); var check=new WslgHealthCheck(runner); var result=await check.CheckAsync(new HealthCheckContext(Host(),[new WslInstance{Name="Ubuntu",State="Running"},new WslInstance{Name="Stopped",State="Stopped"}]),default);
        Assert.Contains(result.Findings,x=>x.Id=="wslg.Ubuntu.wayland" && x.Severity==HealthSeverity.Healthy); Assert.Contains(result.Findings,x=>x.Id=="wslg.Ubuntu.audio"); Assert.Contains(result.Findings,x=>x.Id=="wslg.Stopped.stale-shortcut"); Assert.Equal(1,runner.Calls);
    }
    private static PlatformCapabilitySnapshot Host() => new(new HostPlatformFacts("",new Version(1,0),"",false,null,null,null,null,null),new Dictionary<CapabilityId,CapabilityResult>{{CapabilityId.Wslg,new(CapabilityId.Wslg,CapabilityStatus.Supported,"ok",CapabilitySource.WslCli,DateTimeOffset.UtcNow)}},new Dictionary<CapabilityId,CapabilityResult>(),DateTimeOffset.UtcNow);
    private sealed class EnvRunner : IProcessRunner { public int Calls; public Task<ProcessResult> RunAsync(ProcessRequest r,CancellationToken c=default){Calls++;return Task.FromResult(new ProcessResult(0,"WAYLAND_DISPLAY=wayland-0\nDISPLAY=:0\nPULSE_SERVER=unix:/x\n","",TimeSpan.Zero,false,false,false,1));} }
}
