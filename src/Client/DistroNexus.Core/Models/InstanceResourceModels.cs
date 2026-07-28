namespace DistroNexus.Core.Models;

/// <summary>Path-free resource information for a registered WSL instance.</summary>
public sealed record InstanceResourceSnapshot(string Name, int WslVersion, bool SparseMode);

/// <summary>A short-lived, Core-issued authorization to change sparse mode.</summary>
public sealed record InstanceSparsePreview(string PreviewToken, string Name, bool Enabled, DateTimeOffset ExpiresAt, IReadOnlyList<string> Effects);

/// <summary>The result of consuming a sparse-mode preview authorization.</summary>
public sealed record InstanceSparseOperationResult(bool Succeeded, string OutcomeCode);
