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
    Task<InstanceResourceSnapshot> GetInstanceResourcesAsync(string name, CancellationToken cancellationToken = default);
    Task<InstanceSparsePreview> GetInstanceSparsePreviewAsync(string name, bool enabled, CancellationToken cancellationToken = default);
    Task<InstanceSparseOperationResult> SetInstanceSparseModeAsync(string previewToken, CancellationToken cancellationToken = default);

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
    Task<TerminalStatusResult> GetTerminalStatusAsync(CancellationToken cancellationToken = default);
    Task<TerminalLaunchResult> StartTerminalAsync(string name, string? startPath = null, TerminalKind terminalKind = TerminalKind.Auto, CancellationToken cancellationToken = default);
    Task<TerminalLaunchResult> OpenPackageCacheFolderAsync(CancellationToken cancellationToken = default);

    Task<ContainerRuntimeSnapshot> GetContainerRuntimeStatusAsync(string name, CancellationToken cancellationToken = default);
    Task<PlatformCapabilitySnapshot> GetHostCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<InstanceCapabilitySnapshot> GetInstanceCapabilitiesAsync(string name, CancellationToken cancellationToken = default);
    Task<DistroNexusPodmanUserUnitPreview> GetPodmanUserUnitPreviewAsync(string name, PodmanUserUnit unit, SystemdAction action, CancellationToken cancellationToken = default);
    Task<DistroNexusPodmanUserUnitResult> InvokePodmanUserUnitAsync(DistroNexusPodmanUserUnitPreview preview, CancellationToken cancellationToken = default);
    Task<DistroNexusPodmanConnectionPreview> GetPodmanConnectionPreviewAsync(string name, PodmanConnectionRequest request, CancellationToken cancellationToken = default);
    Task<PodmanConnectionResult> InvokePodmanConnectionAsync(DistroNexusPodmanConnectionPreview preview, CancellationToken cancellationToken = default);
    Task<WslgApplicationStatus> GetWslgStatusAsync(string name, CancellationToken cancellationToken = default);
    Task<WslgDiscoveryResult> DiscoverWslgApplicationsAsync(string name, CancellationToken cancellationToken = default);
    Task<WslgActionResult> LaunchWslgApplicationAsync(string discoveryToken, string applicationId, CancellationToken cancellationToken = default);
    Task<WslgActionResult> RevealWslgApplicationAsync(string discoveryToken, string applicationId, CancellationToken cancellationToken = default);
    Task<WslgActionResult> SetWslgApplicationPinAsync(string discoveryToken, string applicationId, bool pinned, CancellationToken cancellationToken = default);
    Task<DockerIntegrationSnapshot> GetDockerIntegrationAsync(string name, CancellationToken cancellationToken = default);
    Task<DockerIntegrationPreview> GetDockerIntegrationPreviewAsync(string name, bool enabled, CancellationToken cancellationToken = default);
    Task<DockerIntegrationResult> SetDockerIntegrationAsync(string name, bool enabled, string previewToken, CancellationToken cancellationToken = default);
    Task<MonitoringSnapshotResult> GetMonitoringSnapshotAsync(string name, int intervalSeconds, CancellationToken cancellationToken = default);
    Task<MonitoringProcessActionPreview> GetMonitoringProcessActionPreviewAsync(string snapshotToken, int processId, MonitoringProcessAction action, CancellationToken cancellationToken = default);
    Task<ProcessActionResult> InvokeMonitoringProcessActionAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemdServiceInfo>> GetSystemdServicesAsync(string name, SystemdScope scope, CancellationToken cancellationToken = default);
    Task<SystemdServiceDetails?> GetSystemdServiceDetailsAsync(string name, SystemdUnitName unit, SystemdScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemdJournalEntry>> GetSystemdServiceJournalAsync(string name, SystemdUnitName unit, SystemdScope scope, string? search, int lineLimit, CancellationToken cancellationToken = default);
    Task<SystemdOperationPreview> GetSystemdServicePreviewAsync(string name, SystemdUnitName unit, SystemdAction action, SystemdScope scope, CancellationToken cancellationToken = default);
    Task<SystemdOperationResult> InvokeSystemdServiceAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<FirewallStatus> GetNetworkStatusAsync(CancellationToken cancellationToken = default);
    Task<string?> GetInstanceIpAddressAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortMapping>> GetPortMappingsAsync(string name, string? protocol = null, CancellationToken cancellationToken = default);
    Task<NetworkProbeResult> ProbeNetworkAsync(NetworkProbeRequest request, CancellationToken cancellationToken = default);
    Task<NetworkingModeGuidance> GetNetworkModeAsync(WslNetworkingMode mode, CancellationToken cancellationToken = default);
    Task<NetworkModePreview> GetNetworkModePreviewAsync(WslNetworkingMode mode, CancellationToken cancellationToken = default);
    Task<ConfigurationSaveResult> SetNetworkModeAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<NetworkSettings> GetNetworkSettingsAsync(CancellationToken cancellationToken = default);
    Task<NetworkSettingsPreview> GetNetworkSettingsPreviewAsync(NetworkSettings settings, CancellationToken cancellationToken = default);
    Task<ConfigurationSaveResult> SetNetworkSettingsAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<FixedExplorerResult> OpenNetworkLoopbackAsync(string host, int port, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirewallRuleInfo>> GetFirewallRulesAsync(CancellationToken cancellationToken = default);
    Task<FirewallOperationPreview> GetFirewallCreatePreviewAsync(FirewallRuleRequest request, CancellationToken cancellationToken = default);
    Task<FirewallOperationResult> CreateFirewallRuleAsync(string previewRuleId, CancellationToken cancellationToken = default);
    Task<FirewallRemovalPreview> GetFirewallRemovePreviewAsync(string ruleId, CancellationToken cancellationToken = default);
    Task<FirewallOperationResult> RemoveFirewallRuleAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<GlobalConfigurationSnapshot> GetGlobalConfigurationAsync(CancellationToken cancellationToken = default);
    Task<GlobalConfigurationPreview> GetGlobalConfigurationPreviewAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken = default);
    Task<GlobalConfigurationApplyResult> SetGlobalConfigurationAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<FixedExplorerResult> OpenWslConfigFileAsync(CancellationToken cancellationToken = default);
    Task<FixedExplorerResult> OpenRecoveryPointFolderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupSchedule>> GetBackupSchedulesAsync(CancellationToken cancellationToken = default);
    Task SaveBackupScheduleAsync(BackupSchedule schedule, CancellationToken cancellationToken = default);
    Task RemoveBackupScheduleAsync(string instanceName, CancellationToken cancellationToken = default);
    Task InvokeBackupAsync(string instanceName, string destination, int retentionCount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupNotification>> ConsumeBackupNotificationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryPointSummary>> GetRecoveryPointsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryHistoryEntry>> GetRecoveryHistoryAsync(CancellationToken cancellationToken = default);
    Task<RecoveryPointVerification> VerifyRecoveryPointAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecoveryOperationPreview> GetRecoveryCreatePreviewAsync(RecoveryPointCreateRequest request, CancellationToken cancellationToken = default);
    Task<RecoveryPointSummary> CreateRecoveryPointAsync(RecoveryOperationPreview preview, RecoveryPointCreateRequest request, CancellationToken cancellationToken = default);
    Task<RecoveryOperationPreview> GetRecoveryRemovePreviewAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveRecoveryPointAsync(RecoveryOperationPreview preview, Guid id, CancellationToken cancellationToken = default);
    Task<int?> GetRecoveryRetentionAsync(string instanceName, CancellationToken cancellationToken = default);
    Task<RecoveryRetentionPreview> GetRecoveryRetentionPreviewAsync(string instanceName, int maximum, CancellationToken cancellationToken = default);
    Task SetRecoveryRetentionAsync(RecoveryRetentionPreview preview, CancellationToken cancellationToken = default);
    Task<RecoveryOperationPreview> GetRecoveryRestorePreviewAsync(RecoveryRestoreRequest request, CancellationToken cancellationToken = default);
    Task RestoreRecoveryPointAsync(RecoveryOperationPreview preview, RecoveryRestoreRequest request, CancellationToken cancellationToken = default);
    Task<RecoveryOperationPreview> GetRecoveryClonePreviewAsync(RecoveryCloneRequest request, CancellationToken cancellationToken = default);
    Task CloneRecoveryPointAsync(RecoveryOperationPreview preview, RecoveryCloneRequest request, CancellationToken cancellationToken = default);
    Task<RecoveryOperationPreview> PreviewRecoveryPointNotesAsync(Guid id, string description, IReadOnlyList<string> tags, bool pinned, CancellationToken cancellationToken = default);
    Task ExecuteRecoveryPointNotesAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<HealthScanResult> ScanHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthHistoryEntry>> GetHealthHistoryAsync(CancellationToken cancellationToken = default);
    Task<RepairPreview> GetHealthRepairPreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default);
    Task<RepairResult> RepairHealthAsync(string previewToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDiagnosticLogOptionsAsync(CancellationToken cancellationToken = default);
    Task<DiagnosticReportPreview> GetDiagnosticReportPreviewAsync(DiagnosticReportFormat format, IReadOnlyList<string> selectedLogIds, CancellationToken cancellationToken = default);
    Task<DiagnosticReportExportResult> ExportDiagnosticReportAsync(string snapshotToken, string destinationFileName, int? deadlineMilliseconds = null, CancellationToken cancellationToken = default);
}

public sealed record FixedExplorerResult(bool Succeeded, string OutcomeCode);

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
