using System.Text.Json;
using DistroNexus.Core.Exceptions;
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
    private readonly IPowerShellService _powerShellService;
    private readonly HttpClient _httpClient;
    private List<DistroPackage>? _cachedCatalog;
    private readonly string _catalogCachePath;
    private readonly string _localCatalogPath;

    public CatalogService(
        ILogger<CatalogService> logger, 
        ISettingsService settingsService,
        IPowerShellService powerShellService,
        HttpClient httpClient,
        string? catalogCachePath = null,
        string? localCatalogPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        
        _catalogCachePath = catalogCachePath ?? Path.Combine(appFolder, "catalog.json");
        
        
        // Local fallback path - try multiple locations
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localCatalogPath = localCatalogPath ?? FindLocalCatalogPath(baseDir);
        
        _logger.LogInformation("CatalogService initialized. Local catalog path: {LocalPath}, Exists: {Exists}", 
            _localCatalogPath, File.Exists(_localCatalogPath));
    }

    private static string FindLocalCatalogPath(string baseDir)
    {
        return AppResourcePathResolver.FindFileInBaseOrParents(baseDir, Path.Combine("config", "catalog.json"));
    }

    /// <inheritdoc/>
    public async Task<List<DistroPackage>> LoadCatalogAsync(bool forceReload = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!forceReload && _cachedCatalog is not null)
            return ClonePackages(_cachedCatalog);

        // Read operations are intentionally native and side-effect free.  The PowerShell
        // service remains only for the separately migrated refresh/delete operations below.
        var catalog = await ReadCatalogAsync(_catalogCachePath, cancellationToken)
            ?? await ReadCatalogAsync(_localCatalogPath, cancellationToken)
            ?? [];
        UpdatePackageCacheStatus(catalog);
        _cachedCatalog = ClonePackages(catalog);
        return ClonePackages(_cachedCatalog);
    }

    /// <inheritdoc/>
    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Refreshing catalog via PowerShell Update-DistroNexusCatalog");
            
            // Call PowerShell module to update catalog
            var success = await _powerShellService.ExecuteAsync<bool>(
                "Update-DistroNexusCatalog",
                null,
                cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Catalog updated successfully");
                
                // Clear cache to force reload
                _cachedCatalog = null;
                
                // Reload from PowerShell
                await LoadCatalogAsync(false, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Catalog update returned false, using existing catalog");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh catalog via PowerShell module");
        }
    }

    /// <summary>
    /// Caches the catalog to local storage for offline use.
    /// </summary>
    private async Task CacheCatalogAsync(List<DistroPackage> catalog, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheOptions = new JsonSerializerOptions { WriteIndented = true };
            var cacheJson = JsonSerializer.Serialize(catalog, cacheOptions);
            await File.WriteAllTextAsync(_catalogCachePath, cacheJson, cancellationToken);
            _logger.LogDebug("Catalog cached to {Path}", _catalogCachePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache catalog");
        }
    }

    /// <inheritdoc/>
    public async Task<List<DistroPackage>> SearchDistributionsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await LoadCatalogAsync(false, cancellationToken);

        try
        {
            var catalog = await LoadCatalogAsync(false, cancellationToken);
            var normalizedQuery = query.Trim().ToLowerInvariant();

            var results = catalog.Where(d =>
                d.Name.ToLowerInvariant().Contains(normalizedQuery) ||
                d.Description.ToLowerInvariant().Contains(normalizedQuery) ||
                d.Category.ToLowerInvariant().Contains(normalizedQuery) ||
                d.Id.ToLowerInvariant().Contains(normalizedQuery)
            ).ToList();

            _logger.LogInformation("Found {Count} distributions matching query '{Query}'", results.Count, query);
            return ClonePackages(results);
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
            var catalog = await LoadCatalogAsync(false, cancellationToken);
            var distro = catalog.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (distro == null)
            {
                _logger.LogWarning("Distribution with ID '{Id}' not found", id);
            }

            return distro is null ? null : ClonePackage(distro);
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
            _logger.LogInformation("Deleting cached package {PackageId} via PowerShell", packageId);

            // Find the package in catalog to get its DefaultName or LocalPath
            var package = _cachedCatalog?.FirstOrDefault(p => p.Id == packageId);
            if (package == null)
            {
                _logger.LogWarning("Package {PackageId} not found in catalog", packageId);
                return;
            }

            Dictionary<string, object>? parameters = null;
            
            // Use LocalPath if available, otherwise use DefaultName
            if (!string.IsNullOrWhiteSpace(package.LocalPath) && File.Exists(package.LocalPath))
            {
                parameters = new Dictionary<string, object>
                {
                    { "LocalPath", package.LocalPath },
                    { "Force", true }
                };
            }
            else if (!string.IsNullOrWhiteSpace(package.DefaultName))
            {
                parameters = new Dictionary<string, object>
                {
                    { "DefaultName", package.DefaultName },
                    { "Force", true }
                };
            }
            else
            {
                _logger.LogWarning("Cannot delete package {PackageId}: no LocalPath or DefaultName", packageId);
                return;
            }

            // Call PowerShell module to remove package
            var success = await _powerShellService.ExecuteAsync<bool>(
                "Remove-DistroNexusPackage",
                parameters,
                cancellationToken);

            if (success)
            {
                _logger.LogInformation("Successfully deleted cached package {PackageId}", packageId);
                
                // Update cached catalog to mark as not cached
                package.IsCached = false;
                package.LocalPath = string.Empty;
                package.FileSize = 0;
            }
            else
            {
                _logger.LogWarning("Failed to delete cached package {PackageId}", packageId);
            }
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
                throw new WslOperationFailedException(
                    "No packages found in the custom source.",
                    DistroNexusErrorCode.WslConfigReadFailed,
                    operation: "AddCustomSource");
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
            
            // Reload catalog via PowerShell to get correct cache status
            await LoadCatalogAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add custom source: {Url}", sourceUrl);
            throw;
        }
    }

    /// <inheritdoc/>
    public string GetPackageCachePath()
    {
        // Load settings to get configured cache path
        var settings = _settingsService.LoadSettings();
        var cachePath = settings.PackageCachePath;

        // Fallback to default AppData path if not configured
        if (string.IsNullOrWhiteSpace(cachePath))
        {
            cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DistroNexus",
                "packages");
        }

        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }

        return cachePath;
    }

    /// <inheritdoc/>
    public async Task<CacheUsageInfo> GetCacheUsageAsync(CancellationToken cancellationToken = default)
    {
        var result = new CacheUsageInfo
        {
            CachePath = GetPackageCachePath()
        };

        try
        {
            _logger.LogInformation("Getting cache usage from {CachePath}", result.CachePath);

            if (!Directory.Exists(result.CachePath))
            {
                return result;
            }

            var files = Directory.GetFiles(result.CachePath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(file);
                result.TotalSizeBytes += fileInfo.Length;

                var cachedPackage = new CachedPackageInfo
                {
                    FilePath = file,
                    FileName = fileInfo.Name,
                    SizeBytes = fileInfo.Length,
                    CachedDate = fileInfo.CreationTime,
                    LastAccessedDate = fileInfo.LastAccessTime,
                    PackageId = Path.GetFileNameWithoutExtension(fileInfo.Name),
                    Name = Path.GetFileNameWithoutExtension(fileInfo.Name)
                };

                result.CachedPackages.Add(cachedPackage);
            }

            result.PackageCount = result.CachedPackages.Count;

            _logger.LogInformation("Cache usage: {Count} files, {Size} bytes", 
                result.PackageCount, result.TotalSizeBytes);

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache usage");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> ClearAllCacheAsync(CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;
        var cachePath = GetPackageCachePath();

        try
        {
            _logger.LogInformation("Clearing all cache from {CachePath}", cachePath);

            if (!Directory.Exists(cachePath))
            {
                return 0;
            }

            var files = Directory.GetFiles(cachePath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.Delete(file);
                    deletedCount++;
                    _logger.LogDebug("Deleted cached file: {FilePath}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cached file: {FilePath}", file);
                }
            }

            // Update cached catalog to mark all as not cached
            if (_cachedCatalog != null)
            {
                foreach (var package in _cachedCatalog)
                {
                    package.IsCached = false;
                }
            }

            _logger.LogInformation("Cleared {Count} files from cache", deletedCount);

            return await Task.FromResult(deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            throw;
        }
    }

    /// <summary>
    /// Updates the IsCached property for all packages based on actual file existence.
    /// </summary>
    private void UpdatePackageCacheStatus(List<DistroPackage> packages)
    {
        try
        {
            var cachePath = ResolvePackageCachePath();
            
            if (!Directory.Exists(cachePath))
            {
                _logger.LogDebug("Cache directory does not exist: {CachePath}", cachePath);
                foreach (var package in packages)
                {
                    package.IsCached = false;
                    package.LocalPath = string.Empty;
    }

                return;
            }

            // Build a dictionary of cached files with their paths and sizes (files > 0 bytes only)
            var cachedFiles = Directory.GetFiles(cachePath, "*.*", SearchOption.AllDirectories)
                .Select(f => new { Path = f, Name = Path.GetFileName(f).ToLowerInvariant(), Size = new FileInfo(f).Length })
                .Where(f => f.Size > 0) // Ignore zero-byte files
                .ToDictionary(f => f.Name, f => new { f.Path, f.Size });

            _logger.LogDebug("Checking cache status for {Count} packages against {FileCount} cached files", 
                packages.Count, cachedFiles.Count);

            foreach (var package in packages)
            {
                // Extract filename from download URL if not already set
                var expectedFileName = !string.IsNullOrWhiteSpace(package.LocalPath)
                    ? Path.GetFileName(package.LocalPath)
                    : !string.IsNullOrWhiteSpace(package.DownloadUrl)
                        ? Path.GetFileName(new Uri(package.DownloadUrl).LocalPath)
                        : null;

                if (!string.IsNullOrWhiteSpace(expectedFileName))
                {
                    var normalizedFileName = expectedFileName.ToLowerInvariant();
                    if (cachedFiles.TryGetValue(normalizedFileName, out var fileInfo))
                    {
                        package.IsCached = true;
                        package.LocalPath = fileInfo.Path;
                        package.FileSize = fileInfo.Size;
                        _logger.LogDebug("Package {Name} is cached at {Path} with size {Size} bytes", 
                            package.Name, package.LocalPath, package.FileSize);
                    }
                    else
                    {
                        package.IsCached = false;
                        package.LocalPath = string.Empty;
                        package.FileSize = 0;
                    }
                }
                else
                {
                    package.IsCached = false;
                    package.LocalPath = string.Empty;
                    package.FileSize = 0;
                }
            }

            var cachedCount = packages.Count(p => p.IsCached);
            _logger.LogInformation("Updated cache status: {CachedCount}/{TotalCount} packages are cached", 
                cachedCount, packages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update package cache status");
        }
    }

    private async Task<List<DistroPackage>?> ReadCatalogAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = File.OpenRead(path);
            var catalog = await JsonSerializer.DeserializeAsync<List<DistroPackage>>(stream, cancellationToken: cancellationToken);
            return catalog is { Count: > 0 } ? catalog : null;
        }
        catch (JsonException ex) { _logger.LogWarning(ex, "Ignoring invalid catalog at {Path}", path); return null; }
        catch (IOException ex) { _logger.LogWarning(ex, "Could not read catalog at {Path}", path); return null; }
    }

    private string ResolvePackageCachePath()
    {
        var configured = _settingsService.LoadSettings().PackageCachePath;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus", "packages")
            : configured;
    }

    private static List<DistroPackage> ClonePackages(IEnumerable<DistroPackage> packages) => packages.Select(ClonePackage).ToList();
    private static DistroPackage ClonePackage(DistroPackage package) => JsonSerializer.Deserialize<DistroPackage>(JsonSerializer.Serialize(package))!;
}
