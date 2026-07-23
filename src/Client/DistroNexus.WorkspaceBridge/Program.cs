using System.Text.Json;
using System.Text.Json.Serialization;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.WorkspaceBridge;

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
            _ => throw new ArgumentException("Bridge operation is unsupported.")
        };
        response = new(true, value, null, null);
    }
    catch (DistroNexus.Core.Exceptions.WslOperationFailedException ex) { response = new(false, null, ex.Code.ToString(), ex.Message); }
    catch (Exception ex) { response = new(false, null, ex is InvalidOperationException ? "Workspace.ConflictOrState" : "Workspace.Bridge.Invalid", ex.Message); }
    WriteFrame(response);
}

async Task<object> PreviewPodmanUnitAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanUnitPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman payload is required."); var preview = await containers.PreviewPodmanUserUnitAsync(p.InstanceName, p.Unit, p.Action); return new { Token = preview.SystemdPreview.PreviewToken, InstanceName = p.InstanceName, Unit = p.Unit, Action = p.Action, Effects = preview.SystemdPreview.Effects }; }
async Task<object> ExecutePodmanUnitAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanUnitPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman payload is required."); return await containers.ExecutePodmanUserUnitAsync(request.Token ?? string.Empty, p.InstanceName, p.Unit, p.Action); }
async Task<object> PreviewPodmanConnectionAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanConnectionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman connection payload is required."); var preview = await containers.PreviewPodmanConnectionAsync(p.InstanceName, new PodmanConnectionRequest(p.Name, new Uri(p.Endpoint, UriKind.Absolute))); return new { preview.Token, preview.InstanceName, Name = preview.Request.Name, Endpoint = preview.Request.SafeEndpoint, preview.Operation, preview.ExistingEndpoint, preview.Effects }; }
async Task<object> ExecutePodmanConnectionAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanConnectionPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Podman connection payload is required."); return await containers.ConfigurePodmanConnectionAsync(request.Token ?? string.Empty, p.InstanceName, new PodmanConnectionRequest(p.Name, new Uri(p.Endpoint, UriKind.Absolute))); }
async Task<ContainerRuntimeStatusResponse> ContainerRuntimeStatusAsync(BridgeRequest request) { var p = JsonSerializer.Deserialize<PodmanStatusPayload>(request.Payload?.GetRawText() ?? "", options) ?? throw new ArgumentException("Container runtime payload is required."); return await ContainerRuntimeBridgeHandler.GetStatusAsync(containers, p.InstanceName); }
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
public sealed record PodmanUnitPayload(string InstanceName, PodmanUserUnit Unit, SystemdAction Action);
public sealed record PodmanConnectionPayload(string InstanceName, string Name, string Endpoint);
public sealed record PodmanStatusPayload(string InstanceName);
public sealed record MarketplaceSourcePayload(string Url, TemplateSourceKind Kind, bool ExplicitlyAcceptedNonHttps);
public sealed record MarketplaceSourceIdPayload(string SourceId);
public sealed record MarketplaceStatusPayload(string SourceId, string? TemplateId = null, string? ManifestDigest = null);
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
