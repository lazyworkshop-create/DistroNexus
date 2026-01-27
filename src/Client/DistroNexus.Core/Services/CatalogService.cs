using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for managing the distribution catalog.
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly ILogger<CatalogService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private List<DistroPackage>? _cachedCatalog;
    private readonly string _catalogCachePath;

    public CatalogService(
        ILogger<CatalogService> logger, 
        ISettingsService settingsService, 
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        _catalogCachePath = Path.Combine(appFolder, "catalog.json");
    }

    /// <inheritdoc/>
    public async Task<List<DistroPackage>> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedCatalog != null)
            return _cachedCatalog;

        try
        {
            // Try to load from cache first
            if (File.Exists(_catalogCachePath))
            {
                _logger.LogInformation("Loading catalog from cache: {CachePath}", _catalogCachePath);
                
                var json = await File.ReadAllTextAsync(_catalogCachePath, cancellationToken);
                _cachedCatalog = JsonSerializer.Deserialize<List<DistroPackage>>(json) ?? new List<DistroPackage>();
                
                _logger.LogInformation("Loaded {Count} distributions from cache", _cachedCatalog.Count);
                return _cachedCatalog;
            }

            // If no cache, refresh from remote
            await RefreshCatalogAsync(cancellationToken);
            return _cachedCatalog ?? new List<DistroPackage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog");
            return new List<DistroPackage>();
        }
    }

    /// <inheritdoc/>
    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
            var catalogUrl = settings.CatalogUrl;

            _logger.LogInformation("Refreshing catalog from {CatalogUrl}", catalogUrl);

            var json = await _httpClient.GetStringAsync(catalogUrl, cancellationToken);
            _cachedCatalog = JsonSerializer.Deserialize<List<DistroPackage>>(json) ?? new List<DistroPackage>();

            // Save to cache
            var cacheOptions = new JsonSerializerOptions { WriteIndented = true };
            var cacheJson = JsonSerializer.Serialize(_cachedCatalog, cacheOptions);
            await File.WriteAllTextAsync(_catalogCachePath, cacheJson, cancellationToken);

            _logger.LogInformation("Catalog refreshed successfully with {Count} distributions", _cachedCatalog.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh catalog");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<DistroPackage>> SearchDistributionsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await LoadCatalogAsync(cancellationToken);

        try
        {
            var catalog = await LoadCatalogAsync(cancellationToken);
            var normalizedQuery = query.Trim().ToLowerInvariant();

            var results = catalog.Where(d =>
                d.Name.ToLowerInvariant().Contains(normalizedQuery) ||
                d.Description.ToLowerInvariant().Contains(normalizedQuery) ||
                d.Category.ToLowerInvariant().Contains(normalizedQuery) ||
                d.Id.ToLowerInvariant().Contains(normalizedQuery)
            ).ToList();

            _logger.LogInformation("Found {Count} distributions matching query '{Query}'", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search distributions");
            return new List<DistroPackage>();
        }
    }

    /// <inheritdoc/>
    public async Task<DistroPackage?> GetDistributionByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));

        try
        {
            var catalog = await LoadCatalogAsync(cancellationToken);
            var distro = catalog.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (distro == null)
            {
                _logger.LogWarning("Distribution with ID '{Id}' not found", id);
            }

            return distro;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distribution by ID '{Id}'", id);
            return null;
        }
    }

    /// <inheritdoc/>
    public string GetCatalogCachePath()
    {
        return _catalogCachePath;
    }

    /// <inheritdoc/>
    public async Task DeleteCachedPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        try
        {
            _logger.LogInformation("Deleting cached package {PackageId}", packageId);

            var settings = await _settingsService.LoadSettingsAsync(cancellationToken);
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DistroNexus",
                "packages");

            if (!Directory.Exists(cachePath))
            {
                _logger.LogWarning("Package cache directory does not exist");
                return;
            }

            // Find and delete files matching the package ID
            var packageFiles = Directory.GetFiles(cachePath, $"{packageId}*");
            foreach (var file in packageFiles)
            {
                File.Delete(file);
                _logger.LogInformation("Deleted cached file: {FilePath}", file);
            }

            // Update cached catalog to mark as not cached
            if (_cachedCatalog != null)
            {
                var package = _cachedCatalog.FirstOrDefault(p => p.Id == packageId);
                if (package != null)
                {
                    package.IsCached = false;
                }
            }

            _logger.LogInformation("Deleted cached package {PackageId}", packageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cached package {PackageId}", packageId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task AddCustomSourceAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentNullException(nameof(sourceUrl));

        try
        {
            _logger.LogInformation("Adding custom source: {Url}", sourceUrl);

            // Validate and fetch the custom catalog
            var customJson = await _httpClient.GetStringAsync(sourceUrl, cancellationToken);
            var customPackages = JsonSerializer.Deserialize<List<DistroPackage>>(customJson);

            if (customPackages == null || customPackages.Count == 0)
            {
                throw new InvalidOperationException("No packages found in the custom source");
            }

            // Mark packages as custom
            foreach (var package in customPackages)
            {
                package.IsCustomSource = true;
            }

            // Add to cached catalog
            _cachedCatalog ??= new List<DistroPackage>();
            _cachedCatalog.AddRange(customPackages);

            // Save updated catalog
            var cacheOptions = new JsonSerializerOptions { WriteIndented = true };
            var cacheJson = JsonSerializer.Serialize(_cachedCatalog, cacheOptions);
            await File.WriteAllTextAsync(_catalogCachePath, cacheJson, cancellationToken);

            _logger.LogInformation("Added {Count} packages from custom source", customPackages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add custom source: {Url}", sourceUrl);
            throw;
        }
    }
}
