using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IPlatformCapabilityService
{
    Task<PlatformCapabilitySnapshot> GetHostSnapshotAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task<InstanceCapabilitySnapshot> GetInstanceSnapshotAsync(string instanceName, bool forceRefresh = false, CancellationToken cancellationToken = default);
    void InvalidateHostCapabilities();
    void InvalidateOptionalDependency(CapabilityId dependency);
    void InvalidateInstance(string instanceName);
    void InvalidateAll();
}
