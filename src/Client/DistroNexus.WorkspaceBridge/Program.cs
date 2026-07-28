using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.WorkspaceBridge;
using Microsoft.Extensions.Logging.Abstractions;

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
var root = Environment.GetEnvironmentVariable("DISTRONEXUS_WORKSPACE_STORE_ROOT");
// Keep this composition deliberately equivalent to the desktop composition.  The
// bridge is a real execution boundary, not a persistence-only surrogate: Core owns
// validation, preview tokens, capability checks, and structured process requests.
var processes = new ProcessRunner();
var lifecycleRoot = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus");
var instances = new BridgeWslManagerService(processes, lifecycleRoot);
var lifecycleRoutes = new LifecyclePathOperationService(instances, lifecycleRoot, new LifecycleMetadataCleanup(lifecycleRoot, processes));
var instanceResources = new InstanceResourceService(new RegisteredInstanceSparseAdapter(processes), Path.Combine(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus"), "instance-sparse-grants"));
var dockerIntegration = new DockerIntegrationService(NullLogger<DockerIntegrationService>.Instance, instances);
var capabilities = new PlatformCapabilityService(processes);
var networkStatus = new WindowsNetworkStatusAdapter();
var portMappings = new BridgeNetworkPortMappingService(processes, networkStatus);
var distributionConfiguration = new DistributionConfigurationService(processes);
var networkDiagnostics = new NetworkDiagnosticsService(new WslNetworkDiagnosticsAdapter(processes));
var networkConfiguration = new NetworkConfigurationService(new WslConfigService(NullLogger<WslConfigService>.Instance, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), capabilities, networkDiagnostics);
var firewall = new GuardedFirewallOperationBroker();
var systemd = new SystemdService(processes, capabilities, distributionConfiguration);
var containers = ContainerRuntimeBridgeComposition.Create(processes, systemd);
var wslg = new WslgApplicationService(processes, capabilities, root);
var recovery = new RecoveryPointService(new WslRecoveryPointRuntime(processes, capabilities), root: root);
var explorerRoutes = new FixedExplorerRoutes(recovery, () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), info => Process.Start(info), RecoveryPathSafety.IsNoReparsePointInExistingPath);
var monitoring = new MonitoringService(processes);
var applicationRoot = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
var monitoringAutomation = new MonitoringAutomationService(monitoring, processes, applicationRoot);
var bridgePowerShell = new BridgeReadOnlyPowerShellService();
var settings = new SettingsService(NullLogger<SettingsService>.Instance, Path.Combine(applicationRoot, "settings.json"));
var catalogSources = new CatalogSourceManager(settings, new HttpClient(), NullLogger<CatalogSourceManager>.Instance);
var catalogHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
var catalog = new CatalogService(NullLogger<CatalogService>.Instance, settings, catalogHttp);
var globalConfiguration = new WslConfigService(NullLogger<WslConfigService>.Instance, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
var globalConfigurationGateway = new GlobalConfigurationService(globalConfiguration, globalConfiguration, capabilities, Path.Combine(applicationRoot, "global-configuration-grants"));
var backups = new BackupService(bridgePowerShell, NullLogger<BackupService>.Instance, applicationRoot);
var fixedBackups = new FixedBackupRuntime(instances, processes, applicationRoot);
if (args.Length > 0)
{
    if (args.Length != 2 || !string.Equals(args[0], "--run-backup-schedule", StringComparison.Ordinal) || args[1].Length != 32 || args[1].Any(c => !Uri.IsHexDigit(c)))
    {
        Console.Error.WriteLine("Invalid scheduled backup invocation.");
        Environment.ExitCode = 2;
        return;
    }

    var scheduled = await fixedBackups.RunScheduledAsync(args[1]);
    Environment.ExitCode = scheduled.Succeeded ? 0 : 1;
    return;
}
var templates = new TemplateService(NullLogger<TemplateService>.Instance, settings, bridgePowerShell, new HttpClient());
var monitoringWarnings = new MonitoringWarningRegistry();
var healthRuntime = new HealthRuntimeAdapter(processes, globalConfiguration);
var healthProbe = new DefaultHealthProbe(new BackupHealthSource(backups), templates, healthRuntime);
var windowsPrerequisites = new WindowsPrerequisiteProbe(processes);
var templateRuntimePreflight = new TemplateRuntimePreflightEvaluator(processes);
// Compose the same concrete Core health check families as the Desktop. Every operation here is
// read-only; no category is omitted or replaced with a synthetic availability result.
var health = new HealthOrchestrator([
    new InitialProbeHealthCheck(healthProbe), new CapabilityHealthCheck(), new WindowsPrerequisiteHealthCheck(windowsPrerequisites),
    new StorageHealthCheck(), new IntegrationHealthCheck(), new NetworkHealthCheck(), new SystemdHealthCheck(capabilities), new WslgHealthCheck(processes),
    new GlobalConfigurationHealthCheck(globalConfiguration), new DistributionConfigurationHealthCheck(distributionConfiguration),
    new BackupHealthCheck(backups), new TemplateHealthCheck(templates, templateRuntimePreflight), new MonitoringHealthCheck(monitoringWarnings)
], capabilities, instances, Path.Combine(applicationRoot, "health-history.json"));
var healthRepairs = new HealthRepairService([
    // Navigation and UAC brokering are intentionally Desktop-only.  Retain their canonical IDs
    // so PowerShell receives a reviewed, actionable result rather than an unregistered repair.
    new DesktopOnlyRepairAction("open.wsl-update", "Open WSL update settings", RepairSafety.Safe),
    new DesktopOnlyRepairAction("open.windows-virtualization-settings", "Open Windows virtualization settings", RepairSafety.Safe),
    new DesktopOnlyRepairAction("enable.windows-features", "Enable required Windows features", RepairSafety.PrivilegedOrDisruptive),
    new GlobalConfigurationRepairAction(globalConfiguration),
    new InstanceConfigurationRepairAction(distributionConfiguration),
    new FixedProcessRepairAction("wsl.update", "Update WSL", RepairSafety.RequiresConfirmation, RepairIdempotency.Idempotent,
        ["Download and install the latest available WSL update."], _ => new ProcessRequest("wsl.exe", ["--update"], TimeSpan.FromMinutes(5)), processes,
        _ => new ProcessRequest("wsl.exe", ["--version"], TimeSpan.FromSeconds(30))),
    new WslRestartRepairAction(instances, processes),
    new FixedProcessRepairAction("wsl.trim", "Trim Linux filesystem", RepairSafety.PrivilegedOrDisruptive, RepairIdempotency.Idempotent,
        ["Run fstrim in the selected running distribution. Linux privilege policy may reject this operation."],
        finding => string.IsNullOrWhiteSpace(finding.InstanceName) ? null : new ProcessRequest("wsl.exe", ["--distribution", finding.InstanceName, "--", "sudo", "--non-interactive", "fstrim", "-av"], TimeSpan.FromMinutes(2)), processes,
        finding => string.IsNullOrWhiteSpace(finding.InstanceName) ? null : new ProcessRequest("wsl.exe", ["--distribution", finding.InstanceName, "--", "sh", "-lc", "df -Pk /"], TimeSpan.FromSeconds(30)))
], durableGrantRoot: applicationRoot, health: health);
var diagnosticLogs = new ApplicationDiagnosticLogProvider(settings);
var diagnosticReports = new DiagnosticReportService(health, capabilities, diagnosticLogs, new StructuredFileErrorProvider(diagnosticLogs), Path.Combine(applicationRoot, "diagnostics"));
var runtime = new WorkspaceRuntime(instances, processes);
var gate = new WorkspaceActionCapabilityGate(capabilities);
var handlers = Enum.GetValues<WorkspaceActionType>()
    .Select(type => (IWorkspaceActionHandler)new WorkspaceActionHandler(type, runtime, gate))
    .ToArray();
var service = new WorkspaceService(runtime, root, handlers: handlers);
var marketplace = new TemplateMarketplaceService(root);
var outputGate = new object();
void WriteFrame(BridgeResponse frame)
{
    lock (outputGate) Console.WriteLine(JsonSerializer.Serialize(frame, options));
}
string? line;
while ((line = Console.ReadLine()) is not null)
{
    BridgeResponse response;
    BridgeRequest? request = null;
    try
    {
        request = JsonSerializer.Deserialize<BridgeRequest>(line, options) ?? throw new ArgumentException("Bridge request is invalid.");
        var payload = request.Payload?.GetRawText() ?? string.Empty;
        object? value = request.Operation! switch
        {
            "list" => await service.ListAsync(),
            "save" => await service.SaveAsync(ParseDefinition(payload, options), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewSave" => await service.PreviewSaveAsync(ParseDefinition(payload, options), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "duplicate" => await DuplicateAsync(service, request),
            "previewDuplicate" => await service.PreviewDuplicateAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.Name ?? throw new ArgumentException("Workspace name is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "export" => await service.ExportAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewExportDryRun" => await service.PreviewExportDryRunAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewImport" => await service.PreviewImportAsync(payload),
            "import" => await service.ImportAsync(payload, request.Token ?? throw new ArgumentException("Import token is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewImportDryRun" => await service.PreviewImportDryRunAsync(payload, request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "approveTrust" => await service.ApproveTrustAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewApproveTrust" => await service.PreviewApproveTrustAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewLaunch" => await service.PreviewLaunchAsync(request.Id ?? throw new ArgumentException("Workspace id is required.")),
            "previewLaunchDryRun" => await service.PreviewLaunchDryRunAsync(request.Id ?? throw new ArgumentException("Workspace id is required.")),
            "launch" => await LaunchAsync(request),
            "previewRetry" => await service.PreviewRetryAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ActionId ?? throw new ArgumentException("Action id is required.")),
            "retry" => await RetryAsync(request),
            "remove" => await RemoveAsync(service, request),
            "previewRemove" => await service.PreviewRemoveAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewRetryDryRun" => await service.PreviewRetryDryRunAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ActionId ?? throw new ArgumentException("Action id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")),
            "previewPodmanUnit" => await PreviewPodmanUnitAsync(request),
            "executePodmanUnit" => await ExecutePodmanUnitAsync(request),
            "previewPodmanConnection" => await PreviewPodmanConnectionAsync(request),
            "executePodmanConnection" => await ExecutePodmanConnectionAsync(request),
            "containerRuntimeStatus" => await ContainerRuntimeStatusAsync(request),
            "docker.integration.get.v1" => await GetDockerIntegrationAsync(request),
            "docker.integration.preview-set.v1" => await PreviewDockerIntegrationAsync(request),
            "docker.integration.set.v1" => await SetDockerIntegrationAsync(request),
            "capability" => await GetCapabilitiesAsync(request),
            "capability.host.v1" => await GetHostCapabilitiesV1Async(request),
            "capability.instance.v1" => await GetInstanceCapabilitiesV1Async(request),
            "systemdList" => await ListSystemdV1Async(request),
            "systemdPreview" => await PreviewSystemdV1Async(request),
            "systemdExecute" => await ExecuteSystemdV1Async(request),
            "systemd.list.v1" => await ListSystemdV1Async(request),
            "systemd.preview.v1" => await PreviewSystemdV1Async(request),
            "systemd.execute.v1" => await ExecuteSystemdV1Async(request),
            "systemd.details.v1" => await GetSystemdDetailsV1Async(request),
            "systemd.journal.v1" => await GetSystemdJournalV1Async(request),
            "wslg.status.v1" => await GetWslgStatusAsync(request),
            "wslg.discover.v1" => await DiscoverWslgAsync(request),
            "wslg.launch.v1" => await LaunchWslgAsync(request),
            "wslg.reveal.v1" => await RevealWslgAsync(request),
            "wslg.pin.v1" => await PinWslgAsync(request),
            "recoveryList" => await RecoveryListV1Async(request),
            "recoveryHistory" => await RecoveryHistoryV1Async(request),
            "recoveryVerify" => await RecoveryVerifyV1Async(request),
            "recoveryPreviewCreate" => await PreviewRecoveryCreateV1Async(request),
            "recoveryCreate" => await CreateRecoveryV1Async(request),
            "recoveryPreviewRestore" => await PreviewRecoveryRestoreV1Async(request),
            "recoveryRestore" => await RestoreRecoveryV1Async(request),
            "recoveryPreviewRemove" => await RecoveryPreviewRemoveV1Async(request),
            "recoveryRemove" => await RemoveRecoveryV1Async(request),
            "recovery.list.v1" => await RecoveryListV1Async(request),
            "recovery.history.v1" => await RecoveryHistoryV1Async(request),
            "recovery.verify.v1" => await RecoveryVerifyV1Async(request),
            "explorer.wslconfig.v1" => explorerRoutes.OpenWslConfig(request),
            "explorer.recovery-point.v1" => await explorerRoutes.OpenRecoveryPointAsync(request),
            "recovery.preview-create.v1" => await PreviewRecoveryCreateV1Async(request),
            "recovery.create.v1" => await CreateRecoveryV1Async(request),
            "recovery.preview-restore.v1" => await PreviewRecoveryRestoreV1Async(request),
            "recovery.restore.v1" => await RestoreRecoveryV1Async(request),
            "recovery.preview-remove.v1" => await RecoveryPreviewRemoveV1Async(request),
            "recovery.remove.v1" => await RemoveRecoveryV1Async(request),
            "recovery.preview-clone.v1" => await PreviewRecoveryCloneV1Async(request),
            "recovery.clone.v1" => await CloneRecoveryV1Async(request),
            "recovery.notes.v1" => throw new ArgumentException("The legacy recovery notes operation is not supported."),
            "recovery.notes.preview.v1" => await PreviewRecoveryNotesV1Async(request),
            "recovery.notes.execute.v1" => await ExecuteRecoveryNotesV1Async(request),
            "recovery.retention.get.v1" => await GetRecoveryRetentionV1Async(request),
            "recovery.retention.preview.v1" => await PreviewRecoveryRetentionV1Async(request),
            "recovery.retention.set.v1" => await SetRecoveryRetentionV1Async(request),
            "backup.schedule.list.v1" => await fixedBackups.GetSchedulesAsync(),
            "backup.schedule.preview.v1" => await PreviewBackupScheduleV1Async(request),
            "backup.schedule.remove.preview.v1" => await PreviewBackupScheduleRemovalV1Async(request),
            "backup.manual.preview.v1" => await PreviewManualBackupV1Async(request),
            "backup.execute.v1" => await ExecuteBackupV1Async(request),
            "backup.notifications.consume.v1" => await fixedBackups.ConsumeNotificationsAsync(),
            "monitoring.snapshot.v1" => await GetMonitoringSnapshotAsync(request),
            "monitoring.process.preview.v1" => await PreviewMonitoringProcessActionAsync(request),
            "monitoring.process.execute.v1" => await ExecuteMonitoringProcessActionAsync(request),
            "healthScan" => await HealthScanV1Async(request),
            "healthRepairPreview" => await PreviewHealthRepairV1Async(request),
            "health.scan.v1" => await HealthScanV1Async(request),
            "health.history.v1" => await HealthHistoryV1Async(request),
            "diagnostics.log-options.v1" => DiagnosticLogOptionsV1(request),
            "health.repair-preview.v1" => await PreviewHealthRepairV1Async(request),
            "health.repair.v1" => await ExecuteHealthRepairV1Async(request),
            "diagnostics.preview.v1" => await PreviewDiagnosticsV1Async(request),
            "diagnostics.export.v1" => await ExportDiagnosticsV1Async(request),
            "marketplaceListSources" => await marketplace.GetSourcesAsync(),
            "marketplaceStatus" => await GetMarketplaceStatusAsync(request),
            "marketplaceAddSource" => await AddMarketplaceSourceAsync(request),
            "marketplaceSetSourceEnabled" => await SetMarketplaceSourceEnabledAsync(request),
            "marketplaceRemoveSource" => await RemoveMarketplaceSourceAsync(request),
            "marketplaceCreateReviewGrant" => await CreateMarketplaceReviewGrantAsync(request),
            "marketplaceApproveCandidate" => await ApproveMarketplaceCandidateAsync(request),
            "marketplaceReviewUpdate" => await ReviewMarketplaceUpdateAsync(request),
            "marketplaceScriptDiff" => await ReviewMarketplaceScriptDiffAsync(request),
            "marketplaceArtifactHistory" => await GetMarketplaceArtifactHistoryAsync(request),
            "marketplaceRollback" => await RollbackMarketplaceArtifactAsync(request),
            "marketplaceDownloadArtifact" => await DownloadMarketplaceArtifactAsync(request),
            "instance.list.v1" => await instances.GetInstanceDetailsAsync(ParseInstanceListOptions(request)),
            "instance.start.v1" => await StartInstanceV1Async(request),
            "instance.stop.v1" => await StopInstanceV1Async(request),
            "instance.remove.preview.v1" => await LifecycleRemovePreviewV1Async(request),
            "instance.remove.execute.v1" => await LifecycleExecuteV1Async(request),
            "instance.move.preview.v1" => await LifecycleMovePreviewV1Async(request),
            "instance.move.execute.v1" => await LifecycleExecuteV1Async(request),
            "instance.rename.preview.v1" => await LifecycleRenamePreviewV1Async(request),
            "instance.rename.execute.v1" => await LifecycleExecuteV1Async(request),
            "instance.export.preview.v1" => await LifecycleExportPreviewV1Async(request),
            "instance.export.execute.v1" => await LifecycleExecuteV1Async(request),
            "instance.import.preview.v1" => await LifecycleImportPreviewV1Async(request),
            "instance.import.execute.v1" => await LifecycleExecuteV1Async(request),
            "instance.lifecycle.execute.v1" => await LifecycleExecuteV1Async(request),
            "instance.resources.get.v1" => await InstanceResourcesGetV1Async(request),
            "instance.sparse.preview.v1" => await InstanceSparsePreviewV1Async(request),
            "instance.sparse.execute.v1" => await InstanceSparseExecuteV1Async(request),
            "configuration.global.get.v1" => await GetGlobalConfigurationV1Async(request),
            "configuration.global.preview.v1" => await PreviewGlobalConfigurationV1Async(request),
            "configuration.global.execute.v1" => await ExecuteGlobalConfigurationV1Async(request),
            "settings.get.v1" => GetSettings(request),
            "settings.save.v1" => SaveSettings(request),
            "settings.reset.v1" => ResetSettings(request),
            "catalog-source.list.v1" => await GetCatalogSourcesAsync(request),
            "catalog-source.add.v1" => await AddCatalogSourceAsync(request),
            "catalog-source.update.v1" => await UpdateCatalogSourceAsync(request),
            "catalog-source.remove.v1" => await RemoveCatalogSourceAsync(request),
            "catalog-source.test.v1" => await TestCatalogSourceAsync(request),
            "catalog-source.active.set.v1" => await SetCatalogSourceActiveAsync(request),
            "catalog-source.reorder.v1" => await ReorderCatalogSourcesAsync(request),
            "catalog-source.defaults.get.v1" => GetDefaultCatalogSources(request),
            "catalog-source.defaults.reset.v1" => await ResetCatalogSourcesAsync(request),
            "catalog.list.v1" => await ListCatalogAsync(request),
            "catalog.search.v1" => await SearchCatalogAsync(request),
            "catalog.get.v1" => await GetCatalogAsync(request),
            "catalog.refresh.v1" => await RefreshCatalogAsync(request),
            "package-cache.location.v1" => GetPackageCacheLocation(request),
            "package-cache.usage.v1" => await GetPackageCacheUsageAsync(request),
            "package-cache.delete.v1" => await DeletePackageCacheAsync(request),
            "package-cache.clear.v1" => await ClearPackageCacheAsync(request),
            "terminal.status.v1" => GetTerminalStatus(request),
            "terminal.launch.v1" => await LaunchTerminalAsync(request),
            "explorer.package-cache.v1" => OpenPackageCacheFolder(request),
            "network.status.v1" => NetworkStatusV1(request),
            "network.ip.v1" => await NetworkIpV1Async(request),
            "network.port-mappings.v1" => await NetworkPortMappingsV1Async(request),
            "network.probe.v1" => await NetworkProbeV1Async(request),
            "network.mode.get.v1" => await NetworkModeV1Async(request),
            "network.mode.preview.v1" => await NetworkModePreviewV1Async(request),
            "network.mode.set.v1" => await NetworkModeSetV1Async(request),
            "network.settings.get.v1" => await NetworkSettingsGetV1Async(request),
            "network.settings.preview.v1" => await NetworkSettingsPreviewV1Async(request),
            "network.settings.set.v1" => await NetworkSettingsSetV1Async(request),
            "browser.loopback.v1" => OpenLoopbackV1(request),
            "firewall.list.v1" => await FirewallListV1Async(request),
            "firewall.preview-create.v1" => await FirewallPreviewCreateV1Async(request),
            "firewall.create.v1" => await FirewallCreateV1Async(request),
            "firewall.preview-remove.v1" => await FirewallPreviewRemoveV1Async(request),
            "firewall.remove.v1" => await FirewallRemoveV1Async(request),
            _ => throw new ArgumentException("Bridge operation is unsupported.")
        };
        response = new(true, value, null, null);
    }
    catch (DistroNexus.Core.Exceptions.WslOperationFailedException) when (request?.Operation.StartsWith("monitoring.", StringComparison.Ordinal) == true) { response = new(false, null, "Monitor.Failed", "Monitor.Failed"); }
    catch (DistroNexus.Core.Exceptions.WslOperationFailedException ex) { response = new(false, null, ex.Code.ToString(), ex.Message); }
    catch (InvalidOperationException ex) when (ex.Message is "Wslg.DiscoveryGrantInvalid" or "Wslg.DiscoveryGrantExpired" or "Wslg.ApplicationNotFound" or "Wslg.EntryChanged") { response = new(false, null, ex.Message, ex.Message); }
    catch (InvalidOperationException ex) when (string.Equals(ex.Message, "PackageCache.EntryInvalid", StringComparison.Ordinal)) { response = new(false, null, "PackageCache.EntryInvalid", "Package cache entry is invalid."); }
    catch (Exception ex) when (request?.Operation.StartsWith("instance.", StringComparison.Ordinal) == true && (request.Operation.Contains(".preview.", StringComparison.Ordinal) || request.Operation.Contains(".execute.", StringComparison.Ordinal)))
    {
        var code = ex is OperationCanceledException ? "Lifecycle.Cancelled" : ex.Message is "Lifecycle.PathInvalid" or "Lifecycle.GrantInvalid" or "Lifecycle.GrantExpired" or "Lifecycle.InstanceStateChanged" or "Lifecycle.KeepFilesUnavailable" ? ex.Message : "Lifecycle.Failed";
        response = new(false, null, code, code);
    }
    catch (Exception ex) when (request?.Operation.StartsWith("instance.", StringComparison.Ordinal) == true && request.Operation.Contains(".preview.", StringComparison.Ordinal) || request?.Operation.StartsWith("instance.", StringComparison.Ordinal) == true && request.Operation.Contains(".execute.", StringComparison.Ordinal) == true)
    {
        var code = ex is OperationCanceledException ? "Lifecycle.Cancelled" : ex.Message is "Lifecycle.PathInvalid" or "Lifecycle.GrantInvalid" or "Lifecycle.GrantExpired" or "Lifecycle.InstanceStateChanged" or "Lifecycle.KeepFilesUnavailable" ? ex.Message : "Lifecycle.Failed";
        response = new(false, null, code, code);
    }
    catch (Exception ex) when (request?.Operation.StartsWith("docker.integration.", StringComparison.Ordinal) == true)
    {
        var code = ex.Message is "DockerIntegration.PreviewInvalid" or "DockerIntegration.PreviewExpired" or "DockerIntegration.PreviewMismatch" or "DockerIntegration.PreviewStale" ? ex.Message : "DockerIntegration.Conflict";
        response = new(false, null, code, code);
    }
    catch (Exception ex) when (request?.Operation.StartsWith("monitoring.", StringComparison.Ordinal) == true)
    {
        var code = ex switch
        {
            ArgumentException => "Monitor.InvalidRequest",
            OperationCanceledException => "Monitor.Cancelled",
            InvalidOperationException when ex.Message is "Monitor.SnapshotGrantInvalid" or "Monitor.GrantInvalid" => "Monitor.SnapshotInvalid",
            InvalidOperationException when ex.Message is "Monitor.PreviewInvalid" => "Monitor.PreviewInvalid",
            InvalidOperationException when ex.Message is "Monitor.PreviewReplayed" => "Monitor.PreviewReplayed",
            InvalidOperationException when ex.Message is "Monitor.GrantExpired" => "Monitor.GrantExpired",
            InvalidOperationException when ex.Message is "Monitor.KillRequiresTermAndReprobe" => "Monitor.KillRequiresTermAndReprobe",
            InvalidOperationException when ex.Message is "Monitor.ProcessIdentityChanged" => "Monitor.ProcessIdentityChanged",
            InvalidOperationException when ex.Message is "Monitor.ProcessNotFound" => "Monitor.ProcessNotFound",
            InvalidOperationException when ex.Message is "Monitor.InstanceStopped" => "Monitor.InstanceStopped",
            InvalidOperationException when ex.Message is "Monitor.ProcessActionInvalid" => "Monitor.InvalidRequest",
            _ => "Monitor.Failed"
        };
        response = new(false, null, code, code);
    }
    catch (Exception ex) when (request?.Operation.StartsWith("diagnostics.", StringComparison.Ordinal) == true) { response = new(false, null, "Diagnostic.ExportInvalid", SensitiveDataRedactor.Redact(ex.Message)); }
    catch (Exception ex) { response = new(false, null, ex is InvalidOperationException ? "Workspace.ConflictOrState" : "Workspace.Bridge.Invalid", ex.Message); }
    WriteFrame(response);
}

void ValidateEmptyPayload(BridgeRequest request)
{
    if (request.Payload is not null) throw new ArgumentException("This bridge operation does not accept a payload.");
}
void ValidatePayload(BridgeRequest request, IEnumerable<string> allowed, IEnumerable<string> required)
{
    if (request.Payload is not { ValueKind: JsonValueKind.Object } payload) throw new ArgumentException("A typed bridge payload is required.");
    var names = payload.EnumerateObject().Select(property => property.Name).ToArray();
    if (names.Any(name => !allowed.Contains(name, StringComparer.OrdinalIgnoreCase)) || required.Any(name => !names.Contains(name, StringComparer.OrdinalIgnoreCase)))
        throw new ArgumentException("Bridge payload does not match the operation contract.");
}

string ParseInstanceName(BridgeRequest request, bool allowKeepAlive = false) => ParseInstancePayload(request, allowKeepAlive).Name;

InstanceNamePayload ParseInstancePayload(BridgeRequest request, bool allowKeepAlive = false)
{
    ValidatePayload(request, allowKeepAlive ? ["Name", "KeepAlive"] : ["Name"], ["Name"]);
    var payload = JsonSerializer.Deserialize<InstanceNamePayload>(request.Payload?.GetRawText() ?? string.Empty,
        new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
        ?? throw new ArgumentException("Instance payload is required.");
    if (string.IsNullOrWhiteSpace(payload.Name))
        throw new ArgumentException("Instance name is required.");
    return payload;
}

async Task<object> StartInstanceV1Async(BridgeRequest request)
{
    var payload = ParseInstancePayload(request, allowKeepAlive: true);
    var name = payload.Name;
    var registered = await instances.GetInstanceDetailsAsync(new InstanceListOptions(false, false, true, false));
    if (!registered.Any(instance => string.Equals(instance.Name, name, StringComparison.OrdinalIgnoreCase)))
        throw new ArgumentException("The instance is not registered.");
    var started = payload.KeepAlive
        ? await instances.StartInstanceWithKeepAliveAsync(name)
        : await instances.StartInstanceAsync(name);
    return new { Succeeded = started, Started = started, KeepAliveEstablished = started && payload.KeepAlive };
}

async Task<object> StopInstanceV1Async(BridgeRequest request)
{
    var stopped = await instances.StopInstanceAsync(ParseInstanceName(request));
    return new { Succeeded = stopped };
}

async Task<LifecycleOperationPreview> LifecycleRemovePreviewV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "KeepFiles"], ["Name", "KeepFiles"]);
    var payload = ParsePayload<LifecyclePayload>(request);
    return await lifecycleRoutes.PreviewRemoveAsync(payload.Name, payload.KeepFiles);
}
async Task<LifecycleOperationPreview> LifecycleMovePreviewV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "Destination"], ["Name", "Destination"]);
    var payload = ParsePayload<LifecyclePayload>(request);
    return await lifecycleRoutes.PreviewMoveAsync(payload.Name, payload.Destination!);
}
async Task<LifecycleOperationPreview> LifecycleRenamePreviewV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "NewName"], ["Name", "NewName"]);
    var payload = ParsePayload<LifecyclePayload>(request);
    return await lifecycleRoutes.PreviewRenameAsync(payload.Name, payload.NewName!);
}
async Task<LifecycleOperationPreview> LifecycleExportPreviewV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "Destination", "StopRunning"], ["Name", "Destination", "StopRunning"]);
    var payload = ParsePayload<LifecyclePayload>(request);
    return await lifecycleRoutes.PreviewExportAsync(payload.Name, payload.Destination!, payload.StopRunning);
}
async Task<LifecycleOperationPreview> LifecycleImportPreviewV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "Source", "InstallPath"], ["Name", "Source", "InstallPath"]);
    var payload = ParsePayload<LifecyclePayload>(request);
    return await lifecycleRoutes.PreviewImportAsync(payload.Name, payload.Source!, payload.InstallPath!);
}
async Task<LifecycleOperationResult> LifecycleExecuteV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]);
    return await lifecycleRoutes.ExecuteAsync(ParsePayload<LifecycleExecutePayload>(request).PreviewToken);
}

