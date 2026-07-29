using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for checking application updates from GitHub releases.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private readonly IStoreComplianceModeService _storeComplianceModeService;
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/LazyWorkshopCreate/DistroNexus/releases/latest";
    private const string GitHubReleasesPageUrl = "https://github.com/LazyWorkshopCreate/DistroNexus/releases";

    public UpdateService(HttpClient httpClient, ILogger<UpdateService> logger, IStoreComplianceModeService storeComplianceModeService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storeComplianceModeService = storeComplianceModeService ?? throw new ArgumentNullException(nameof(storeComplianceModeService));

        // Set User-Agent header required by GitHub API
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DistroNexus-UpdateChecker");
        }
    }

    /// <inheritdoc/>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default, bool includePrerelease = false)
    {
        try
        {
            if (_storeComplianceModeService.IsStoreComplianceModeEnabled())
            {
                _logger.LogInformation("Skipped update check because Store compliance mode is enabled");
                return null;
            }

            _logger.LogInformation("Checking for updates from GitHub");

            var response = await _httpClient.GetAsync(includePrerelease ? "https://api.github.com/repos/LazyWorkshopCreate/DistroNexus/releases" : GitHubReleasesApiUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check for updates: {StatusCode}", response.StatusCode);
                return null;
            }

            var release = includePrerelease
                ? (await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken))?
                    .Where(candidate => !candidate.Draft)
                    .Aggregate((best, candidate) => CompareVersions(
                        candidate.TagName?.TrimStart('v', 'V') ?? "0.0.0",
                        best.TagName?.TrimStart('v', 'V') ?? "0.0.0") > 0 ? candidate : best)
                : await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);

            if (release == null)
            {
                _logger.LogWarning("No release information found");
                return null;
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = release.TagName?.TrimStart('v', 'V') ?? "0.0.0";

            var isUpdateAvailable = CompareVersions(latestVersion, currentVersion) > 0;

            var updateInfo = new UpdateInfo
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                IsUpdateAvailable = isUpdateAvailable,
                ReleaseNotes = release.Body ?? string.Empty,
                ReleaseUrl = release.HtmlUrl ?? GitHubReleasesPageUrl,
                DownloadUrl = release.Assets?.FirstOrDefault()?.BrowserDownloadUrl ?? string.Empty,
                ReleaseDate = release.PublishedAt ?? DateTime.MinValue,
                IsPreRelease = release.Prerelease
            };

            if (isUpdateAvailable)
            {
                _logger.LogInformation("Update available: {CurrentVersion} -> {LatestVersion}", 
                    currentVersion, latestVersion);
            }
            else
            {
                _logger.LogInformation("Application is up to date (v{CurrentVersion})", currentVersion);
            }

            return updateInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error while checking for updates");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return null;
        }
    }

    /// <inheritdoc/>
    public virtual string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2.0.0";
    }

    /// <inheritdoc/>
    public void OpenDownloadPage(string releaseUrl)
    {
        try
        {
            if (_storeComplianceModeService.IsStoreComplianceModeEnabled())
            {
                _logger.LogInformation("Blocked opening download page because Store compliance mode is enabled");
                return;
            }

            var url = string.IsNullOrEmpty(releaseUrl) ? GitHubReleasesPageUrl : releaseUrl;
            
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            _logger.LogInformation("Opened download page: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open download page");
        }
    }

    /// <summary>
    /// Compares two semantic version strings.
    /// </summary>
    /// <returns>Positive if version1 > version2, negative if version1 < version2, zero if equal.</returns>
    private static int CompareVersions(string version1, string version2)
    {
        var v1Parts = ParseVersion(version1);
        var v2Parts = ParseVersion(version2);

        for (int i = 0; i < Math.Max(v1Parts.Length, v2Parts.Length); i++)
        {
            var v1Part = i < v1Parts.Length ? v1Parts[i] : 0;
            var v2Part = i < v2Parts.Length ? v2Parts[i] : 0;

            if (v1Part != v2Part)
            {
                return v1Part.CompareTo(v2Part);
            }
        }
        var firstPreRelease = version1.Contains('-', StringComparison.Ordinal);
        var secondPreRelease = version2.Contains('-', StringComparison.Ordinal);
        if (firstPreRelease != secondPreRelease) return firstPreRelease ? -1 : 1;
        if (!firstPreRelease) return 0;
        var left = version1[(version1.IndexOf('-') + 1)..].Split('.');
        var right = version2[(version2.IndexOf('-') + 1)..].Split('.');
        for (var index = 0; index < Math.Max(left.Length, right.Length); index++)
        {
            if (index == left.Length) return -1;
            if (index == right.Length) return 1;
            var leftNumeric = int.TryParse(left[index], out var leftNumber);
            var rightNumeric = int.TryParse(right[index], out var rightNumber);
            if (leftNumeric && rightNumeric && leftNumber != rightNumber) return leftNumber.CompareTo(rightNumber);
            if (leftNumeric != rightNumeric) return leftNumeric ? -1 : 1;
            var comparison = string.CompareOrdinal(left[index], right[index]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }

    private static int[] ParseVersion(string version)
    {
        // Remove any pre-release suffix (e.g., "-beta", "-rc1")
        var mainVersion = version.Split('-')[0];
        
        return mainVersion
            .Split('.')
            .Select(p => int.TryParse(p, out var num) ? num : 0)
            .ToArray();
    }

    /// <summary>
    /// GitHub release API response model.
    /// </summary>
    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
