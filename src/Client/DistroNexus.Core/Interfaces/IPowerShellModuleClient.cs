using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides the closed set of DistroNexus module operations available to presentation clients.
/// </summary>
public interface IPowerShellModuleClient
{
    /// <summary>Gets installed WSL instances through the module contract.</summary>
    Task<IReadOnlyList<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts an instance through the module contract.</summary>
    Task<bool> StartInstanceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Stops an instance through the module contract.</summary>
    Task<bool> StopInstanceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tags for every instance, or for one instance when <paramref name="name"/> is supplied.
    /// </summary>
    Task<IReadOnlyList<DistroNexusInstanceTagResult>> GetInstanceTagsAsync(
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a tag to an instance through the module contract.</summary>
    Task AddInstanceTagAsync(string name, string tag, CancellationToken cancellationToken = default);

    /// <summary>Replaces the tags for an instance through the module contract.</summary>
    Task SetInstanceTagsAsync(string name, IReadOnlyList<string> tags, CancellationToken cancellationToken = default);

    /// <summary>Removes a tag from an instance through the module contract.</summary>
    Task RemoveInstanceTagAsync(string name, string tag, CancellationToken cancellationToken = default);

    /// <summary>Migrates tags to an instance's new name through the module contract.</summary>
    Task RenameInstanceTagsAsync(string oldName, string newName, CancellationToken cancellationToken = default);

    /// <summary>Gets the modeled global settings through the module contract.</summary>
    Task<GlobalSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies the supplied modeled settings fields through the module contract.</summary>
    Task SaveSettingsAsync(DistroNexusSettingsUpdate settings, CancellationToken cancellationToken = default);

    /// <summary>Resets modeled global settings through the module contract.</summary>
    Task ResetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets catalog sources through the module contract.</summary>
    Task<IReadOnlyList<CatalogSource>> GetCatalogSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a catalog source through the module contract.</summary>
    Task<CatalogSource> AddCatalogSourceAsync(
        DistroNexusCatalogSourceCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a catalog source through the module contract.</summary>
    Task<CatalogSource> UpdateCatalogSourceAsync(
        DistroNexusCatalogSourceUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a catalog source through the module contract.</summary>
    Task<bool> RemoveCatalogSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>Tests whether a catalog source URL is accessible through the module contract.</summary>
    Task<bool> TestCatalogSourceAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Sets a catalog source active state through the module contract.</summary>
    Task<bool> SetCatalogSourceActiveAsync(string sourceId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Reorders catalog sources through the module contract.</summary>
    Task<bool> ReorderCatalogSourcesAsync(IReadOnlyList<string> sourceIds, CancellationToken cancellationToken = default);

    /// <summary>Resets catalog sources to their defaults through the module contract.</summary>
    Task<bool> ResetCatalogSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists catalog packages through the module contract.</summary>
    Task<IReadOnlyList<DistroPackage>> GetPackagesAsync(string? family = null, bool forceReload = false, CancellationToken cancellationToken = default);

    /// <summary>Searches catalog packages through the module contract.</summary>
    Task<IReadOnlyList<DistroPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Gets one catalog package through the module contract.</summary>
    Task<DistroPackage?> GetPackageAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Refreshes the catalog through the module contract.</summary>
    Task<DistroNexusCatalogRefreshResult> RefreshCatalogAsync(string? sourceUrl = null, CancellationToken cancellationToken = default);

    Task<PackageCacheLocationResult> GetPackageCacheLocationAsync(CancellationToken cancellationToken = default);
    Task<CacheUsageInfo> GetPackageCacheUsageAsync(CancellationToken cancellationToken = default);
    Task<PackageCacheDeleteResult> DeletePackageCacheEntryAsync(string cacheEntryId, CancellationToken cancellationToken = default);
    Task<PackageCacheClearResult> ClearPackageCacheAsync(CancellationToken cancellationToken = default);

    Task<ContainerRuntimeSnapshot> GetContainerRuntimeStatusAsync(string name, CancellationToken cancellationToken = default);
    Task<InstanceCapabilitySnapshot> GetInstanceCapabilitiesAsync(string name, CancellationToken cancellationToken = default);
    Task<DistroNexusPodmanUserUnitPreview> GetPodmanUserUnitPreviewAsync(string name, PodmanUserUnit unit, SystemdAction action, CancellationToken cancellationToken = default);
    Task<DistroNexusPodmanUserUnitResult> InvokePodmanUserUnitAsync(DistroNexusPodmanUserUnitPreview preview, CancellationToken cancellationToken = default);
    Task<DistroNexusPodmanConnectionPreview> GetPodmanConnectionPreviewAsync(string name, PodmanConnectionRequest request, CancellationToken cancellationToken = default);
    Task<PodmanConnectionResult> InvokePodmanConnectionAsync(DistroNexusPodmanConnectionPreview preview, CancellationToken cancellationToken = default);
}

public sealed record DistroNexusPodmanUserUnitPreview(string Token, string InstanceName, PodmanUserUnit Unit, SystemdAction Action, IReadOnlyList<string> Effects);
public sealed record DistroNexusPodmanUserUnitResult(bool Succeeded, string OutcomeCode, string? Guidance = null);
public sealed record DistroNexusPodmanConnectionPreview(string Token, string InstanceName, string Name, string Endpoint, string Operation, string? ExistingEndpoint, IReadOnlyList<string> Effects);

/// <summary>
/// Represents the stable module result for an instance tag query.
/// </summary>
public sealed record DistroNexusInstanceTagResult(string Name, IReadOnlyList<string> Tags);

/// <summary>Represents the explicit catalog source fields accepted for creation.</summary>
public sealed record DistroNexusCatalogSourceCreateRequest(
    string Name,
    string Url,
    string? Description = null,
    bool IsActive = true);

/// <summary>Represents the explicit catalog source fields accepted for an update.</summary>
public sealed record DistroNexusCatalogSourceUpdateRequest(
    string SourceId,
    string Name,
    string Url,
    string? Description = null,
    bool IsActive = true);

/// <summary>Public-safe result returned by the catalog refresh command.</summary>
public sealed record DistroNexusCatalogRefreshResult(bool Succeeded, string? SourceId, string CacheState, string DiagnosticCode);

/// <summary>
/// Represents the explicit subset of modeled global settings to update. A null member is not sent
/// to the module, allowing presentation clients to make a typed partial update.
/// </summary>
public sealed record DistroNexusSettingsUpdate(
    string? DefaultInstallPath = null,
    string? PackageCachePath = null,
    string? TerminalStartPath = null,
    int? DefaultWslVersion = null,
    string? DefaultUsername = null,
    string? DefaultDistributionId = null,
    bool? EnableLogging = null,
    string? LogPath = null,
    bool? CheckUpdatesOnStartup = null,
    string? CatalogUrl = null,
    string? Theme = null,
    string? Language = null,
    bool? ShowConfirmationDialogs = null,
    int? MaxConcurrentDownloads = null,
    bool? AutoRetryDownloads = null,
    int? MaxRetryAttempts = null,
    bool? AutoSaveEnabled = null,
    int? AutoSaveInterval = null,
    string? PowerShellModulePath = null,
    bool UpdatePowerShellModulePath = false);
