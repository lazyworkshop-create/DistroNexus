using System.Text.Json;
using System.Text.Json.Serialization;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.WorkspaceBridge;
using Microsoft.Extensions.Logging.Abstractions;

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
var root = Environment.GetEnvironmentVariable("DISTRONEXUS_WORKSPACE_STORE_ROOT");
// Keep this composition deliberately equivalent to the desktop composition.  The
// bridge is a real execution boundary, not a persistence-only surrogate: Core owns
// validation, preview tokens, capability checks, and structured process requests.
var processes = new ProcessRunner();
var instances = new BridgeWslManagerService(processes);
var capabilities = new PlatformCapabilityService(processes);
var distributionConfiguration = new DistributionConfigurationService(processes);
var systemd = new SystemdService(processes, capabilities, distributionConfiguration);
var containers = ContainerRuntimeBridgeComposition.Create(processes, systemd);
var wslg = new WslgApplicationService(processes, capabilities, root);
var recovery = new RecoveryPointService(new WslRecoveryPointRuntime(processes, capabilities), root: root);
var monitoring = new MonitoringService(processes);
var applicationRoot = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
var bridgePowerShell = new BridgeReadOnlyPowerShellService();
var settings = new SettingsService(NullLogger<SettingsService>.Instance, Path.Combine(applicationRoot, "settings.json"));
var globalConfiguration = new WslConfigService(NullLogger<WslConfigService>.Instance, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
var backups = new BackupService(bridgePowerShell, NullLogger<BackupService>.Instance, applicationRoot);
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
]);
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
    try
    {
        var request = JsonSerializer.Deserialize<BridgeRequest>(line, options) ?? throw new ArgumentException("Bridge request is invalid.");
        var payload = request.Payload?.GetRawText() ?? string.Empty;
        object value = request.Operation switch
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
            "capability" => await GetCapabilitiesAsync(request),
            "systemdList" => await ListSystemdAsync(request),
            "systemdPreview" => await PreviewSystemdAsync(request),
            "systemdExecute" => await ExecuteSystemdAsync(request),
            "wslgStatus" => await GetWslgStatusAsync(request),
            "wslgDiscover" => await DiscoverWslgAsync(request),
            "wslgLaunch" => await LaunchWslgAsync(request),
            "recoveryList" => await recovery.ListAsync(),
            "recoveryHistory" => await recovery.GetHistoryAsync(),
            "recoveryVerify" => await recovery.VerifyAsync(request.Id ?? throw new ArgumentException("Recovery id is required.")),
            "recoveryPreviewCreate" => await PreviewRecoveryCreateAsync(request),
            "recoveryCreate" => await CreateRecoveryAsync(request),
            "recoveryPreviewRestore" => await PreviewRecoveryRestoreAsync(request),
            "recoveryRestore" => await RestoreRecoveryAsync(request),
            "recoveryPreviewRemove" => await recovery.PreviewDeleteAsync(request.Id ?? throw new ArgumentException("Recovery id is required.")),
            "recoveryRemove" => await RemoveRecoveryAsync(request),
            "monitorSnapshot" => await GetMonitoringSnapshotAsync(request),
            "healthScan" => await health.ScanAsync(),
            "healthRepairPreview" => await PreviewHealthRepairAsync(request),
            "healthRepairExecute" => await ExecuteHealthRepairAsync(request),
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
            "instance.start.v1" => await instances.StartInstanceAsync(ParseInstanceName(request)),
            "instance.stop.v1" => await instances.StopInstanceAsync(ParseInstanceName(request)),
            "settings.get.v1" => GetSettings(request),
            "settings.save.v1" => SaveSettings(request),
            "settings.reset.v1" => ResetSettings(request),
            _ => throw new ArgumentException("Bridge operation is unsupported.")
        };
        response = new(true, value, null, null);
    }
    catch (DistroNexus.Core.Exceptions.WslOperationFailedException ex) { response = new(false, null, ex.Code.ToString(), ex.Message); }
    catch (Exception ex) { response = new(false, null, ex is InvalidOperationException ? "Workspace.ConflictOrState" : "Workspace.Bridge.Invalid", ex.Message); }
    WriteFrame(response);
}