GlobalSettings GetSettings(BridgeRequest request)
{
    RequireNoPayload(request, "Settings get does not accept a payload.");
    return settings.LoadSettings();
}

GlobalSettings SaveSettings(BridgeRequest request)
{
    var payload = JsonSerializer.Deserialize<SettingsSavePayload>(request.Payload?.GetRawText() ?? string.Empty,
        new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
        ?? throw new ArgumentException("Settings save payload is required.");
    ArgumentNullException.ThrowIfNull(payload.Settings);
    settings.SaveSettings(payload.Settings);
    return settings.LoadSettings();
}

GlobalSettings ResetSettings(BridgeRequest request)
{
    RequireNoPayload(request, "Settings reset does not accept a payload.");
    settings.ResetSettings();
    return settings.LoadSettings();
}

async Task<List<CatalogSource>> GetCatalogSourcesAsync(BridgeRequest request)
{
    RequireNoPayload(request, "Catalog source list does not accept a payload.");
    return await catalogSources.GetSourcesAsync();
}

async Task<List<DistroPackage>> ListCatalogAsync(BridgeRequest request)
{
    var payload = DeserializeOptionalCatalogPayload<CatalogListPayload>(request);
    if (payload is not null && payload.Family is not null)
        ValidateCatalogText(payload.Family, "family");
    var packages = await catalog.LoadCatalogAsync(payload?.ForceReload ?? false);
    return string.IsNullOrWhiteSpace(payload?.Family) ? packages : packages.Where(p => string.Equals(p.Category, payload.Family, StringComparison.OrdinalIgnoreCase)).ToList();
}

async Task<List<DistroPackage>> SearchCatalogAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<CatalogSearchPayload>(request, "Catalog search payload is required.");
    ValidateCatalogText(payload.Query, "query");
    return await catalog.SearchDistributionsAsync(payload.Query);
}

