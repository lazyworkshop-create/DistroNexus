using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly VersionedJsonStore<List<WorkspaceDefinition>> _store;
    private readonly IWorkspaceRuntime _runtime;
    private readonly IReadOnlyDictionary<WorkspaceActionType, IWorkspaceActionHandler> _handlers;
    private readonly IWorkspaceDecisionProvider? _decisions;
    private readonly ConcurrentDictionary<string, (Guid Id, long Revision, DateTimeOffset Expires)> _tokens = new();
    private readonly ConcurrentDictionary<string, (Guid Id, string Digest, long DocumentRevision, DateTimeOffset Expires)> _importTokens = new();
    private readonly ConcurrentDictionary<string, (Guid Id, Guid ActionId, long Revision, DateTimeOffset Expires)> _retryTokens = new();
    private readonly ConcurrentDictionary<string, (Guid Id, long Revision, DateTimeOffset Expires)> _closeTokens = new();
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public WorkspaceService(IWorkspaceRuntime runtime, string? appDataDirectory = null, IWorkspaceDecisionProvider? decisions = null, IEnumerable<IWorkspaceActionHandler>? handlers = null)
    {
        _runtime = runtime; _decisions = decisions;
        _handlers = (handlers ?? []).GroupBy(handler => handler.Type).ToDictionary(group => group.Key, group => group.Single());
        var root = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
        _store = new VersionedJsonStore<List<WorkspaceDefinition>>(Path.Combine(root, "workspaces.json"), 1, n => n.Deserialize<List<WorkspaceDefinition>>(_json) ?? [], serializerOptions: _json);
    }
    public async Task<IReadOnlyList<WorkspaceDefinition>> ListAsync(CancellationToken ct = default) => (await ReadAsync(ct)).Value.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    public async Task<WorkspaceDefinition> SaveAsync(WorkspaceDefinition definition, long expectedRevision, CancellationToken ct = default)
    {
        WorkspaceValidation.ValidateDefinition(definition);
        var current = await ReadAsync(ct); var all = current.Value;
        var index = all.FindIndex(x => x.Id == definition.Id);
        if (index >= 0 && all[index].Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict.");
        if (index < 0 && expectedRevision != 0) throw new InvalidOperationException("Workspace revision conflict.");
        // Edits preserve trust state; only explicit approval changes it.
        var saved = definition with { Revision = (index >= 0 ? all[index].Revision : 0) + 1, TrustState = index >= 0 ? all[index].TrustState : definition.TrustState, TrustedAt = index >= 0 ? all[index].TrustedAt : definition.TrustedAt };
        if (index >= 0) all[index] = saved; else all.Add(saved);
        await WriteAsync(all, current.Revision, ct); return saved;
    }
    public async Task<WorkspaceDryRunResult> PreviewSaveAsync(WorkspaceDefinition definition, long expectedRevision, CancellationToken ct = default)
    {
        WorkspaceValidation.ValidateDefinition(definition);
        var current = await ReadAsync(ct); var existing = current.Value.SingleOrDefault(x => x.Id == definition.Id);
        if ((existing is null && expectedRevision != 0) || (existing is not null && existing.Revision != expectedRevision)) throw new InvalidOperationException("Workspace revision conflict.");
        return DryRun("save", definition.Id, existing is null ? 1 : existing.Revision + 1, ["Workspace schema and expected revision are valid."]);
    }
    public async Task RemoveAsync(Guid id, long expectedRevision, CancellationToken ct = default) { var current = await ReadAsync(ct); var item = current.Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException(); if (item.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict."); current.Value.Remove(item); await WriteAsync(current.Value, current.Revision, ct); }
    public async Task<WorkspaceDryRunResult> PreviewRemoveAsync(Guid id, long expectedRevision, CancellationToken ct = default)
    { var item = (await ReadAsync(ct)).Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException(); if (item.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict."); return DryRun("remove", id, item.Revision, ["Workspace exists and expected revision is valid."]); }
    public async Task<WorkspaceDefinition> DuplicateAsync(Guid id, string displayName, long expectedRevision, CancellationToken ct = default)
    {
        var current = await ReadAsync(ct);
        var source = current.Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (source.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict.");
        var duplicate = source with { Id = Guid.NewGuid(), DisplayName = displayName, Revision = 1 };
        WorkspaceValidation.ValidateDefinition(duplicate);
        current.Value.Add(duplicate);
        await WriteAsync(current.Value, current.Revision, ct);
        return duplicate;
    }
    public async Task<WorkspaceDryRunResult> PreviewDuplicateAsync(Guid id, string displayName, long expectedRevision, CancellationToken ct = default)
    { var source = (await ReadAsync(ct)).Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException(); if (source.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict."); WorkspaceValidation.ValidateDefinition(source with { Id = Guid.NewGuid(), DisplayName = displayName, Revision = 1 }); return DryRun("duplicate", id, 1, ["Source workspace, name, and expected revision are valid."]); }
    public async Task<string> ExportAsync(Guid id, long expectedRevision, CancellationToken ct = default)
    {
        var result = await _store.ReadLockedAsync(document =>
        {
            var definitions = document.Value;
            for (var index = 0; index < definitions.Count; index++) definitions[index] = Normalize(definitions[index]);
            foreach (var definition in definitions) WorkspaceValidation.ValidateDefinition(definition);
            var item = definitions.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException();
            if (item.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict.");
            return JsonSerializer.Serialize(item, _json);
        }, ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Error == StoreErrorKind.RevisionConflict ? "Workspace revision conflict." : result.Message);
        return result.Value!;
    }
    public async Task<WorkspaceDryRunResult> PreviewExportDryRunAsync(Guid id, long expectedRevision, CancellationToken ct = default)
    {
        var item = (await ReadAsync(ct)).Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (item.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict.");
        WorkspaceValidation.ValidateDefinition(item);
        return DryRun("export", id, item.Revision, ["Workspace schema and expected revision are valid. Export will not modify the workspace definition."]);
    }
    public async Task<WorkspaceImportPreview> PreviewImportAsync(string content, CancellationToken ct = default)
    {
        var parsed = Parse(content) with { TrustState = WorkspaceTrustState.Untrusted, TrustedAt = null, Revision = 0 };
        var commands = parsed.ActionGroups.SelectMany(x => x.Actions).Where(x => x.Type is WorkspaceActionType.LinuxCommand or WorkspaceActionType.ShellScript).Select(x => string.Join(' ', x.Arguments)).ToArray();
        var current = await ReadAsync(ct);
        var token = Token(parsed.Id, current.Revision, "import"); _importTokens[token] = (parsed.Id, Digest(parsed), current.Revision, DateTimeOffset.UtcNow.AddMinutes(10));
        return new WorkspaceImportPreview(parsed, commands, commands.Length == 0 ? [] : ["Imported command content will not run until explicitly trusted."], token);
    }
    public async Task<WorkspaceDefinition> ImportAsync(string content, string importToken, long expectedRevision, CancellationToken ct = default)
    {
        // Imports create a new definition, so its expected entity revision is always zero.
        if (expectedRevision != 0) throw new InvalidOperationException("Workspace import expected revision must be zero.");
        var definition = Parse(content) with { TrustState = WorkspaceTrustState.Untrusted, TrustedAt = null, Revision = 0 };
        if (!_importTokens.TryRemove(importToken, out var token) || token.Id != definition.Id || token.Digest != Digest(definition) || token.Expires < DateTimeOffset.UtcNow) throw new InvalidOperationException("Import preview expired or content changed; preview again.");
        var current = await ReadAsync(ct);
        if (current.Revision != token.DocumentRevision) throw new InvalidOperationException("Workspace import preview is stale; preview again.");
        if (current.Value.Any(x => x.Id == definition.Id)) definition = definition with { Id = Guid.NewGuid() };
        var imported = definition with { Revision = 1 };
        current.Value.Add(imported);
        await WriteAsync(current.Value, current.Revision, ct);
        return imported;
    }
    public async Task<WorkspaceDryRunResult> PreviewImportDryRunAsync(string content, long expectedRevision, CancellationToken ct = default)
    {
        if (expectedRevision != 0) throw new InvalidOperationException("Workspace import expected revision must be zero.");
        var definition = Parse(content) with { TrustState = WorkspaceTrustState.Untrusted, TrustedAt = null, Revision = 0 };
        await ReadAsync(ct);
        return DryRun("import", definition.Id, 1, ["Imported content is schema-valid and will remain untrusted."]);
    }
    public async Task<WorkspaceDefinition> ApproveTrustAsync(Guid id, long expectedRevision, CancellationToken ct = default) { var current = await ReadAsync(ct); var i = current.Value.FindIndex(x => x.Id == id); if (i < 0 || current.Value[i].Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict."); var trusted = current.Value[i] with { TrustState = WorkspaceTrustState.Trusted, TrustedAt = DateTimeOffset.UtcNow, Revision = current.Value[i].Revision + 1 }; current.Value[i] = trusted; await WriteAsync(current.Value, current.Revision, ct); return trusted; }
    public async Task<WorkspaceDryRunResult> PreviewApproveTrustAsync(Guid id, long expectedRevision, CancellationToken ct = default)
    { var item = (await ReadAsync(ct)).Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException(); if (item.Revision != expectedRevision) throw new InvalidOperationException("Workspace revision conflict."); return DryRun("approveTrust", id, item.Revision + 1, ["Explicit trust approval is ready to be confirmed."]); }
    public async Task<WorkspaceLaunchPreview> PreviewLaunchAsync(Guid id, CancellationToken ct = default)
        => await PreviewLaunchCoreAsync(id, issueToken: true, ct);
    public async Task<WorkspaceDryRunResult> PreviewLaunchDryRunAsync(Guid id, CancellationToken ct = default)
    {
        var preview = await PreviewLaunchCoreAsync(id, issueToken: false, ct);
        return new("launch", preview.WorkspaceId, preview.Revision, true, preview.Preconditions, preview.ActionResults ?? [], preview.PreflightResults ?? []);
    }
    private async Task<WorkspaceLaunchPreview> PreviewLaunchCoreAsync(Guid id, bool issueToken, CancellationToken ct)
    {
        var item = (await ReadAsync(ct)).Value.Single(x => x.Id == id); WorkspaceValidation.ValidateDefinition(item);
        var actions = item.ActionGroups.SelectMany(x => x.Actions).ToArray();
        var commands = actions.Select(action => PreviewAction(item, action)).ToArray();
        var trust = item.TrustState != WorkspaceTrustState.Trusted && actions.Any(RequiresTrust);
        var instanceAvailable = await _runtime.InstanceExistsAsync(item.InstanceName, ct);
        var checks = await Task.WhenAll(item.PreflightChecks.Select(check => _runtime.CheckAsync(item, check, ct)));
        var actionResults = new List<WorkspaceActionResult>();
        foreach (var action in actions)
        {
            try { HandlerFor(action).Validate(action); await EnsureCapabilityForPreviewAsync(item, action, ct); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            { actionResults.Add(new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Action.Unavailable", BoundedDiagnostic(ex.Message))); }
        }
        if (!instanceAvailable)
            actionResults.AddRange(actions.Select(action => new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Instance.Unavailable")));
        if (checks.Select((result, index) => (result, required: item.PreflightChecks[index].Required)).Any(x => x.required && !x.result.Succeeded))
            actionResults.AddRange(actions.Select(action => new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Preflight.Failed")));
        // Capability probes are advisory and can change between preview and launch.
        // Preserve the action-scoped result (and token) so execution/retry reports the
        // same action rather than turning a transient capability into a token failure.
        var canLaunch = instanceAvailable && !trust && !checks.Select((result, index) => (result, required: item.PreflightChecks[index].Required)).Any(x => x.required && !x.result.Succeeded);
        var token = canLaunch && issueToken ? Token(item.Id, item.Revision, "launch") : string.Empty;
        if (!string.IsNullOrEmpty(token)) _tokens[token] = (item.Id, item.Revision, DateTimeOffset.UtcNow.AddMinutes(5));
        return new(item.Id, item.Revision, ["Launches only declared workspace actions."], commands, ["Instance and action definitions are revalidated before launch."], trust, token, actionResults, checks, instanceAvailable);
    }
    public async Task<WorkspaceLaunchPreview> PreviewRetryAsync(Guid id, Guid actionId, CancellationToken ct = default) { var item=(await ReadAsync(ct)).Value.Single(x=>x.Id==id);var action=item.ActionGroups.SelectMany(x=>x.Actions).SingleOrDefault(x=>x.Id==actionId)??throw new KeyNotFoundException();var token=Token(id,item.Revision,"retry");_retryTokens[token]=(id,actionId,item.Revision,DateTimeOffset.UtcNow.AddMinutes(5));return new(id,item.Revision,["Retries only selected action."],[PreviewAction(item, action)],[],item.TrustState!=WorkspaceTrustState.Trusted&&RequiresTrust(action),token); }
    public async Task<WorkspaceActionResult> RetryAsync(Guid id, Guid actionId, long revision, string retryToken, CancellationToken ct=default) { if(!_retryTokens.TryRemove(retryToken,out var t)||t.Id!=id||t.ActionId!=actionId||t.Revision!=revision||t.Expires<DateTimeOffset.UtcNow)throw new InvalidOperationException("Retry preview expired; preview again.");var item=(await ReadAsync(ct)).Value.Single(x=>x.Id==id);if(item.Revision!=revision)throw new InvalidOperationException("Workspace changed; preview again.");var action=item.ActionGroups.SelectMany(x=>x.Actions).Single(x=>x.Id==actionId);if(item.TrustState!=WorkspaceTrustState.Trusted&&RequiresTrust(action))throw new InvalidOperationException("Explicit trust is required before command execution.");if(!await _runtime.InstanceExistsAsync(item.InstanceName,ct))throw new InvalidOperationException("The workspace instance is unavailable.");foreach(var check in item.PreflightChecks.Where(x=>x.Required)){var r=await _runtime.CheckAsync(item,check,ct);if(!r.Succeeded)throw new InvalidOperationException("Workspace preflight failed: "+r.Code);}return await ExecuteOne(item,action,null,ct); }
    public async Task<WorkspaceDryRunResult> PreviewRetryDryRunAsync(Guid id, Guid actionId, long revision, CancellationToken ct = default)
    {
        var item = (await ReadAsync(ct)).Value.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (item.Revision != revision) throw new InvalidOperationException("Workspace revision conflict.");
        var action = item.ActionGroups.SelectMany(x => x.Actions).SingleOrDefault(x => x.Id == actionId) ?? throw new KeyNotFoundException();
        var preflight = await Task.WhenAll(item.PreflightChecks.Select(check => _runtime.CheckAsync(item, check, ct)));
        var results = new List<WorkspaceActionResult>();
        try { HandlerFor(action).Validate(action); await EnsureCapabilityForPreviewAsync(item, action, ct); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { results.Add(new(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Action.Unavailable", BoundedDiagnostic(ex.Message))); }
        if (!await _runtime.InstanceExistsAsync(item.InstanceName, ct)) results.Add(new(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Instance.Unavailable"));
        if (preflight.Select((value, index) => (value, required: item.PreflightChecks[index].Required)).Any(x => x.required && !x.value.Succeeded)) results.Add(new(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Preflight.Failed"));
        return new("retry", id, revision, true, ["Retry is validated without an executable confirmation."], results, preflight);
    }
    public async Task<WorkspaceLaunchResult> LaunchAsync(Guid id, long revision, string launchToken, IProgress<WorkspaceActionResult>? progress = null, CancellationToken ct = default)
    {
        if (!_tokens.TryRemove(launchToken, out var token) || token.Id != id || token.Revision != revision || token.Expires < DateTimeOffset.UtcNow) throw new InvalidOperationException("Launch preview expired; preview again.");
        var item = (await ReadAsync(ct)).Value.Single(x => x.Id == id); if (item.Revision != revision) throw new InvalidOperationException("Workspace changed; preview again."); WorkspaceValidation.ValidateDefinition(item);
        if (!await _runtime.InstanceExistsAsync(item.InstanceName, ct)) throw new InvalidOperationException(item.MissingInstanceRemediation == WorkspaceMissingInstanceRemediation.PromptForInstallation ? "The workspace instance is unavailable; installation is required before launch." : "The workspace instance is unavailable.");
        if (item.TrustState != WorkspaceTrustState.Trusted && item.ActionGroups.SelectMany(x => x.Actions).Any(RequiresTrust)) throw new InvalidOperationException("Explicit trust is required before command execution.");
        var checks = await Task.WhenAll(item.PreflightChecks.Select(check => _runtime.CheckAsync(item, check, ct)));
        var failed = checks.Select((result, index) => (result, required: item.PreflightChecks[index].Required)).FirstOrDefault(x => !x.result.Succeeded && x.required).result;
        if (failed is not null) throw new InvalidOperationException($"Workspace preflight failed: {failed.Code}");
        var results = new List<WorkspaceActionResult>();
        try { foreach (var group in item.ActionGroups) { var tasks = group.AllowParallel ? group.Actions.Select(x => ExecuteOne(item, x, progress, ct)).ToArray() : null; var groupResults = tasks is null ? await Sequential(item, group.Actions, progress, ct) : await Task.WhenAll(tasks); results.AddRange(groupResults); if (groupResults.Any(x => x.Outcome == WorkspaceActionOutcome.Failed && item.ActionGroups.SelectMany(g => g.Actions).Single(a => a.Id == x.ActionId).FailurePolicy == WorkspaceFailurePolicy.Stop)) break; } }
        catch (OperationCanceledException) { return new(id, results, true); }
        return new(id, results, false);
    }
    public Task<WorkspaceActionResult> CloseAsync(Guid id, long revision, CancellationToken ct = default) => throw new InvalidOperationException("Workspace close requires a valid preview token.");
    public async Task<WorkspaceLaunchPreview> PreviewCloseAsync(Guid id, CancellationToken ct=default) { var item=(await ReadAsync(ct)).Value.Single(x=>x.Id==id);var token=Token(id,item.Revision,"close");_closeTokens[token]=(id,item.Revision,DateTimeOffset.UtcNow.AddMinutes(5));var effect=item.ClosePolicy.Mode==WorkspaceCloseMode.StopInstance?"Stops the bound WSL instance.":item.ClosePolicy.Mode==WorkspaceCloseMode.StopSelectedServices?"Stops only selected services.":"No close action is configured.";return new(id,item.Revision,[effect],[],["Close action requires explicit confirmation."],true,token); }
    public async Task<WorkspaceActionResult> CloseAsync(Guid id,long revision,string closeToken,CancellationToken ct=default) { if(!_closeTokens.TryRemove(closeToken,out var t)||t.Id!=id||t.Revision!=revision||t.Expires<DateTimeOffset.UtcNow)throw new InvalidOperationException("Close preview expired; preview again.");var item=(await ReadAsync(ct)).Value.Single(x=>x.Id==id);if(item.Revision!=revision)throw new InvalidOperationException("Workspace changed; preview again.");return await _runtime.CloseAsync(item,ct); }
    public string BuildShortcutArguments(Guid id) => "--workspace " + id.ToString("D");
    private static WorkspaceDryRunResult DryRun(string operation, Guid id, long revision, IReadOnlyList<string> preconditions) => new(operation, id, revision, true, preconditions, [], []);
    private async Task<WorkspaceActionResult[]> Sequential(WorkspaceDefinition d, IReadOnlyList<WorkspaceAction> actions, IProgress<WorkspaceActionResult>? p, CancellationToken ct) { var results = new List<WorkspaceActionResult>(); foreach (var action in actions) { var result = await ExecuteOne(d, action, p, ct); results.Add(result); if (result.Outcome == WorkspaceActionOutcome.Failed && (action.FailurePolicy == WorkspaceFailurePolicy.Stop || (action.FailurePolicy == WorkspaceFailurePolicy.Ask && !await (_decisions?.ContinueAfterFailureAsync(d, action, result, ct) ?? Task.FromResult(false))))) break; } return results.ToArray(); }
    private async Task<WorkspaceActionResult> ExecuteOne(WorkspaceDefinition d, WorkspaceAction a, IProgress<WorkspaceActionResult>? p, CancellationToken ct)
    {
        try
        {
            var handler = HandlerFor(a);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(a.Timeout ?? handler.DefaultTimeout);
            var result = await handler.ExecuteAsync(d, a, timeout.Token);
            p?.Report(result);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            var result = new WorkspaceActionResult(a.Id, WorkspaceActionOutcome.Failed, "Workspace.Action.Timeout");
            p?.Report(result);
            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            var result = new WorkspaceActionResult(a.Id, WorkspaceActionOutcome.Failed, "Workspace.Action.Failed", BoundedDiagnostic(ex.Message));
            p?.Report(result);
            return result;
        }
        catch (Exception ex)
        {
            var result = new WorkspaceActionResult(a.Id, WorkspaceActionOutcome.Failed, "Workspace.Action.RuntimeFailed", BoundedDiagnostic(ex.Message));
            p?.Report(result);
            return result;
        }
    }
    private WorkspaceDefinition Parse(string content) { if (string.IsNullOrWhiteSpace(content) || content.Length > 512 * 1024) throw new ArgumentException("Workspace import is invalid."); try { var result = Normalize(JsonSerializer.Deserialize<WorkspaceDefinition>(content, _json) ?? throw new JsonException()); WorkspaceValidation.ValidateDefinition(result); return result; } catch (JsonException ex) { throw new ArgumentException("Workspace import schema is invalid.", ex); } }
    private async Task<(List<WorkspaceDefinition> Value, long Revision)> ReadAsync(CancellationToken ct) { var r = await _store.ReadAsync(ct); if (r.Error == StoreErrorKind.NotFound) return ([], 0); if (!r.Succeeded || r.Value is null) throw new InvalidOperationException(r.Message); try { for (var index = 0; index < r.Value.Value.Count; index++) r.Value.Value[index] = Normalize(r.Value.Value[index]); foreach (var definition in r.Value.Value) WorkspaceValidation.ValidateDefinition(definition); } catch (ArgumentException ex) { throw new InvalidOperationException("Workspace store contains an invalid schema-1 definition.", ex); } return (r.Value.Value, r.Value.Revision); }
    private static WorkspaceDefinition Normalize(WorkspaceDefinition definition) => definition with { PreflightChecks = definition.PreflightChecks ?? [], ActionGroups = definition.ActionGroups ?? [], ClosePolicy = definition.ClosePolicy ?? new WorkspaceClosePolicy() };
    private async Task WriteAsync(List<WorkspaceDefinition> v, long revision, CancellationToken ct) { var r = await _store.WriteAsync(v, revision, ct); if (!r.Succeeded) throw new InvalidOperationException(r.Message); }
    private IWorkspaceActionHandler HandlerFor(WorkspaceAction action)
    {
        WorkspaceValidation.ValidateAction(action);
        if (_handlers.Count == 0) return new WorkspaceActionHandler(action.Type, _runtime, _runtime as IWorkspaceActionCapabilityGate ?? new FailClosedWorkspaceActionCapabilityGate());
        return _handlers.TryGetValue(action.Type, out var handler) ? handler : throw new InvalidOperationException($"No handler is registered for workspace action type '{action.Type}'.");
    }
    private async Task EnsureCapabilityForPreviewAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken ct)
    {
        if (_handlers.TryGetValue(action.Type, out var handler) && handler is WorkspaceActionHandler typed)
        {
            // Preview must probe the exact capability gate but never invoke the runtime.
            await typed.EnsureAvailableAsync(definition, ct);
            return;
        }
        if (_runtime is IWorkspaceActionCapabilityGate gate) await gate.EnsureAvailableAsync(definition, action.Type, ct);
        else throw new InvalidOperationException($"Workspace.Capability.{action.Type}.GateUnavailable");
    }
    private string PreviewAction(WorkspaceDefinition definition, WorkspaceAction action) => string.Join("; ", HandlerFor(action).Preview(definition, action).Commands);
    private static string? BoundedDiagnostic(string message) => string.IsNullOrWhiteSpace(message) ? null : message.Length > 512 ? message[..512] : message;
    private static bool RequiresTrust(WorkspaceAction action) => action.RequiresTrust || action.Type is WorkspaceActionType.LinuxCommand or WorkspaceActionType.ShellScript;
    private static string Token(Guid id, long revision, string purpose) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{id}:{revision}:{Guid.NewGuid()}")));
    private string Digest(WorkspaceDefinition definition) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(definition, _json))));
}
