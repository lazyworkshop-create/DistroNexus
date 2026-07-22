using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistroNexus.WorkspaceBridge;

/// <summary>Fixed production composition; tests may inject only an in-memory runner directly.</summary>
public static class ContainerRuntimeBridgeComposition
{
    public static IContainerRuntimeService Create(IProcessRunner runner, ISystemdService systemd)
    {
        var instances = new BridgeWslManagerService(runner);
        var docker = new DockerIntegrationService(NullLogger<DockerIntegrationService>.Instance, instances);
        return new ContainerRuntimeService([
            new DockerDesktopRuntimeAdapter(docker, runner),
            new PodmanWslRuntimeAdapter(runner),
            new PodmanDesktopRuntimeAdapter(runner, new WindowsPodmanDesktopInstallationDetector())], systemd, runner);
    }
}
