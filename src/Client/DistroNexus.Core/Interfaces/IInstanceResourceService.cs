using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Closed resource and sparse-mode authority for registered WSL instances.</summary>
public interface IInstanceResourceService
{
    Task<InstanceResourceSnapshot> GetAsync(string name, CancellationToken cancellationToken = default);
    Task<InstanceSparsePreview> PreviewSparseAsync(string name, bool enabled, CancellationToken cancellationToken = default);
    Task<InstanceSparseOperationResult> ExecuteSparseAsync(string previewToken, CancellationToken cancellationToken = default);
}

/// <summary>Internal boundary for the fixed registered-instance sparse executor.</summary>
public interface IRegisteredInstanceSparseAdapter
{
    Task<RegisteredInstanceSparseState?> GetAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> SetSparseAsync(string registeredName, bool enabled, CancellationToken cancellationToken = default);
}

/// <summary>Opaque registered-instance state; no registry path or process arguments cross this boundary.</summary>
public sealed record RegisteredInstanceSparseState(string Name, string Identity, int WslVersion, bool SparseMode);
