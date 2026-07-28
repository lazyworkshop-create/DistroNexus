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
}

/// <summary>
/// Represents the stable module result for an instance tag query.
/// </summary>
public sealed record DistroNexusInstanceTagResult(string Name, IReadOnlyList<string> Tags);

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
