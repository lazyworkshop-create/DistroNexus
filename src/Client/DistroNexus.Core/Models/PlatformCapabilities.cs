using System.Collections.ObjectModel;

namespace DistroNexus.Core.Models;

public enum CapabilityStatus
{
    Supported,
    Unsupported,
    Unavailable,
    RequiresUpdate,
    RequiresElevation,
    Unknown
}

public enum CapabilityId
{
    HostFacts,
    Wsl,
    SparseVhd,
    MirroredNetworking,
    VhdExport,
    ImportInPlace,
    Systemd,
    Wslg,
    GpuCompute,
    UsbIp,
    WindowsTerminal,
    VisualStudioCode,
    DockerDesktop,
    Podman,
    UsbIpd,
    TaskScheduler,
    DistributionIdentity,
    InstanceWslVersion,
    InstanceSystemd
}

public enum CapabilitySource
{
    WslCli,
    InstanceCli,
    DependencyCli,
    OperatingSystem,
    ProcessIdentity
}

public enum CapabilityCacheKind { Stable, Dependency, Volatile }

public enum RestartScope { None, Instance, Wsl, Application }

public sealed record CapabilityResult(
    CapabilityId Id,
    CapabilityStatus Status,
    string ReasonCode,
    CapabilitySource Source,
    DateTimeOffset CheckedAt,
    Version? DetectedVersion = null,
    Version? MinimumVersion = null,
    IReadOnlyDictionary<string, string>? Evidence = null,
    RestartScope RestartScope = RestartScope.None)
{
    public bool IsSupported => Status == CapabilityStatus.Supported;
}

public sealed record HostPlatformFacts(
    string Edition,
    Version WindowsVersion,
    string Architecture,
    bool IsElevated,
    string? WslInstallationSource,
    Version? WslVersion,
    Version? KernelVersion,
    Version? WslgVersion,
    bool? UpdateAvailable);

public sealed record InstancePlatformFacts(
    string Name,
    int? WslVersion,
    string? DistributionId,
    string? DistributionVersion,
    bool? SystemdAvailable,
    bool? SystemdRunning);

public sealed record PlatformCapabilitySnapshot(
    HostPlatformFacts Host,
    IReadOnlyDictionary<CapabilityId, CapabilityResult> Capabilities,
    IReadOnlyDictionary<CapabilityId, CapabilityResult> OptionalDependencies,
    DateTimeOffset RefreshedAt)
{
    public static IReadOnlyDictionary<CapabilityId, CapabilityResult> ReadOnly(
        IDictionary<CapabilityId, CapabilityResult> values) =>
        new ReadOnlyDictionary<CapabilityId, CapabilityResult>(values);
}

public sealed record InstanceCapabilitySnapshot(
    InstancePlatformFacts Instance,
    IReadOnlyDictionary<CapabilityId, CapabilityResult> Capabilities,
    DateTimeOffset RefreshedAt);
