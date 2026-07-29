using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class WorkspaceActionCapabilityGate : IWorkspaceActionCapabilityGate
{
    private readonly IPlatformCapabilityService _capabilities;
    public WorkspaceActionCapabilityGate(IPlatformCapabilityService capabilities) => _capabilities = capabilities;

    public async Task EnsureAvailableAsync(WorkspaceDefinition definition, WorkspaceActionType actionType, CancellationToken cancellationToken)
    {
        var capability = actionType switch
        {
            WorkspaceActionType.Terminal => CapabilityId.WindowsTerminal,
            WorkspaceActionType.VisualStudioCode => CapabilityId.VisualStudioCode,
            WorkspaceActionType.Systemd => CapabilityId.InstanceSystemd,
            WorkspaceActionType.DockerCompose => CapabilityId.DockerDesktop,
            WorkspaceActionType.PodmanCompose => CapabilityId.Podman,
            _ => CapabilityId.Wsl
        };
        CapabilityResult? result;
        if (capability is CapabilityId.InstanceSystemd)
        {
            var snapshot = await _capabilities.GetInstanceSnapshotAsync(definition.InstanceName, cancellationToken: cancellationToken);
            snapshot.Capabilities.TryGetValue(capability, out result);
        }
        else
        {
            var snapshot = await _capabilities.GetHostSnapshotAsync(cancellationToken: cancellationToken);
            result = snapshot.Capabilities.TryGetValue(capability, out var host) ? host : snapshot.OptionalDependencies.TryGetValue(capability, out var dependency) ? dependency : null;
        }
        if (result is null || !result.IsSupported)
        {
            var reason = result?.ReasonCode ?? "NotReported";
            throw new InvalidOperationException($"Workspace.Capability.{capability}.{reason}");
        }
    }
}

/// <summary>Safe default for compositions that forgot to register capability probing.</summary>
public sealed class FailClosedWorkspaceActionCapabilityGate : IWorkspaceActionCapabilityGate
{
    public Task EnsureAvailableAsync(WorkspaceDefinition definition, WorkspaceActionType actionType, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException($"Workspace.Capability.{actionType}.GateUnavailable"));
}