async Task<DistroPackage?> GetCatalogAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<CatalogGetPayload>(request, "Catalog get payload is required.");
    ValidateCatalogText(payload.Id, "id");
    return await catalog.GetDistributionByIdAsync(payload.Id);
}

async Task<object> RefreshCatalogAsync(BridgeRequest request)
{
    var payload = DeserializeOptionalCatalogPayload<CatalogRefreshPayload>(request);
    if (payload?.SourceUrl is { } sourceUrl && !IsValidRefreshUrl(sourceUrl))
        throw new ArgumentException("Catalog source URL is invalid.");
    return await catalog.RefreshCatalogWithResultAsync(payload?.SourceUrl);
}

PackageCacheLocationResult GetPackageCacheLocation(BridgeRequest request)
{
    RequireNoPayload(request, "Package cache location does not accept a payload.");
    return catalog.GetPackageCacheLocation();
}

async Task<CacheUsageInfo> GetPackageCacheUsageAsync(BridgeRequest request)
{
    RequireNoPayload(request, "Package cache usage does not accept a payload.");
    return await catalog.GetCacheUsageAsync();
}

FirewallStatus NetworkStatusV1(BridgeRequest request) { ValidateEmptyPayload(request); return networkStatus.GetFirewallStatusAsync().GetAwaiter().GetResult(); }
async Task<string?> NetworkIpV1Async(BridgeRequest request) { ValidatePayload(request, ["Name"], ["Name"]); return await portMappings.GetInstanceIpAddressAsync(ParseNetworkPayload(request).Name); }
async Task<IReadOnlyList<PortMapping>> NetworkPortMappingsV1Async(BridgeRequest request) { ValidatePayload(request, ["Name", "Protocol"], ["Name"]); var p = ParseNetworkPayload(request); return await portMappings.GetPortMappingsAsync(p.Name, p.Protocol); }
async Task<NetworkProbeResult> NetworkProbeV1Async(BridgeRequest request) { ValidatePayload(request, ["Request"], ["Request"]); return await networkDiagnostics.ProbeAsync(ParsePayload<NetworkProbePayload>(request).Request); }
async Task<NetworkingModeGuidance> NetworkModeV1Async(BridgeRequest request) { ValidatePayload(request, ["Mode"], ["Mode"]); return await networkConfiguration.GetGuidanceAsync(ParsePayload<NetworkModePayload>(request).Mode); }
async Task<NetworkModePreview> NetworkModePreviewV1Async(BridgeRequest request) { ValidatePayload(request, ["Mode"], ["Mode"]); return await networkConfiguration.PreviewModeAsync(ParsePayload<NetworkModePayload>(request).Mode); }
async Task<ConfigurationSaveResult> NetworkModeSetV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await networkConfiguration.ApplyModeAsync(request.Token ?? throw new ArgumentException("Network mode preview token is required.")); }
async Task<NetworkSettings> NetworkSettingsGetV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await networkConfiguration.ReadSettingsAsync(); }
async Task<NetworkSettingsPreview> NetworkSettingsPreviewV1Async(BridgeRequest request) { ValidatePayload(request, ["Settings"], ["Settings"]); return await networkConfiguration.PreviewSettingsAsync(ParsePayload<NetworkSettingsPayload>(request).Settings); }
async Task<ConfigurationSaveResult> NetworkSettingsSetV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await networkConfiguration.ApplySettingsAsync(request.Token ?? throw new ArgumentException("Network settings preview token is required.")); }
FixedExplorerResult OpenLoopbackV1(BridgeRequest request)
{
    ValidatePayload(request, ["Host", "Port"], ["Host", "Port"]);
    var payload = ParsePayload<LoopbackBrowserPayload>(request);
    return DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Open(payload.Host, payload.Port);
}
async Task<IReadOnlyList<FirewallRuleInfo>> FirewallListV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await firewall.ListOwnedAsync(); }
async Task<FirewallOperationPreview> FirewallPreviewCreateV1Async(BridgeRequest request) { ValidatePayload(request, ["Request"], ["Request"]); return await firewall.PreviewCreateAsync(ParsePayload<FirewallRequestPayload>(request).Request); }
async Task<FirewallOperationResult> FirewallCreateV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewRuleId"], ["PreviewRuleId"]); return await firewall.CreateAsync(ParsePayload<FirewallCreatePayload>(request).PreviewRuleId); }
async Task<FirewallRemovalPreview> FirewallPreviewRemoveV1Async(BridgeRequest request) { ValidatePayload(request, ["RuleId"], ["RuleId"]); return await firewall.PreviewRemoveAsync(ParsePayload<FirewallRemovePreviewPayload>(request).RuleId); }
async Task<FirewallOperationResult> FirewallRemoveV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); return await firewall.RemoveAsync(ParsePayload<FirewallRemovePayload>(request).PreviewToken); }
T ParsePayload<T>(BridgeRequest request) => JsonSerializer.Deserialize<T>(request.Payload!.Value.GetRawText(), options) ?? throw new ArgumentException("Bridge payload is invalid.");
NetworkPortMappingPayload ParseNetworkPayload(BridgeRequest request) { var p = ParsePayload<NetworkPortMappingPayload>(request); if (string.IsNullOrWhiteSpace(p.Name)) throw new ArgumentException("Instance name is required."); return p; }

