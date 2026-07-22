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
var runtime = new WorkspaceRuntime(instances, processes);
var gate = new WorkspaceActionCapabilityGate(capabilities);
var handlers = Enum.GetValues<WorkspaceActionType>()
    .Select(type => (IWorkspaceActionHandler)new WorkspaceActionHandler(type, runtime, gate))
    .ToArray();
var service = new WorkspaceService(runtime, root, handlers: handlers);
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
            _ => throw new ArgumentException("Bridge operation is unsupported.")
        };
        response = new(true, value, null, null);
    }
    catch (Exception ex) { response = new(false, null, ex is InvalidOperationException ? "Workspace.ConflictOrState" : "Workspace.Bridge.Invalid", ex.Message); }
    WriteFrame(response);
}

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
public sealed record BridgeResponse(bool Succeeded, object? Value, string? ErrorCode, string? ErrorMessage, string Frame = "result");
public sealed class BridgeProgress(Action<BridgeResponse> write) : IProgress<WorkspaceActionResult>
{
    public void Report(WorkspaceActionResult value) => write(new BridgeResponse(true, value, null, null, "progress"));
}
