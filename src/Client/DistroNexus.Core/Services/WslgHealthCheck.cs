using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Read-only WSLg prerequisite and stale-shortcut guidance. It never starts distributions.</summary>
public sealed class WslgHealthCheck : IHealthCheck
{
    private readonly IProcessRunner? _runner;
    public WslgHealthCheck(IProcessRunner? runner = null) => _runner=runner;
    public HealthCheckDescriptor Descriptor { get; } = new("host.wslg", HealthScope.Host, []);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var f=new List<HealthFinding>();
        if(!context.Host.Capabilities.TryGetValue(CapabilityId.Wslg,out var wslg) || !wslg.IsSupported)
            f.Add(new("wslg.environment.unavailable",HealthSeverity.Warning,HealthScope.Host,"WSLg environment unavailable","WSLg display, audio, and application shortcuts are unavailable until WSLg is installed and supported."));
        else
        {
            f.Add(new("wslg.environment.healthy",HealthSeverity.Healthy,HealthScope.Host,"WSLg environment","WSLg host capability is available."));
            foreach(var instance in context.Instances.Where(x=>x.IsRunning))
            {
                // Only a confirmed supported host may run this bounded, read-only env probe.
                var env=_runner is null ? string.Empty : (await _runner.RunAsync(new ProcessRequest("wsl.exe",["--distribution",instance.Name,"--exec","/usr/bin/env"],TimeSpan.FromSeconds(10),64*1024,4096),cancellationToken)).StandardOutput;
                var hasWayland=env.Contains("WAYLAND_DISPLAY=",StringComparison.Ordinal); var hasDisplay=env.Contains("DISPLAY=",StringComparison.Ordinal); var hasAudio=env.Contains("PULSE_SERVER=",StringComparison.Ordinal) || env.Contains("PIPEWIRE_",StringComparison.Ordinal);
                f.Add(new($"wslg.{instance.Name}.wayland",hasWayland?HealthSeverity.Healthy:HealthSeverity.Warning,HealthScope.Instance,"WSLg Wayland display",hasWayland?"Wayland display environment is available.":"Wayland display environment was not reported.",instance.Name));
                f.Add(new($"wslg.{instance.Name}.display",hasDisplay?HealthSeverity.Healthy:HealthSeverity.Information,HealthScope.Instance,"WSLg display",hasDisplay?"Display environment is available.":"Display environment was not reported.",instance.Name));
                f.Add(new($"wslg.{instance.Name}.audio",hasAudio?HealthSeverity.Healthy:HealthSeverity.Information,HealthScope.Instance,"WSLg audio",hasAudio?"Audio environment is available.":"Audio environment was not reported.",instance.Name));
            }
        }
        foreach(var instance in context.Instances.Where(x=>!x.IsRunning)) f.Add(new($"wslg.{instance.Name}.stale-shortcut",HealthSeverity.Information,HealthScope.Instance,"Possible stale WSLg shortcut","The distribution is stopped; a Windows WSLg shortcut may fail until it is started again.",instance.Name));
        return new HealthCheckResult(Descriptor.Id,f,DateTimeOffset.UtcNow);
    }
}
