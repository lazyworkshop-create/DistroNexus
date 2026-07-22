using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.WorkspaceBridge;

/// <summary>Read-only bridge contract for container runtime diagnostics.</summary>
public static class ContainerRuntimeBridgeHandler
{
    public static async Task<ContainerRuntimeStatusResponse> GetStatusAsync(
        IContainerRuntimeService containers,
        string instanceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(containers);
        var snapshot = await containers.GetSnapshotAsync(instanceName, cancellationToken);
        var runtimes = snapshot.Runtimes.Select(runtime => runtime with { Version = VersionSafety.Normalize(runtime.Version) }).ToArray();
        return new(instanceName, true, runtimes, snapshot.Containers, snapshot.Images, snapshot.Projects, snapshot.Failures);
    }
}

public sealed record ContainerRuntimeStatusResponse(
    string InstanceName,
    bool ReadOnly,
    IReadOnlyList<ContainerRuntimeStatus> Runtimes,
    IReadOnlyDictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>> Containers,
    IReadOnlyDictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>> Images,
    IReadOnlyDictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>> Projects,
    IReadOnlyDictionary<ContainerRuntimeKind, string> Failures);