async Task<PackageCacheDeleteResult> DeletePackageCacheAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<PackageCacheDeletePayload>(request, "Package cache delete payload is required.");
    var supplied = new[] { payload.CacheEntryId, payload.DefaultName, payload.LocalPath }.Count(value => !string.IsNullOrWhiteSpace(value));
    if (supplied != 1 || (payload.CacheEntryId?.Length ?? 0) > 4096 || (payload.DefaultName?.Length ?? 0) > 256 || (payload.LocalPath?.Length ?? 0) > 4096)
        throw new ArgumentException("Package cache entry identifier is invalid.");
    return await catalog.DeletePackageCacheAsync(new PackageCacheDeleteRequest(payload.CacheEntryId, payload.DefaultName, payload.LocalPath));
}

async Task<PackageCacheClearResult> ClearPackageCacheAsync(BridgeRequest request)
{
    RequireNoPayload(request, "Package cache clear does not accept a payload.");
    return await catalog.ClearPackageCacheAsync();
}

TerminalStatusResult GetTerminalStatus(BridgeRequest request)
{
    RequireNoPayload(request, "Terminal status does not accept a payload.");
    var commandPrompt = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"));
    var windowsTerminal = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION")) || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe"));
    return new(windowsTerminal, commandPrompt, windowsTerminal ? TerminalKind.WindowsTerminal : TerminalKind.CommandPrompt);
}

async Task<TerminalLaunchResult> LaunchTerminalAsync(BridgeRequest request)
{
    ValidatePayload(request, ["InstanceName", "StartPath", "TerminalKind"], ["InstanceName"]);
    var payload = ParsePayload<TerminalLaunchPayload>(request);
    if (string.IsNullOrWhiteSpace(payload.InstanceName) || payload.InstanceName.Length > 256 || payload.InstanceName.IndexOfAny(['\r', '\n', '\0']) >= 0 || (payload.StartPath is not null && !IsValidLinuxStartPath(payload.StartPath)) || !Enum.IsDefined(payload.TerminalKind)) throw new ArgumentException("Terminal launch payload is invalid.");
    var known = await instances.GetInstancesAsync();
    if (!known.Any(instance => string.Equals(instance.Name, payload.InstanceName, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Terminal instance is unknown.");
    var status = GetTerminalStatus(new BridgeRequest("terminal.status.v1", null, null, null));
    var selected = payload.TerminalKind == TerminalKind.Auto ? status.DefaultKind : payload.TerminalKind;
    if (selected == TerminalKind.WindowsTerminal && !status.WindowsTerminalAvailable || selected == TerminalKind.CommandPrompt && !status.CommandPromptAvailable) return new(false, selected, "Terminal.Unavailable");
    Process.Start(FixedLaunchProcess.CreateTerminalStartInfo(selected, payload.InstanceName, payload.StartPath));
    return new(true, selected, "Terminal.Launched");
}

TerminalLaunchResult OpenPackageCacheFolder(BridgeRequest request)
{
    RequireNoPayload(request, "Package cache explorer does not accept a payload.");
    var configured = settings.LoadSettings().PackageCachePath;
    if (string.IsNullOrWhiteSpace(configured)) return new(false, TerminalKind.Auto, "PackageCache.NotConfigured");
    var root = Path.GetFullPath(configured);
    if (!Directory.Exists(root)) return new(false, TerminalKind.Auto, "PackageCache.NotFound");
    Process.Start(FixedLaunchProcess.CreatePackageCacheStartInfo(root));
    return new(true, TerminalKind.Auto, "PackageCache.Opened");
}

static bool IsValidLinuxStartPath(string value) => value.Length is > 0 and <= 1024 && value.IndexOfAny(['\r', '\n', '\0', '\\']) < 0 && (value == "~" || (value.StartsWith('/') && !value.Contains("//", StringComparison.Ordinal) && !value.Split('/').Any(segment => segment == "..")));

T? DeserializeOptionalCatalogPayload<T>(BridgeRequest request) where T : class
{
    if (request.Payload is null || request.Payload.Value.ValueKind == JsonValueKind.Null) return null;
    return JsonSerializer.Deserialize<T>(request.Payload.Value.GetRawText(), new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
}

static void ValidateCatalogText(string? value, string name)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
        throw new ArgumentException($"Catalog {name} must be between 1 and 256 characters.");
}

static bool IsValidRefreshUrl(string value) => value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && !string.IsNullOrWhiteSpace(uri.Host) &&
    string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);

async Task<CatalogSource> AddCatalogSourceAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<CatalogSourceAddPayload>(request, "Catalog source add payload is required.");
    ValidateCatalogSourceDetails(payload.Name, payload.Url);
    return await catalogSources.AddSourceAsync(new CatalogSource
    {
        Name = payload.Name,
        Url = payload.Url,
        Description = payload.Description ?? string.Empty,
        IsActive = payload.IsActive
    });
}

async Task<CatalogSource> UpdateCatalogSourceAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<CatalogSourceUpdatePayload>(request, "Catalog source update payload is required.");
    if (string.IsNullOrWhiteSpace(payload.SourceId))
        throw new ArgumentException("Catalog source id is required.");
    ValidateCatalogSourceDetails(payload.Name, payload.Url);
    return await catalogSources.UpdateSourceAsync(new CatalogSource
    {
        Id = payload.SourceId,
        Name = payload.Name,
        Url = payload.Url,
        Description = payload.Description ?? string.Empty,
        IsActive = payload.IsActive
    });
}