string ParseInstanceName(BridgeRequest request)
{
    var payload = JsonSerializer.Deserialize<InstanceNamePayload>(request.Payload?.GetRawText() ?? string.Empty, options)
        ?? throw new ArgumentException("Instance payload is required.");
    if (string.IsNullOrWhiteSpace(payload.Name))
        throw new ArgumentException("Instance name is required.");
    return payload.Name;
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

static void RequireNoPayload(BridgeRequest request, string message)
{
    if (request.Payload is not null)
        throw new ArgumentException(message);
}

InstanceListOptions ParseInstanceListOptions(BridgeRequest request) =>
    JsonSerializer.Deserialize<InstanceListPayload>(request.Payload?.GetRawText() ?? "{}", options) is { } payload
        ? new InstanceListOptions(payload.IncludeRelease, payload.IncludeUser, payload.SkipDiskSize)
        : new InstanceListOptions(false, false, false);

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
async Task<IReadOnlyList<SystemdServiceInfo>> ListSystemdAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd payload is required.");
    return await systemd.ListAsync(p.InstanceName, p.Scope);
}
async Task<SystemdOperationPreview> PreviewSystemdAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd payload is required.");
    if (p.Action is null || string.IsNullOrWhiteSpace(p.Unit)) throw new ArgumentException("A systemd unit and action are required.");
    return await systemd.PreviewAsync(p.InstanceName, new SystemdUnitName(p.Unit), p.Action.Value, p.Scope);
}
async Task<SystemdOperationResult> ExecuteSystemdAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<SystemdPreviewPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("systemd preview is required.");
    return await systemd.ExecuteAsync(p.Preview);
}
async Task<WslgApplicationStatus> GetWslgStatusAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<WslgInstancePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg payload is required.");
    return await wslg.GetStatusAsync(p.InstanceName);
}
async Task<IReadOnlyList<WslgApplication>> DiscoverWslgAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<WslgInstancePayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg payload is required.");
    return await wslg.DiscoverAsync(p.InstanceName);
}
async Task<WslgLaunchResult> LaunchWslgAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<WslgApplicationPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("WSLg application payload is required.");
    return await wslg.LaunchAsync(p.Application);
}
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
async Task<MonitoringSample> GetMonitoringSnapshotAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<MonitoringPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Monitoring payload is required.");
    var instance = (await instances.GetInstancesAsync()).FirstOrDefault(x => string.Equals(x.Name, p.InstanceName, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("WSL instance was not found.");
    await using var session = monitoring.CreateSession(instance, TimeSpan.FromSeconds(1));
    await session.StartAsync();
    await Task.Delay(TimeSpan.FromMilliseconds(1250));
    await session.StopAsync();
    return session.Samples.Last();
}
async Task<RepairPreview> PreviewHealthRepairAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<HealthFindingPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Health finding payload is required.");
    return await healthRepairs.PreviewAsync(p.Finding);
}
async Task<RepairResult> ExecuteHealthRepairAsync(BridgeRequest request)
{
    var p = JsonSerializer.Deserialize<HealthRepairPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Health repair payload is required.");
    return await healthRepairs.ExecuteAsync(p.Finding, new RepairExecutionRequest(request.Token ?? string.Empty, p.Confirmed));
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
public sealed record InstanceNamePayload(string Name);
public sealed record InstanceListPayload(bool IncludeRelease = false, bool IncludeUser = false, bool SkipDiskSize = false);
public sealed record SettingsSavePayload(GlobalSettings Settings);
public sealed record PodmanUnitPayload(string InstanceName, PodmanUserUnit Unit, SystemdAction Action);
public sealed record PodmanConnectionPayload(string InstanceName, string Name, string Endpoint);
public sealed record PodmanStatusPayload(string InstanceName);
public sealed record CapabilityPayload(string? InstanceName, bool InstanceOnly);
public sealed record SystemdPayload(string InstanceName, string? Unit, SystemdAction? Action, SystemdScope Scope = SystemdScope.User);
public sealed record SystemdPreviewPayload(SystemdOperationPreview Preview);
public sealed record WslgInstancePayload(string InstanceName);
public sealed record WslgApplicationPayload(WslgApplication Application);
public sealed record RecoveryCreatePayload(RecoveryPointCreateRequest Request);
public sealed record RecoveryRestorePayload(RecoveryRestoreRequest Request);
public sealed record MonitoringPayload(string InstanceName);
public sealed record HealthFindingPayload(HealthFinding Finding);
public sealed record HealthRepairPayload(HealthFinding Finding, bool Confirmed);
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
public sealed class BridgeProgress(Action<BridgeResponse> write) : IProgress<WorkspaceActionResult>
{
    public void Report(WorkspaceActionResult value) => write(new BridgeResponse(true, value, null, null, "progress"));
}
