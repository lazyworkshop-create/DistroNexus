using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for managing the distribution catalog.
/// </summary>
public interface ICatalogService
{
    /// <summary>
    /// Loads the distribution catalog from the configured source.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of available distribution packages.</returns>
    Task<List<DistroPackage>> LoadCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the catalog from the remote source.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RefreshCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for distributions matching the specified query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of matching distribution packages.</returns>
    Task<List<DistroPackage>> SearchDistributionsAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific distribution by its ID.
    /// </summary>
    /// <param name="id">The distribution ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The distribution package, or null if not found.</returns>
    Task<DistroPackage?> GetDistributionByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the local cache path for the catalog.
    /// </summary>
    /// <returns>The full path to the cached catalog file.</returns>
    string GetCatalogCachePath();

    /// <summary>
    /// Deletes a cached distribution package.
    /// </summary>
    /// <param name="packageId">The package ID to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DeleteCachedPackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a custom catalog source URL.
    /// </summary>
    /// <param name="sourceUrl">The URL of the custom catalog source.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task AddCustomSourceAsync(string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache usage statistics including total size and cached packages.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Cache usage information.</returns>
    Task<CacheUsageInfo> GetCacheUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached packages.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of files deleted.</returns>
    Task<int> ClearAllCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the package cache directory path.
    /// </summary>
    /// <returns>The full path to the package cache directory.</returns>
    string GetPackageCachePath();
}
