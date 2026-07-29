namespace DistroNexus.Core.Models;

/// <summary>Sanitized outcome of a catalog refresh operation.</summary>
public sealed record CatalogRefreshResult(bool Succeeded, string? SourceId, string CacheState, string DiagnosticCode);