async Task<bool> RemoveCatalogSourceAsync(BridgeRequest request) =>
    await catalogSources.RemoveSourceAsync(DeserializeCatalogSourcePayload<CatalogSourceIdPayload>(request, "Catalog source remove payload is required.").SourceId);

async Task<bool> TestCatalogSourceAsync(BridgeRequest request) =>
    await TestCatalogSourceUrlAsync(request);

async Task<bool> TestCatalogSourceUrlAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<CatalogSourceTestPayload>(request, "Catalog source test payload is required.");
    ValidateCatalogSourceUrl(payload.Url);
    return await catalogSources.TestSourceAsync(payload.Url);
}

async Task<bool> SetCatalogSourceActiveAsync(BridgeRequest request)
{
    var payload = DeserializeCatalogSourcePayload<CatalogSourceActivePayload>(request, "Catalog source active-state payload is required.");
    return await catalogSources.SetSourceActiveAsync(payload.SourceId, payload.IsActive);
}

async Task<bool> ReorderCatalogSourcesAsync(BridgeRequest request) =>
    await catalogSources.ReorderSourcesAsync(DeserializeCatalogSourcePayload<CatalogSourceReorderPayload>(request, "Catalog source reorder payload is required.").SourceIds);

List<CatalogSource> GetDefaultCatalogSources(BridgeRequest request)
{
    RequireNoPayload(request, "Catalog source defaults do not accept a payload.");
    return catalogSources.GetDefaultSources();
}

async Task<bool> ResetCatalogSourcesAsync(BridgeRequest request)
{
    RequireNoPayload(request, "Catalog source reset does not accept a payload.");
    return await catalogSources.ResetToDefaultsAsync();
}

T DeserializeCatalogSourcePayload<T>(BridgeRequest request, string missingPayloadMessage)
{
    return JsonSerializer.Deserialize<T>(request.Payload?.GetRawText() ?? string.Empty,
        new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
        ?? throw new ArgumentException(missingPayloadMessage);
}

static void ValidateCatalogSourceDetails(string? name, string? url)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Catalog source name is required.");
    ValidateCatalogSourceUrl(url);
}

static void ValidateCatalogSourceUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url))
        throw new ArgumentException("Catalog source URL is required.");
}

static void RequireNoPayload(BridgeRequest request, string message)
{
    if (request.Payload is not null)
        throw new ArgumentException(message);
}

