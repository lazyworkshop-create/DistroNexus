using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for managing catalog sources.
/// </summary>
public class CatalogSourceManager : ICatalogSourceManager
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogSourceManager> _logger;
    private const string SourcesKey = "CatalogSources";

    public CatalogSourceManager(
        ISettingsService settingsService,
        HttpClient httpClient,
        ILogger<CatalogSourceManager> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<List<CatalogSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading catalog sources");

            var settings = await _settingsService.LoadSettingsAsync();
            var sourcesJson = settings.CustomData.GetValueOrDefault(SourcesKey, "[]");
            
            var sources = JsonSerializer.Deserialize<List<CatalogSource>>(sourcesJson) ?? GetDefaultSources();
            
            _logger.LogInformation("Loaded {Count} catalog sources", sources.Count);
            return sources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog sources, using defaults");
            return GetDefaultSources();
        }
    }

    /// <inheritdoc/>
    public async Task<CatalogSource> AddSourceAsync(CatalogSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Id))
        {
            source.Id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            throw new ArgumentException("Source name is required", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(source.Url))
        {
            throw new ArgumentException("Source URL is required", nameof(source));
        }

        try
        {
            _logger.LogInformation("Adding catalog source: {Name} ({Url})", source.Name, source.Url);

            var sources = await GetSourcesAsync(cancellationToken);
            
            // Check for duplicate URLs
            if (sources.Any(s => s.Url.Equals(source.Url, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A source with this URL already exists.");
            }

            source.CreatedDate = DateTime.UtcNow;
            source.Priority = sources.Count;
            sources.Add(source);

            await SaveSourcesAsync(sources, cancellationToken);

            _logger.LogInformation("Added catalog source: {Id}", source.Id);
            return source;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add catalog source: {Name}", source.Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CatalogSource> UpdateSourceAsync(CatalogSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            _logger.LogInformation("Updating catalog source: {Id}", source.Id);

            var sources = await GetSourcesAsync(cancellationToken);
            var existingSource = sources.FirstOrDefault(s => s.Id == source.Id);
            
            if (existingSource == null)
            {
                throw new ArgumentException($"Source with ID '{source.Id}' not found.", nameof(source));
            }

            // Update properties
            existingSource.Name = source.Name;
            existingSource.Url = source.Url;
            existingSource.Description = source.Description;
            existingSource.IsActive = source.IsActive;

            await SaveSourcesAsync(sources, cancellationToken);

            _logger.LogInformation("Updated catalog source: {Id}", source.Id);
            return existingSource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update catalog source: {Id}", source.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return false;

        try
        {
            _logger.LogInformation("Removing catalog source: {Id}", sourceId);

            var sources = await GetSourcesAsync(cancellationToken);
            var removed = sources.RemoveAll(s => s.Id == sourceId);

            if (removed > 0)
            {
                // Reorder priorities
                for (int i = 0; i < sources.Count; i++)
                {
                    sources[i].Priority = i;
                }

                await SaveSourcesAsync(sources, cancellationToken);
                _logger.LogInformation("Removed catalog source: {Id}", sourceId);
                return true;
            }

            _logger.LogWarning("Source not found for removal: {Id}", sourceId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove catalog source: {Id}", sourceId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestSourceAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return false;

        try
        {
            _logger.LogInformation("Testing catalog source: {Url}", sourceUrl);

            using var response = await _httpClient.GetAsync(sourceUrl, cancellationToken);
            
            var isSuccess = response.IsSuccessStatusCode;
            _logger.LogInformation("Source test result: {Url} -> {Success}", sourceUrl, isSuccess);
            
            return isSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Source test failed: {Url}", sourceUrl);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SetSourceActiveAsync(string sourceId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return false;

        try
        {
            _logger.LogInformation("Setting source active state: {Id} -> {Active}", sourceId, isActive);

            var sources = await GetSourcesAsync(cancellationToken);
            var source = sources.FirstOrDefault(s => s.Id == sourceId);
            
            if (source != null)
            {
                source.IsActive = isActive;
                await SaveSourcesAsync(sources, cancellationToken);
                _logger.LogInformation("Updated source active state: {Id}", sourceId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set source active state: {Id}", sourceId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ReorderSourcesAsync(List<string> sourceIds, CancellationToken cancellationToken = default)
    {
        if (sourceIds == null || sourceIds.Count == 0)
            return false;

        try
        {
            _logger.LogInformation("Reordering catalog sources");

            var sources = await GetSourcesAsync(cancellationToken);
            var reorderedSources = new List<CatalogSource>();

            foreach (var sourceId in sourceIds)
            {
                var source = sources.FirstOrDefault(s => s.Id == sourceId);
                if (source != null)
                {
                    reorderedSources.Add(source);
                }
            }

            // Add any remaining sources
            foreach (var source in sources)
            {
                if (!reorderedSources.Contains(source))
                {
                    reorderedSources.Add(source);
                }
            }

            // Update priorities
            for (int i = 0; i < reorderedSources.Count; i++)
            {
                reorderedSources[i].Priority = i;
            }

            await SaveSourcesAsync(reorderedSources, cancellationToken);
            _logger.LogInformation("Reordered catalog sources");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder catalog sources");
            throw;
        }
    }

    /// <inheritdoc/>
    public List<CatalogSource> GetDefaultSources()
    {
        return new List<CatalogSource>
        {
            new()
            {
                Id = "default-1",
                Name = "Official DistroNexus Repository",
                Url = "https://api.distronexus.com/v1/catalog.json",
                Description = "Official repository with the latest and verified distributions",
                IsActive = true,
                IsDefault = true,
                Priority = 0
            },
            new()
            {
                Id = "default-2", 
                Name = "Community DistroNexus Repository",
                Url = "https://community.distronexus.com/v1/catalog.json",
                Description = "Community-contributed distributions and variants",
                IsActive = true,
                IsDefault = true,
                Priority = 1
            }
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resetting catalog sources to defaults");

            var defaultSources = GetDefaultSources();
            await SaveSourcesAsync(defaultSources, cancellationToken);
            
            _logger.LogInformation("Reset catalog sources to defaults");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset catalog sources to defaults");
            throw;
        }
    }

    /// <summary>
    /// Saves the catalog sources to settings.
    /// </summary>
    private async Task SaveSourcesAsync(List<CatalogSource> sources, CancellationToken cancellationToken)
    {
        var sourcesJson = JsonSerializer.Serialize(sources, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        var settings = await _settingsService.LoadSettingsAsync();
        settings.CustomData[SourcesKey] = sourcesJson;
        await _settingsService.SaveSettingsAsync(settings);
    }
}