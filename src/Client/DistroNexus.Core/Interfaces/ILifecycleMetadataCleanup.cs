namespace DistroNexus.Core.Interfaces;

/// <summary>Core-owned cleanup of metadata that is invalid once a registered instance is removed.</summary>
public interface ILifecycleMetadataCleanup { Task CleanupRemovedInstanceAsync(string instanceName, CancellationToken cancellationToken = default); }