InstanceListOptions ParseInstanceListOptions(BridgeRequest request)
{
    ValidatePayload(request, ["IncludeRelease", "IncludeUser", "SkipDiskSize", "ForceRefresh"], []);
    var payload = JsonSerializer.Deserialize<InstanceListPayload>(request.Payload!.Value.GetRawText(), new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
        ?? throw new ArgumentException("Instance list payload is required.");
    return new InstanceListOptions(payload.IncludeRelease, payload.IncludeUser, payload.SkipDiskSize, payload.ForceRefresh);
}

async Task<object> PreviewPodmanUnitAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanUnitPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman payload is required."); var preview = await containers.PreviewPodmanUserUnitAsync(p.InstanceName, p.Unit, p.Action); return new { Token = preview.SystemdPreview.PreviewToken, InstanceName = p.InstanceName, Unit = p.Unit, Action = p.Action, Effects = preview.SystemdPreview.Effects }; }
async Task<object> ExecutePodmanUnitAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanUnitPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman payload is required."); return await containers.ExecutePodmanUserUnitAsync(request.Token ?? string.Empty, p.InstanceName, p.Unit, p.Action); }
async Task<object> PreviewPodmanConnectionAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanConnectionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman connection payload is required."); var preview = await containers.PreviewPodmanConnectionAsync(p.InstanceName, new PodmanConnectionRequest(p.Name, new Uri(p.Endpoint, UriKind.Absolute))); return new { preview.Token, preview.InstanceName, Name = preview.Request.Name, Endpoint = preview.Request.SafeEndpoint, preview.Operation, preview.ExistingEndpoint, preview.Effects }; }
async Task<object> ExecutePodmanConnectionAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanConnectionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman connection payload is required."); return await containers.ConfigurePodmanConnectionAsync(request.Token ?? string.Empty, p.InstanceName, new PodmanConnectionRequest(p.Name, new Uri(p.Endpoint, UriKind.Absolute))); }
async Task<ContainerRuntimeStatusResponse> ContainerRuntimeStatusAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanStatusPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Container runtime payload is required."); return await ContainerRuntimeBridgeHandler.GetStatusAsync(containers, p.InstanceName); }
async Task<object> GetCapabilitiesAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<CapabilityPayload>(request.Payload?.GetRawText() ?? "{}", options) ?? new CapabilityPayload(null, false);
    return string.IsNullOrWhiteSpace(p.InstanceName) || !p.InstanceOnly
        ? await capabilities.GetHostSnapshotAsync()
        : await capabilities.GetInstanceSnapshotAsync(p.InstanceName);
}
async Task<PlatformCapabilitySnapshot> GetHostCapabilitiesV1Async(BridgeRequest request)
{
    ValidateEmptyPayload(request);
    return await capabilities.GetHostSnapshotAsync();
}
async Task<InstanceCapabilitySnapshot> GetInstanceCapabilitiesV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["InstanceName"], ["InstanceName"]);
    var payload = ParsePayload<CapabilityInstancePayload>(request);
    if (string.IsNullOrWhiteSpace(payload.InstanceName) || payload.InstanceName.IndexOfAny(['\r', '\n', '\0']) >= 0)
        throw new ArgumentException("Instance name is invalid.");
    return await capabilities.GetInstanceSnapshotAsync(payload.InstanceName);
}
async Task<IReadOnlyList<SystemdServiceInfo>> ListSystemdAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd payload is required.");
    return await systemd.ListAsync(p.InstanceName, p.Scope);
}
async Task<IReadOnlyList<SystemdServiceInfo>> ListSystemdV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName", "Scope"], ["InstanceName"]); return await ListSystemdAsync(request); }
async Task<SystemdOperationPreview> PreviewSystemdV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName", "Unit", "Action", "Scope"], ["InstanceName", "Unit", "Action"]); return await PreviewSystemdAsync(request); }
async Task<SystemdOperationResult> ExecuteSystemdV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); return await ExecuteSystemdAsync(request); }
async Task<SystemdServiceDetails?> GetSystemdDetailsV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName", "Unit", "Scope"], ["InstanceName", "Unit"]); return await GetSystemdDetailsAsync(request); }
async Task<IReadOnlyList<SystemdJournalEntry>> GetSystemdJournalV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName", "Unit", "Scope", "Search", "LineLimit"], ["InstanceName", "Unit"]); return await GetSystemdJournalAsync(request); }
async Task<SystemdOperationPreview> PreviewSystemdAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd payload is required.");
    if (p.Action is null || string.IsNullOrWhiteSpace(p.Unit)) throw new ArgumentException("A systemd unit and action are required.");
    return await systemd.PreviewAsync(p.InstanceName, new SystemdUnitName(p.Unit), p.Action.Value, p.Scope);
}
async Task<SystemdOperationResult> ExecuteSystemdAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdExecutePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd preview token is required.");
    return await systemd.ExecuteAsync(p.PreviewToken);
}
async Task<SystemdServiceDetails?> GetSystemdDetailsAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd payload is required.");
    if (string.IsNullOrWhiteSpace(p.Unit)) throw new ArgumentException("A systemd unit is required.");
    return await systemd.GetDetailsAsync(p.InstanceName, new SystemdUnitName(p.Unit), p.Scope);
}
async Task<IReadOnlyList<SystemdJournalEntry>> GetSystemdJournalAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdJournalPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd journal payload is required.");
    return await systemd.GetJournalAsync(p.InstanceName, new SystemdUnitName(p.Unit), p.Scope, p.Search, p.LineLimit);
}
async Task<WslgApplicationStatus> GetWslgStatusAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<WslgInstancePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg payload is required.");
    return await wslg.GetStatusAsync(p.InstanceName);
}
async Task<WslgDiscoveryResult> DiscoverWslgAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<WslgInstancePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg payload is required.");
    return await wslg.DiscoverWithGrantAsync(p.InstanceName);
}
async Task<WslgActionResult> LaunchWslgAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<WslgActionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg action payload is required.");
    return await wslg.LaunchGrantedAsync(p.DiscoveryToken, p.ApplicationId);
}
async Task<WslgActionResult> RevealWslgAsync(BridgeRequest request)
{ var p = JsonSerializer.Deserialize<WslgActionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg action payload is required."); return await wslg.RevealGrantedAsync(p.DiscoveryToken, p.ApplicationId); }
async Task<WslgActionResult> PinWslgAsync(BridgeRequest request)
{ var p = JsonSerializer.Deserialize<WslgPinPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg pin payload is required."); return await wslg.SetGrantedPinnedAsync(p.DiscoveryToken, p.ApplicationId, p.Pinned); }
async Task<RecoveryOperationPreview> PreviewRecoveryCreateAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryCreatePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery create payload is required.");
    return await recovery.PreviewCreateAsync(p.Request);
}
async Task<RecoveryPointSummary> CreateRecoveryAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryCreatePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery create payload is required.");
    return await recovery.CreateAsync(p.Request, request.Token ?? throw new ArgumentException("Recovery preview token is required."));
}
async Task<RecoveryOperationPreview> PreviewRecoveryRestoreAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryRestorePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery restore payload is required.");
    return await recovery.PreviewRestoreAsync(p.Request);
}
async Task<object> RestoreRecoveryAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryRestorePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery restore payload is required.");
    await recovery.RestoreAsync(p.Request, request.Token ?? throw new ArgumentException("Recovery preview token is required.")); return new { };
}
async Task<object> RemoveRecoveryAsync(BridgeRequest request) { await recovery.DeleteAsync(request.Id ?? throw new ArgumentException("Recovery id is required."), request.Token ?? throw new ArgumentException("Recovery preview token is required.")); return new { }; }
async Task<object> CloneRecoveryAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryClonePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery clone payload is required.");
    if (string.IsNullOrWhiteSpace(request.Token)) return await recovery.PreviewCloneAsync(p.Request);
    await recovery.RestoreCloneAsync(p.Request, request.Token); return new { };
}
async Task<object> GetRecoveryRetentionAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryRetentionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery retention payload is required.");
    return new { SourceInstance = p.SourceInstance, Maximum = await recovery.GetRetentionAsync(p.SourceInstance) };
}
async Task<object> SetRecoveryRetentionAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<RecoveryRetentionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Recovery retention payload is required.");
    if (p.Maximum is null) throw new ArgumentException("Recovery retention maximum is required.");
    await recovery.ApplyRetentionAsync(p.SourceInstance, p.Maximum.Value, request.Token ?? throw new ArgumentException("Recovery retention preview token is required.")); return new { SourceInstance = p.SourceInstance, Maximum = p.Maximum.Value };
}
async Task<IReadOnlyList<RecoveryPointSummary>> RecoveryListV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await recovery.ListAsync(); }
async Task<IReadOnlyList<RecoveryHistoryEntry>> RecoveryHistoryV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await recovery.GetHistoryAsync(); }
async Task<RecoveryPointVerification> RecoveryVerifyV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await recovery.VerifyAsync(request.Id ?? throw new ArgumentException("Recovery id is required.")); }
async Task<RecoveryOperationPreview> PreviewRecoveryCreateV1Async(BridgeRequest request) { ValidatePayload(request, ["Request"], ["Request"]); return await PreviewRecoveryCreateAsync(request); }
async Task<RecoveryPointSummary> CreateRecoveryV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); var p = ParsePayload<RecoveryExecutePayload>(request); return (await recovery.ExecutePreviewAsync(p.PreviewToken)) as RecoveryPointSummary ?? throw new InvalidOperationException("Recovery create did not produce a recovery point."); }
async Task<RecoveryOperationPreview> PreviewRecoveryRestoreV1Async(BridgeRequest request) { ValidatePayload(request, ["Request"], ["Request"]); return await PreviewRecoveryRestoreAsync(request); }
async Task<object> RestoreRecoveryV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); await recovery.ExecutePreviewAsync(ParsePayload<RecoveryExecutePayload>(request).PreviewToken); return new { }; }
async Task<RecoveryOperationPreview> RecoveryPreviewRemoveV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await recovery.PreviewDeleteAsync(request.Id ?? throw new ArgumentException("Recovery id is required.")); }
async Task<object> RemoveRecoveryV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); await recovery.ExecutePreviewAsync(ParsePayload<RecoveryExecutePayload>(request).PreviewToken); return new { }; }
async Task<RecoveryOperationPreview> PreviewRecoveryCloneV1Async(BridgeRequest request) { ValidatePayload(request, ["Request"], ["Request"]); return await recovery.PreviewCloneAsync(ParsePayload<RecoveryClonePayload>(request).Request); }
async Task<object> CloneRecoveryV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); await recovery.ExecutePreviewAsync(ParsePayload<RecoveryExecutePayload>(request).PreviewToken); return new { }; }
async Task<RecoveryOperationPreview> PreviewRecoveryNotesV1Async(BridgeRequest request) { ValidatePayload(request, ["Id", "Description", "Tags", "Pinned"], ["Id", "Description", "Tags", "Pinned"]); var p = ParsePayload<RecoveryNotesPayload>(request); return await recovery.PreviewUpdateNotesAsync(p.Id, p.Description, p.Tags, p.Pinned); }
async Task<object> ExecuteRecoveryNotesV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); await recovery.ExecutePreviewAsync(ParsePayload<RecoveryExecutePayload>(request).PreviewToken); return new { }; }
async Task<object> GetRecoveryRetentionV1Async(BridgeRequest request) { ValidatePayload(request, ["SourceInstance"], ["SourceInstance"]); return await GetRecoveryRetentionAsync(request); }
async Task<RecoveryRetentionPreview> PreviewRecoveryRetentionV1Async(BridgeRequest request) { ValidatePayload(request, ["SourceInstance", "Maximum"], ["SourceInstance", "Maximum"]); var p = JsonSerializer.Deserialize<RecoveryRetentionPayload>(request.Payload!.Value.GetRawText(), options)!; if (p.Maximum is null) throw new ArgumentException("Recovery retention maximum is required."); return await recovery.PreviewRetentionAsync(p.SourceInstance, p.Maximum.Value); }
async Task<object> SetRecoveryRetentionV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); await recovery.ExecuteRetentionPreviewAsync(ParsePayload<RecoveryExecutePayload>(request).PreviewToken); return new { }; }
async Task<BackupOperationPreview> PreviewBackupScheduleV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName", "Frequency", "RetentionCount", "Time", "Destination"], ["InstanceName", "Frequency", "RetentionCount", "Time"]); return await fixedBackups.PreviewScheduleAsync(ParsePayload<BackupScheduleRequest>(request)); }
async Task<BackupOperationPreview> PreviewBackupScheduleRemovalV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName"], ["InstanceName"]); return await fixedBackups.PreviewScheduleRemovalAsync(ParsePayload<BackupManualPayload>(request).InstanceName); }
async Task<BackupOperationPreview> PreviewManualBackupV1Async(BridgeRequest request) { ValidatePayload(request, ["InstanceName", "RetentionCount", "Destination"], ["InstanceName", "RetentionCount"]); var p = ParsePayload<BackupManualPayload>(request); ValidateLegacyBackupDestination(p.Destination); return await fixedBackups.PreviewBackupAsync(p.InstanceName, p.RetentionCount); }
void ValidateLegacyBackupDestination(string? destination) { if (destination is not null && (string.IsNullOrWhiteSpace(destination) || destination.IndexOfAny(['\r', '\n', '\0']) >= 0 || !Path.IsPathFullyQualified(destination))) throw new ArgumentException("The legacy backup destination is invalid."); }
async Task<BackupOperationResult> ExecuteBackupV1Async(BridgeRequest request) { ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]); return await fixedBackups.ExecuteAsync(ParsePayload<RecoveryExecutePayload>(request).PreviewToken); }
async Task<HealthScanResult> HealthScanV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await health.ScanAsync(); }
async Task<IReadOnlyList<HealthHistoryEntry>> HealthHistoryV1Async(BridgeRequest request) { ValidateEmptyPayload(request); return await health.GetHistoryAsync(); }
async Task<RepairPreview> PreviewHealthRepairV1Async(BridgeRequest request) { ValidatePayload(request, ["Finding"], ["Finding"]); return await PreviewHealthRepairAsync(request); }
async Task<RepairResult> ExecuteHealthRepairV1Async(BridgeRequest request) { ValidatePayload(request, [], []); if (string.IsNullOrWhiteSpace(request.Token)) throw new ArgumentException("Health repair preview token is required."); return await healthRepairs.ExecuteAsync(request.Token); }
IReadOnlyList<string> DiagnosticLogOptionsV1(BridgeRequest request) { ValidateEmptyPayload(request); return diagnosticLogs.AllowedLogIds.Order(StringComparer.Ordinal).ToArray(); }
async Task<DiagnosticReportPreview> PreviewDiagnosticsV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Format", "SelectedLogIds", "DeadlineMilliseconds"], ["Format"]);
    var payload = ParsePayload<DiagnosticPreviewPayload>(request);
    if (!Enum.IsDefined(payload.Format)) throw new ArgumentException("Diagnostic report format is invalid.");
    if (payload.SelectedLogIds is { Count: > 32 } || payload.SelectedLogIds?.Any(string.IsNullOrWhiteSpace) == true)
        throw new ArgumentException("Diagnostic log selection is invalid.");
    using var cancellation = CreateDiagnosticCancellation(payload.DeadlineMilliseconds);
    return await diagnosticReports.PreviewAsync(new DiagnosticReportRequest(payload.Format, true, payload.SelectedLogIds), cancellation.Token);
}
async Task<DiagnosticReportExportResult> ExportDiagnosticsV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["DestinationFileName", "DeadlineMilliseconds"], ["DestinationFileName"]);
    if (string.IsNullOrWhiteSpace(request.Token)) throw new ArgumentException("Diagnostic preview token is required.");
    var payload = ParsePayload<DiagnosticExportPayload>(request);
    using var cancellation = CreateDiagnosticCancellation(payload.DeadlineMilliseconds);
    return await diagnosticReports.ExportAsync(new DiagnosticReportExportRequest(request.Token, payload.DestinationFileName), cancellation.Token);
}
static CancellationTokenSource CreateDiagnosticCancellation(int? deadlineMilliseconds)
{
    var cancellation = new CancellationTokenSource();
    if (deadlineMilliseconds is null) return cancellation;
    if (deadlineMilliseconds is < 1 or > 30_000) throw new ArgumentException("Diagnostic request deadline must be between 1 and 30000 milliseconds.");
    cancellation.CancelAfter(TimeSpan.FromMilliseconds(deadlineMilliseconds.Value));
    return cancellation;
}
async Task<DockerIntegrationSnapshot> GetDockerIntegrationAsync(BridgeRequest request)
{
    ValidatePayload(request, ["Name"], ["Name"]);
    var payload = ParsePayload<DockerIntegrationPayload>(request);
    return await dockerIntegration.GetSnapshotAsync(payload.Name);
}
async Task<DockerIntegrationPreview> PreviewDockerIntegrationAsync(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "Enabled"], ["Name", "Enabled"]);
    var payload = ParsePayload<DockerIntegrationPayload>(request);
    return await dockerIntegration.PreviewSetAsync(payload.Name, payload.Enabled);
}
async Task<DockerIntegrationResult> SetDockerIntegrationAsync(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "Enabled"], ["Name", "Enabled"]);
    if (string.IsNullOrWhiteSpace(request.Token)) throw new ArgumentException("Docker integration preview token is required.");
    var payload = ParsePayload<DockerIntegrationPayload>(request);
    return await dockerIntegration.SetFromPreviewAsync(request.Token, payload.Name, payload.Enabled);
}
async Task<InstanceResourceSnapshot> InstanceResourcesGetV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name"], ["Name"]);
    return await instanceResources.GetAsync(ParsePayload<InstanceResourcePayload>(request).Name);
}
async Task<InstanceSparsePreview> InstanceSparsePreviewV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "Enabled"], ["Name", "Enabled"]);
    var payload = ParsePayload<InstanceSparsePayload>(request);
    return await instanceResources.PreviewSparseAsync(payload.Name, payload.Enabled);
}
async Task<InstanceSparseOperationResult> InstanceSparseExecuteV1Async(BridgeRequest request)
{
    ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]);
    return await instanceResources.ExecuteSparseAsync(ParsePayload<InstanceSparseExecutePayload>(request).PreviewToken);
}
async Task<GlobalConfigurationSnapshot> GetGlobalConfigurationV1Async(BridgeRequest request)
{ ValidateGlobalConfigurationRequest(request); ValidateEmptyPayload(request); return await globalConfigurationGateway.GetAsync(); }
async Task<GlobalConfigurationPreview> PreviewGlobalConfigurationV1Async(BridgeRequest request)
{
    ValidateGlobalConfigurationRequest(request); ValidatePayload(request, ["Changes"], ["Changes"]);
    var payload = ParsePayload<GlobalConfigurationPreviewPayload>(request);
    if (payload.Changes is null) throw new ArgumentException("Global configuration changes are required.");
    return await globalConfigurationGateway.PreviewAsync(payload.Changes);
}
async Task<GlobalConfigurationApplyResult> ExecuteGlobalConfigurationV1Async(BridgeRequest request)
{
    ValidateGlobalConfigurationRequest(request); ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]);
    var payload = ParsePayload<GlobalConfigurationExecutePayload>(request);
    return await globalConfigurationGateway.ExecuteAsync(payload.PreviewToken);
}
static void ValidateGlobalConfigurationRequest(BridgeRequest request)
{
    if (request.Id is not null || request.ExpectedRevision is not null || request.Token is not null || request.Name is not null || request.ActionId is not null)
        throw new ArgumentException("The global configuration request is invalid.");
}
async Task<MonitoringSnapshotResult> GetMonitoringSnapshotAsync(BridgeRequest request)
{
    ValidatePayload(request, ["Name", "IntervalSeconds"], ["Name", "IntervalSeconds"]);
    var p = ParsePayload<MonitoringSnapshotPayload>(request);
    if (string.IsNullOrWhiteSpace(p.Name) || p.IntervalSeconds is not (1 or 2 or 5 or 10)) throw new ArgumentException("Monitoring request is invalid.");
    var instance = (await instances.GetInstancesAsync()).FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("WSL instance was not found.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(p.IntervalSeconds + 10));
    return await monitoringAutomation.GetSnapshotAsync(instance, TimeSpan.FromSeconds(p.IntervalSeconds), deadline.Token);
}
async Task<MonitoringProcessActionPreview> PreviewMonitoringProcessActionAsync(BridgeRequest request)
{
    ValidatePayload(request, ["SnapshotToken", "ProcessId", "Action"], ["SnapshotToken", "ProcessId", "Action"]);
    var p = ParsePayload<MonitoringPreviewPayload>(request);
    return await monitoringAutomation.PreviewAsync(p.SnapshotToken, p.ProcessId, p.Action);
}
async Task<ProcessActionResult> ExecuteMonitoringProcessActionAsync(BridgeRequest request)
{
    ValidatePayload(request, ["PreviewToken"], ["PreviewToken"]);
    var p = ParsePayload<MonitoringExecutePayload>(request);
    return await monitoringAutomation.ExecuteAsync(p.PreviewToken);
}
async Task<RepairPreview> PreviewHealthRepairAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<HealthFindingPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Health finding payload is required.");
    return await healthRepairs.PreviewAsync(p.Finding);
}
async Task<TemplateSource> AddMarketplaceSourceAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceSourcePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace source payload is required."); return await marketplace.AddSourceAsync(p.Url, p.Kind, p.ExplicitlyAcceptedNonHttps); }
async Task<TemplateMarketplaceStatus> GetMarketplaceStatusAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceStatusPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace source payload is required."); return string.IsNullOrWhiteSpace(p.TemplateId) || string.IsNullOrWhiteSpace(p.ManifestDigest) ? await marketplace.GetStatusAsync(p.SourceId) : await marketplace.GetStatusAsync(p.SourceId, p.TemplateId, p.ManifestDigest); }
async Task<object> SetMarketplaceSourceEnabledAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceSourceEnabledPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace source payload is required."); await marketplace.SetSourceEnabledAsync(p.SourceId, p.Enabled); return new { }; }
async Task<object> RemoveMarketplaceSourceAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceSourceRemovePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace source payload is required."); await marketplace.RemoveSourceAsync(p.SourceId); return new { }; }
async Task<TemplateReviewGrant> CreateMarketplaceReviewGrantAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceReviewGrantPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace review payload is required."); return await marketplace.CreateReviewGrantAsync(p.SourceId, p.Sha256); }
async Task<TemplateArtifact> ApproveMarketplaceCandidateAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceApprovalPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace approval payload is required."); return await marketplace.ApproveCandidateAsync(p.ReviewToken); }
async Task<TemplateUpdateReview> ReviewMarketplaceUpdateAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceExactEntryPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace review payload is required."); return await marketplace.ReviewUpdateAsync(p.SourceId, p.TemplateId, p.ManifestDigest); }
async Task<TemplateScriptDiff> ReviewMarketplaceScriptDiffAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceArtifactPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace artifact payload is required."); return await marketplace.ReviewScriptDiffAsync(p.TemplateId, p.Sha256); }
async Task<IReadOnlyList<TemplateArtifactHistoryEntry>> GetMarketplaceArtifactHistoryAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceTemplatePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace template payload is required."); return await marketplace.GetArtifactHistoryAsync(p.TemplateId); }
async Task<object> RollbackMarketplaceArtifactAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceArtifactPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace artifact payload is required."); await marketplace.RollbackAsync(p.TemplateId, p.Sha256); return new { }; }
async Task<TemplateArtifact> DownloadMarketplaceArtifactAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<MarketplaceDownloadPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Marketplace download payload is required."); return string.IsNullOrWhiteSpace(p.TemplateId) || string.IsNullOrWhiteSpace(p.ManifestDigest) ? throw new ArgumentException("Exact catalog entry identity is required.") : await marketplace.DownloadArtifactAsync(p.SourceId, p.TemplateId, p.ManifestDigest); }

