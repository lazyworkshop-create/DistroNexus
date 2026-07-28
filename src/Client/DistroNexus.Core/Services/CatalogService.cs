using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
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
    private readonly HttpClient _httpClient;
    private List<DistroPackage>? _cachedCatalog;
    private readonly string _catalogCachePath;
    private readonly string _localCatalogPath;
    private readonly string? _tokenKeyRoot;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, FileAttributes> _getAttributes;
    private readonly Action<string> _deleteFile;
    private readonly Action _beforeTokenKeyPublish;

    public CatalogService(
        ILogger<CatalogService> logger, 
        ISettingsService settingsService,
        HttpClient httpClient,
        string? catalogCachePath = null,
        string? localCatalogPath = null,
        string? tokenKeyRoot = null,
        TimeProvider? timeProvider = null,
        Func<string, FileAttributes>? getAttributes = null,
        Action<string>? deleteFile = null,
        Action? beforeTokenKeyPublish = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        
        _catalogCachePath = catalogCachePath ?? Path.Combine(appFolder, "catalog.json");
        
        
        // Local fallback path - try multiple locations
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localCatalogPath = localCatalogPath ?? FindLocalCatalogPath(baseDir);
        _tokenKeyRoot = tokenKeyRoot;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _getAttributes = getAttributes ?? File.GetAttributes;
        _deleteFile = deleteFile ?? File.Delete;
        _beforeTokenKeyPublish = beforeTokenKeyPublish ?? (() => { });
        
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
        await RefreshCatalogWithResultAsync(null, cancellationToken);
    }

    /// <summary>Refreshes from a validated one-call override without persisting source state.</summary>
    public async Task RefreshCatalogAsync(string? sourceUrl, CancellationToken cancellationToken = default)
    {
        await RefreshCatalogWithResultAsync(sourceUrl, cancellationToken);
    }

    /// <summary>Refreshes natively and returns a public-safe outcome without exposing source URLs.</summary>
    public async Task<CatalogRefreshResult> RefreshCatalogWithResultAsync(string? sourceUrl = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sources = string.IsNullOrWhiteSpace(sourceUrl) ? ResolveRefreshSources() : [new CatalogSource { Id = "override", Url = sourceUrl }];
        foreach (var source in sources)
        {
            if (!TryValidateSourceUri(source.Url, out var uri))
            {
                _logger.LogWarning("Skipping invalid catalog source {SourceId}", source.Id);
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 10 * 1024 * 1024)
                    throw new InvalidDataException("Catalog response exceeds the maximum size.");
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var packages = await JsonSerializer.DeserializeAsync<List<DistroPackage>>(new BoundedReadStream(stream, 10 * 1024 * 1024), cancellationToken: cancellationToken);
                if (!IsValidCatalog(packages)) throw new InvalidDataException("Catalog response is invalid.");
                await ReplaceCatalogAtomicallyAsync(packages!, cancellationToken);
                _cachedCatalog = ClonePackages(packages!);
                UpdatePackageCacheStatus(_cachedCatalog);
                return new CatalogRefreshResult(true, source.Id, "Updated", "Catalog.RefreshUpdated");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                _logger.LogWarning(ex, "Catalog source {SourceId} failed; trying next source", source.Id);
            }
        }

        return new CatalogRefreshResult(false, null, File.Exists(_catalogCachePath) || _cachedCatalog is not null ? "Preserved" : "Unavailable", "Catalog.RefreshFailed");
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
            _logger.LogInformation("Deleting cached package {PackageId}", packageId);

            // Find the package in catalog to get its DefaultName or LocalPath
            var package = _cachedCatalog?.FirstOrDefault(p => p.Id == packageId);
            if (package == null)
            {
                _logger.LogWarning("Package {PackageId} not found in catalog", packageId);
                return;
            }

            var root = Path.GetFullPath(ResolvePackageCachePath());
            var candidate = string.IsNullOrWhiteSpace(package.LocalPath) ? null : Path.GetFullPath(package.LocalPath);
            if (candidate is null || !IsChildPath(root, candidate) || !File.Exists(candidate)) return;
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(candidate);
            package.IsCached = false;
            package.LocalPath = string.Empty;
            package.FileSize = 0;
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

            foreach (var file in EnumerateContainedCacheFiles(result.CachePath, cancellationToken))
            {
                var fileInfo = new FileInfo(file);
                result.TotalSizeBytes += fileInfo.Length;
                result.PackageCount++;

                // Totals intentionally cover every eligible file; only display entries are bounded.
                if (result.CachedPackages.Count >= 1000)
                {
                    result.HasMoreEntries = true;
                    continue;
                }

                var cachedPackage = new CachedPackageInfo
                {
                    CacheEntryId = CreateCacheEntryId(result.CachePath, fileInfo),
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
        return (await ClearPackageCacheAsync(cancellationToken)).DeletedCount;
    }

    public PackageCacheLocationResult GetPackageCacheLocation() => new(GetPackageCachePath());

    /// <inheritdoc />
    public Task<PackageCacheDeleteResult> DeletePackageCacheEntryAsync(string cacheEntryId, CancellationToken cancellationToken = default)
    {
        return DeletePackageCacheAsync(new PackageCacheDeleteRequest(CacheEntryId: cacheEntryId), cancellationToken);
    }

    /// <inheritdoc />
    public Task<PackageCacheDeleteResult> DeletePackageCacheAsync(PackageCacheDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var root = GetPackageCachePath();
        var supplied = new[] { request.CacheEntryId, request.DefaultName, request.LocalPath }.Count(value => !string.IsNullOrWhiteSpace(value));
        if (supplied != 1) throw new InvalidOperationException("PackageCache.EntryInvalid");
        var cacheEntryId = request.CacheEntryId ?? ResolveCompatibilitySelector(root, request);
        var file = VerifyCacheEntryId(root, cacheEntryId);
        cancellationToken.ThrowIfCancellationRequested();
        _deleteFile(file.FullName);
        return Task.FromResult(new PackageCacheDeleteResult(true, "PackageCache.Deleted"));
    }

    /// <inheritdoc />
    public Task<PackageCacheClearResult> ClearPackageCacheAsync(CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        var failed = 0;
        var root = GetPackageCachePath();
        foreach (var file in EnumerateContainedCacheFiles(root, cancellationToken))
        {
            try { _deleteFile(file); deleted++; }
            catch (IOException) { failed++; }
            catch (UnauthorizedAccessException) { failed++; }
        }
        if (_cachedCatalog is not null)
            foreach (var package in _cachedCatalog) package.IsCached = false;
        return Task.FromResult(new PackageCacheClearResult(deleted, failed, failed == 0 ? "PackageCache.Cleared" : "PackageCache.Partial"));
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

    private IReadOnlyList<CatalogSource> ResolveRefreshSources()
    {
        var settings = _settingsService.LoadSettings();
        if (!settings.CustomData.TryGetValue("CatalogSources", out var serialized))
            return [new CatalogSource { Id = "legacy", Url = settings.CatalogUrl }];

        try
        {
            return (JsonSerializer.Deserialize<List<CatalogSource>>(serialized) ?? [])
                .Where(s => s.IsActive)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Id, StringComparer.Ordinal)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryValidateSourceUri(string? value, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 &&
               Uri.TryCreate(value, UriKind.Absolute, out uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsValidCatalog(List<DistroPackage>? packages) =>
        packages is { Count: > 0 and <= 10000 } && packages.All(p =>
            !string.IsNullOrWhiteSpace(p.Id) && p.Id.Length <= 256 &&
            !string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 256 &&
            (string.IsNullOrWhiteSpace(p.DownloadUrl) || Uri.TryCreate(p.DownloadUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)));

    private async Task ReplaceCatalogAtomicallyAsync(List<DistroPackage> packages, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_catalogCachePath) ?? throw new InvalidOperationException("Catalog cache path has no parent.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(_catalogCachePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, packages, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, _catalogCachePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string ResolvePackageCachePath()
    {
        var configured = _settingsService.LoadSettings().PackageCachePath;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus", "packages")
            : configured;
    }

    private IEnumerable<string> EnumerateContainedCacheFiles(string configuredRoot, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(configuredRoot);
        if (!Directory.Exists(root) || IsReparsePoint(root)) yield break;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsChildPath(root, Path.GetFullPath(file)) && !IsReparsePoint(file)) yield return file;
        }
    }

    private string CreateCacheEntryId(string configuredRoot, FileInfo file)
    {
        var root = Path.GetFullPath(configuredRoot);
        var relative = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
        var expiry = _timeProvider.GetUtcNow().AddMinutes(15).ToUnixTimeSeconds();
        var body = string.Join("|", Convert.ToBase64String(Encoding.UTF8.GetBytes(relative)), file.Length, file.LastWriteTimeUtc.Ticks, expiry, RootFingerprint(root));
        using var hmac = new HMACSHA256(GetCacheTokenKey());
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(body)) + "." + Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    private FileInfo VerifyCacheEntryId(string configuredRoot, string token)
    {
        try
        {
            var parts = token.Split('.', 2);
            if (parts.Length != 2) throw new InvalidDataException();
            var bodyBytes = Convert.FromBase64String(parts[0]);
            var signature = Convert.FromBase64String(parts[1]);
            using var hmac = new HMACSHA256(GetCacheTokenKey());
            if (!CryptographicOperations.FixedTimeEquals(signature, hmac.ComputeHash(bodyBytes))) throw new InvalidDataException();
            var fields = Encoding.UTF8.GetString(bodyBytes).Split('|');
            if (fields.Length != 5 || !long.TryParse(fields[1], out var length) || !long.TryParse(fields[2], out var writeTicks) || !long.TryParse(fields[3], out var expiry) || expiry < _timeProvider.GetUtcNow().ToUnixTimeSeconds()) throw new InvalidDataException();
            var root = Path.GetFullPath(configuredRoot);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(fields[4]), Encoding.UTF8.GetBytes(RootFingerprint(root)))) throw new InvalidDataException();
            var relative = Encoding.UTF8.GetString(Convert.FromBase64String(fields[0]));
            if (Path.IsPathRooted(relative) || relative.Contains("..", StringComparison.Ordinal)) throw new InvalidDataException();
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!IsChildPath(root, path) || IsReparsePoint(root) || IsReparsePoint(path)) throw new InvalidDataException();
            var file = new FileInfo(path);
            if (!file.Exists || file.Length != length || file.LastWriteTimeUtc.Ticks != writeTicks) throw new InvalidDataException();
            return file;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("PackageCache.EntryInvalid");
        }
    }

    private byte[] GetCacheTokenKey()
    {
        var directory = _tokenKeyRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "package-cache-token.key");
        if (File.Exists(path)) return ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        var key = RandomNumberGenerator.GetBytes(32);
        var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        var temporary = path + "." + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(temporary, protectedKey);
        // Test-only synchronization seam: production is a no-op. It deliberately sits after
        // absence observation and before atomic publish so collision recovery is testable.
        _beforeTokenKeyPublish();
        try
        {
            File.Move(temporary, path, false);
            return key;
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another bridge process won first creation; discard our protected candidate and
            // load the established current-user key so tokens remain process-independent.
            File.Delete(temporary);
            return ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        }
    }

    private bool IsReparsePoint(string path) => (_getAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static string RootFingerprint(string root) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())));

    private string ResolveCompatibilitySelector(string root, PackageCacheDeleteRequest request)
    {
        IEnumerable<string> candidates = EnumerateContainedCacheFiles(root, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(request.LocalPath))
        {
            var candidate = Path.GetFullPath(request.LocalPath);
            if (!IsChildPath(Path.GetFullPath(root), candidate)) throw new InvalidOperationException("PackageCache.EntryInvalid");
            candidates = candidates.Where(path => string.Equals(Path.GetFullPath(path), candidate, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var name = request.DefaultName!;
            candidates = candidates.Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase));
        }
        var selected = candidates.Take(2).ToList();
        if (selected.Count != 1) throw new InvalidOperationException("PackageCache.EntryInvalid");
        return CreateCacheEntryId(root, new FileInfo(selected[0]));
    }

    private static bool IsChildPath(string root, string candidate)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !new FileInfo(candidate).Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static List<DistroPackage> ClonePackages(IEnumerable<DistroPackage> packages) => packages.Select(ClonePackage).ToList();
    private static DistroPackage ClonePackage(DistroPackage package) => JsonSerializer.Deserialize<DistroPackage>(JsonSerializer.Serialize(package))!;

    private sealed class BoundedReadStream(Stream inner, long maximum) : Stream
    {
        private long _read;
        public override bool CanRead => inner.CanRead; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override Task FlushAsync(CancellationToken token) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) { var n = inner.Read(buffer, offset, count); Check(n); return n; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { var n = await inner.ReadAsync(buffer, token); Check(n); return n; }
        private void Check(int n) { _read += n; if (_read > maximum) throw new InvalidDataException("Catalog response exceeds the maximum size."); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
