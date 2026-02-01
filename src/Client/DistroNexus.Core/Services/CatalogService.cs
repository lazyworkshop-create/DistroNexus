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
    private readonly string _localCatalogPath;

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
        
        
        // Local fallback path - try multiple locations
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localCatalogPath = FindLocalCatalogPath(baseDir);
        
        _logger.LogInformation("CatalogService initialized. Local catalog path: {LocalPath}, Exists: {Exists}", 
            _localCatalogPath, File.Exists(_localCatalogPath));
    }

    private static string FindLocalCatalogPath(string baseDir)
    {
        // Try multiple possible paths for the local distros.json file
        string[] possiblePaths =
        [
            Path.Combine(baseDir, "config", "distros.json"),
            Path.Combine(baseDir, @"..\config\distros.json"),
            Path.Combine(baseDir, @"..\..\config\distros.json"),
            Path.Combine(baseDir, @"..\..\..\config\distros.json"),
            Path.Combine(baseDir, @"..\..\..\..\config\distros.json"),
            Path.Combine(baseDir, @"..\..\..\..\..\config\distros.json"),
            @"D:\wsl\DistroNexus\config\distros.json" // Direct path as final fallback
        ];

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Return a default path even if it doesn't exist
        return Path.Combine(baseDir, "config", "distros.json");
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
                _cachedCatalog = JsonSerializer.Deserialize<List<DistroPackage>>(json) ?? [];
                
                if (_cachedCatalog.Count > 0)
                {
                    _logger.LogInformation("Loaded {Count} distributions from cache", _cachedCatalog.Count);
                    return _cachedCatalog;
                }
            }

            // If no cache or empty, try local file first, then remote
            await RefreshCatalogAsync(cancellationToken);
            return _cachedCatalog ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog");
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        string? json = null;

        // Try remote URL first
        try
        {
            var settings = _settingsService.LoadSettings();
            var catalogUrl = settings.CatalogUrl;

            _logger.LogInformation("Refreshing catalog from {CatalogUrl}", catalogUrl);
            json = await _httpClient.GetStringAsync(catalogUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch catalog from remote, trying local file");
        }

        // Fallback to local file if remote fails
        if (string.IsNullOrEmpty(json) && File.Exists(_localCatalogPath))
        {
            _logger.LogInformation("Loading catalog from local file: {LocalPath}", _localCatalogPath);
            json = await File.ReadAllTextAsync(_localCatalogPath, cancellationToken);
        }

        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException("Failed to load catalog from both remote URL and local file");
        }

        // Parse the nested JSON format and convert to DistroPackage list
        _cachedCatalog = ParseCatalogJson(json);

        // Save to cache in flat format
        var cacheOptions = new JsonSerializerOptions { WriteIndented = true };
        var cacheJson = JsonSerializer.Serialize(_cachedCatalog, cacheOptions);
        await File.WriteAllTextAsync(_catalogCachePath, cacheJson, cancellationToken);

        _logger.LogInformation("Catalog refreshed successfully with {Count} distributions", _cachedCatalog.Count);
    }

    /// <summary>
    /// Parses the nested catalog JSON format and converts to a flat list of DistroPackage.
    /// </summary>
    private List<DistroPackage> ParseCatalogJson(string json)
    {
        var packages = new List<DistroPackage>();

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Check if it's already in flat format (array)
            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<DistroPackage>>(json) ?? [];
            }

            // Parse nested format: { "1": { "Name": "Ubuntu", "Versions": { ... } }, ... }
            foreach (var familyProperty in root.EnumerateObject())
            {
                var familyObj = familyProperty.Value;
                
                if (!familyObj.TryGetProperty("Name", out var familyNameElement))
                    continue;
                    
                var familyName = familyNameElement.GetString() ?? "Unknown";
                
                if (!familyObj.TryGetProperty("Versions", out var versionsElement))
                    continue;

                foreach (var versionProperty in versionsElement.EnumerateObject())
                {
                    var versionObj = versionProperty.Value;
                    
                    var name = versionObj.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    var url = versionObj.TryGetProperty("Url", out var u) ? u.GetString() ?? "" : "";
                    var defaultName = versionObj.TryGetProperty("DefaultName", out var dn) ? dn.GetString() ?? "" : "";
                    var filename = versionObj.TryGetProperty("Filename", out var fn) ? fn.GetString() ?? "" : "";
                    var source = versionObj.TryGetProperty("Source", out var s) ? s.GetString() ?? "" : "";
                    var localPath = versionObj.TryGetProperty("LocalPath", out var lp) ? lp.GetString() ?? "" : "";

                    var package = new DistroPackage
                    {
                        Id = $"{familyProperty.Name}-{versionProperty.Name}",
                        Name = name,
                        Category = familyName,
                        DownloadUrl = url,
                        Description = $"{name} - {source}",
                        IsOfficial = source.Equals("Official", StringComparison.OrdinalIgnoreCase),
                        Version = defaultName,
                        LocalPath = localPath
                    };

                    packages.Add(package);
                }
            }

            _logger.LogInformation("Parsed {Count} distributions from nested catalog format", packages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse catalog JSON");
        }

        return packages;
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

            var settings = _settingsService.LoadSettings();
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

    /// <inheritdoc/>
    public string GetPackageCachePath()
    {
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DistroNexus",
            "packages");

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
}
