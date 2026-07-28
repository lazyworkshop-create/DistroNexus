namespace DistroNexus.Core.Models;

/// <summary>Public-safe result of a package cache deletion.</summary>
public sealed record PackageCacheDeleteResult(bool Deleted, string DiagnosticCode);

/// <summary>Public-safe result of a package cache clear operation.</summary>
public sealed record PackageCacheClearResult(int DeletedCount, int FailedCount, string DiagnosticCode);

/// <summary>Modeled, read-only package-cache location result.</summary>
public sealed record PackageCacheLocationResult(string CachePath);

/// <summary>Fixed delete authority: token is normal; legacy selectors are resolution hints only.</summary>
public sealed record PackageCacheDeleteRequest(string? CacheEntryId = null, string? DefaultName = null, string? LocalPath = null);