async Task<WorkspaceLaunchResult> LaunchAsync(BridgeRequest request)
{
    var id = request.Id ?? throw new ArgumentException("Workspace id is required.");
    return await service.LaunchAsync(id, request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required."), request.Token ?? throw new ArgumentException("Launch token is required."),
        new BridgeProgress(WriteFrame));
}

async Task<WorkspaceActionResult> RetryAsync(BridgeRequest request)
{
    var result = await service.RetryAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ActionId ?? throw new ArgumentException("Action id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required."), request.Token ?? throw new ArgumentException("Retry token is required."));
    WriteFrame(new(true, result, null, null, "progress"));
    return result;
}

static async Task<object> RemoveAsync(IWorkspaceService service, BridgeRequest request) { await service.RemoveAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required.")); return new { }; }
static Task<WorkspaceDefinition> DuplicateAsync(IWorkspaceService service, BridgeRequest request) => service.DuplicateAsync(request.Id ?? throw new ArgumentException("Workspace id is required."), request.Name ?? throw new ArgumentException("Workspace name is required."), request.ExpectedRevision ?? throw new ArgumentException("Expected revision is required."));
static WorkspaceDefinition ParseDefinition(string payload, JsonSerializerOptions options) => JsonSerializer.Deserialize<WorkspaceDefinition>(payload, options) ?? throw new ArgumentException("Workspace definition is required.");
public sealed record BridgeRequest(string Operation, Guid? Id, JsonElement? Payload, long? ExpectedRevision, string? Token = null, string? Name = null, Guid? ActionId = null);
public sealed record InstanceNamePayload(string Name, bool KeepAlive = false);
public sealed record InstanceResourcePayload(string Name);
public sealed record InstanceSparsePayload(string Name, bool Enabled);
public sealed record InstanceSparseExecutePayload(string PreviewToken);
public sealed record LifecycleRemovePayload(string Name, bool KeepFiles);
public sealed record LifecycleMovePayload(string Name, string Destination);
public sealed record LifecycleRenamePayload(string Name, string NewName);
public sealed record LifecycleExportPayload(string Name, string Destination, bool StopRunning);
public sealed record LifecycleImportPayload(string Name, string Source, string InstallPath);
public sealed record LifecyclePathExecutePayload(string PreviewToken);
public sealed record LifecyclePayload(string Name, bool KeepFiles = false, string? Destination = null, string? NewName = null, bool StopRunning = false, string? Source = null, string? InstallPath = null);
public sealed record LifecycleExecutePayload(string PreviewToken);
public sealed record RecoveryExecutePayload(string PreviewToken);
public sealed record BackupManualPayload(string InstanceName, int RetentionCount, string? Destination = null);
public sealed record GlobalConfigurationPreviewPayload(Dictionary<string, string?> Changes);
public sealed record GlobalConfigurationExecutePayload(string PreviewToken);
public sealed record InstanceListPayload(bool IncludeRelease = false, bool IncludeUser = false, bool SkipDiskSize = false, bool ForceRefresh = false);
public sealed record SettingsSavePayload(GlobalSettings Settings);
public sealed record CatalogSourceAddPayload(string Name, string Url, string? Description, bool IsActive);
public sealed record CatalogSourceUpdatePayload(string SourceId, string Name, string Url, string? Description, bool IsActive);
public sealed record CatalogSourceIdPayload(string SourceId);
public sealed record CatalogSourceTestPayload(string Url);
public sealed record CatalogSourceActivePayload(string SourceId, bool IsActive);
public sealed record CatalogSourceReorderPayload(List<string> SourceIds);
public sealed record CatalogListPayload(string? Family = null, bool ForceReload = false);
public sealed record CatalogSearchPayload(string Query);
public sealed record CatalogGetPayload(string Id);
public sealed record CatalogRefreshPayload(string? SourceUrl = null);
public sealed record PackageCacheDeletePayload(string? CacheEntryId = null, string? DefaultName = null, string? LocalPath = null);
public sealed record TerminalLaunchPayload(string InstanceName, string? StartPath = null, TerminalKind TerminalKind = TerminalKind.Auto);

/// <summary>Owns the only executable and argument shapes used by fixed external-launch routes.</summary>
public static class FixedLaunchProcess
{
    public static ProcessStartInfo CreateTerminalStartInfo(TerminalKind kind, string instanceName, string? startPath)
    {
        var info = new ProcessStartInfo { FileName = kind == TerminalKind.WindowsTerminal ? "wt.exe" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), UseShellExecute = false };
        var arguments = kind == TerminalKind.WindowsTerminal
            ? startPath is null ? new[] { "-w", "0", "wsl", "-d", instanceName } : new[] { "-w", "0", "wsl", "-d", instanceName, "--cd", startPath }
            : startPath is null ? new[] { "/k", "wsl", "-d", instanceName } : new[] { "/k", "wsl", "-d", instanceName, "--cd", startPath };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    public static ProcessStartInfo CreatePackageCacheStartInfo(string existingCacheRoot)
    {
        var info = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
        info.ArgumentList.Add(existingCacheRoot);
        return info;
    }
    public static ProcessStartInfo CreateExplorerStartInfo(string existingTarget)
    {
        var info = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
        info.ArgumentList.Add(existingTarget);
        return info;
    }
}
public sealed record FixedExplorerResult(bool Succeeded, string OutcomeCode);
/// <summary>Closed fixed-target Explorer routes; dependencies are injectable solely for deterministic bridge tests.</summary>
public sealed class FixedExplorerRoutes
{
    private readonly IRecoveryPointService recovery;
    private readonly Func<string> userProfile;
    private readonly Action<ProcessStartInfo> launch;
    private readonly Func<string, bool> noReparsePath;

    public FixedExplorerRoutes(IRecoveryPointService recovery, Func<string> userProfile, Action<ProcessStartInfo> launch, Func<string, bool> noReparsePath)
    { this.recovery = recovery; this.userProfile = userProfile; this.launch = launch; this.noReparsePath = noReparsePath; }

    public FixedExplorerResult OpenWslConfig(BridgeRequest request)
    {
        Validate(request, false);
        var path = Path.Combine(userProfile(), ".wslconfig");
        var file = new FileInfo(path);
        if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint) || !noReparsePath(path)) throw new InvalidOperationException("The current user's .wslconfig file is unavailable or unsafe.");
        launch(FixedLaunchProcess.CreateExplorerStartInfo(path));
        return new(true, "Opened");
    }

    public async Task<FixedExplorerResult> OpenRecoveryPointAsync(BridgeRequest request)
    {
        Validate(request, true);
        var point = (await recovery.ListAsync()).SingleOrDefault(x => x.Manifest.Id == request.Id) ?? throw new InvalidOperationException("The recovery point no longer exists.");
        if (!RecoveryPathSafety.IsOwnedPointDirectory(point.DirectoryPath, point.Manifest)) throw new InvalidOperationException("The recovery point path is unsafe.");
        launch(FixedLaunchProcess.CreateExplorerStartInfo(point.DirectoryPath));
        return new(true, "Opened");
    }

    private static void Validate(BridgeRequest request, bool requireId)
    {
        if (request.Payload is not null || requireId != request.Id.HasValue || request.ExpectedRevision is not null || request.Token is not null || request.Name is not null || request.ActionId is not null)
            throw new ArgumentException("The fixed Explorer operation request is invalid.");
    }
}
public sealed record NetworkPortMappingPayload(string Name, string? Protocol = null);
public sealed record NetworkProbePayload(NetworkProbeRequest Request);
public sealed record NetworkModePayload(WslNetworkingMode Mode);
public sealed record NetworkSettingsPayload(NetworkSettings Settings);
public sealed record LoopbackBrowserPayload(string Host, int Port);

