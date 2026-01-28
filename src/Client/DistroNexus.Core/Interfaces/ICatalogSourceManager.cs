using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for managing catalog sources.
/// </summary>
public interface ICatalogSourceManager
{
    /// <summary>
    /// Gets all configured catalog sources.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of catalog sources.</returns>
    Task<List<CatalogSource>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new catalog source.
    /// </summary>
    /// <param name="source">The source to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The added source.</returns>
    Task<CatalogSource> AddSourceAsync(CatalogSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing catalog source.
    /// </summary>
    /// <param name="source">The source to update.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated source.</returns>
    Task<CatalogSource> UpdateSourceAsync(CatalogSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a catalog source.
    /// </summary>
    /// <param name="sourceId">The ID of the source to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the source was removed, false if not found.</returns>
    Task<bool> RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests if a catalog source is accessible.
    /// </summary>
    /// <param name="sourceUrl">The URL of the source to test.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the source is accessible, false otherwise.</returns>
    Task<bool> TestSourceAsync(string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a source as active/inactive.
    /// </summary>
    /// <param name="sourceId">The ID of the source.</param>
    /// <param name="isActive">Whether the source should be active.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> SetSourceActiveAsync(string sourceId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders catalog sources.
    /// </summary>
    /// <param name="sourceIds">The list of source IDs in the new order.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> ReorderSourcesAsync(List<string> sourceIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default catalog sources.
    /// </summary>
    /// <returns>A list of default catalog sources.</returns>
    List<CatalogSource> GetDefaultSources();

    /// <summary>
    /// Resets to default sources.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}