/// <summary>Single fixed-loopback launch boundary; tests may replace it without accepting arbitrary executables.</summary>
public sealed record FirewallRequestPayload(FirewallRuleRequest Request);
public sealed record FirewallCreatePayload(string PreviewRuleId);
public sealed record FirewallRemovePreviewPayload(string RuleId);
public sealed record FirewallRemovePayload(string PreviewToken);
public sealed record PodmanUnitPayload(string InstanceName, PodmanUserUnit Unit, SystemdAction Action);
public sealed record PodmanConnectionPayload(string InstanceName, string Name, string Endpoint);
public sealed record PodmanStatusPayload(string InstanceName);
public sealed record DockerIntegrationPayload(string Name, bool Enabled = false);
public sealed record CapabilityPayload(string? InstanceName, bool InstanceOnly);
public sealed record CapabilityInstancePayload(string InstanceName);
public sealed record SystemdPayload(string InstanceName, string? Unit, SystemdAction? Action, SystemdScope Scope = SystemdScope.User);
public sealed record SystemdJournalPayload(string InstanceName, string Unit, SystemdScope Scope = SystemdScope.User, string? Search = null, int LineLimit = 200);
public sealed record SystemdPreviewPayload(SystemdOperationPreview Preview);
public sealed record SystemdExecutePayload(string PreviewToken);
public sealed record WslgInstancePayload(string InstanceName);
public sealed record WslgActionPayload(string DiscoveryToken, string ApplicationId);
public sealed record WslgPinPayload(string DiscoveryToken, string ApplicationId, bool Pinned);
public sealed record RecoveryCreatePayload(RecoveryPointCreateRequest Request);
public sealed record RecoveryRestorePayload(RecoveryRestoreRequest Request);
public sealed record RecoveryClonePayload(RecoveryCloneRequest Request);
public sealed record RecoveryNotesPayload(Guid Id, string Description, IReadOnlyList<string> Tags, bool Pinned);
public sealed record RecoveryRetentionPayload(string SourceInstance, int? Maximum = null);
public sealed record MonitoringSnapshotPayload(string Name, int IntervalSeconds);
public sealed record MonitoringPreviewPayload(string SnapshotToken, int ProcessId, MonitoringProcessAction Action);
public sealed record MonitoringExecutePayload(string PreviewToken);
public sealed record HealthFindingPayload(HealthFinding Finding);
public sealed record HealthRepairPayload(HealthFinding Finding, bool Confirmed);
public sealed record DiagnosticPreviewPayload(DiagnosticReportFormat Format, IReadOnlyList<string>? SelectedLogIds = null, int? DeadlineMilliseconds = null);
public sealed record DiagnosticExportPayload(string DestinationFileName, int? DeadlineMilliseconds = null);
public sealed record MarketplaceSourcePayload(string Url, TemplateSourceKind Kind, bool ExplicitlyAcceptedNonHttps);
public sealed record MarketplaceSourceIdPayload(string SourceId);
public sealed record MarketplaceStatusPayload(string SourceId, string? TemplateId = null, string? ManifestDigest = null);

/// <summary>
/// Read-only adapter used by the concrete bridge health composition. It permits health checks to
/// inspect Core-owned settings, backup, template, and monitoring state without allowing a scan to
/// invoke PowerShell mutations.
/// </summary>
public sealed class BridgeReadOnlyPowerShellService : IPowerShellService
{
    private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("Health scan does not execute PowerShell."));
    public Task<T?> ExecuteAsync<T>(string cmdlet, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default) => Unavailable<T?>();
    public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default) => Unavailable<string>();
    public Task<PowerShellScriptResult> ExecuteScriptWithResultAsync(string script, CancellationToken cancellationToken = default) => Unavailable<PowerShellScriptResult>();
    public Task<string> ExecuteScriptStreamingAsync(string script, Action<string>? onOutputLine = null, Action<string>? onErrorLine = null, CancellationToken cancellationToken = default) => Unavailable<string>();
    public Task ImportModuleAsync(string modulePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsModuleLoadedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<PowerShellScriptResult> ExecuteModuleCmdletAsync(string cmdletName, Dictionary<string, object>? parameters = null, ModuleCallOptions? options = null, CancellationToken cancellationToken = default) => Unavailable<PowerShellScriptResult>();
    public Task<T?> ExecuteModuleCmdletAsync<T>(string cmdletName, Dictionary<string, object>? parameters = null, ModuleCallOptions? options = null, CancellationToken cancellationToken = default) => Unavailable<T?>();
    public Task<string> GetDiagnosticInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult("Read-only bridge health composition.");
}
public sealed record MarketplaceSourceEnabledPayload(string SourceId, bool Enabled);
public sealed record MarketplaceSourceRemovePayload(string SourceId);
public sealed record MarketplaceApprovalPayload(string ReviewToken);
public sealed record MarketplaceReviewGrantPayload(string SourceId, string Sha256);
public sealed record MarketplaceReviewPayload(TemplateManifestV2 Previous, TemplateManifestV2 Candidate);
public sealed record MarketplaceExactEntryPayload(string SourceId, string TemplateId, string ManifestDigest);
public sealed record MarketplaceDownloadPayload(string SourceId, string? TemplateId = null, string? ManifestDigest = null);
public sealed record MarketplaceTemplatePayload(string TemplateId);
public sealed record MarketplaceArtifactPayload(string TemplateId, string Sha256);
public sealed record BridgeResponse(bool Succeeded, object? Value, string? ErrorCode, string? ErrorMessage, string Frame = "result");
/// <summary>Only the five reviewed lifecycle shapes are executable from the bridge.</summary>
public sealed class BridgeProgress(Action<BridgeResponse> write) : IProgress<WorkspaceActionResult>
{
    public void Report(WorkspaceActionResult value) => write(new BridgeResponse(true, value, null, null, "progress"));
}
