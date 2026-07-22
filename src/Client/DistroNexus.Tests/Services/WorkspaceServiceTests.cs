using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using System.Text.Json;

namespace DistroNexus.Tests.Services;

public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus-workspaces-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task ImportedCommands_RemainUntrustedUntilExplicitApproval()
    {
        var runtime = new FakeRuntime(); var service = new WorkspaceService(runtime, _root);
        var json = await ExportDefinitionAsync(service, CommandWorkspace());
        var preview = await service.PreviewImportAsync(json);
        Assert.Equal(WorkspaceTrustState.Untrusted, preview.Definition.TrustState);
        Assert.Single(preview.Commands);
        var imported = await service.ImportAsync(json, preview.ImportToken, 0);
        var launch = await service.PreviewLaunchAsync(imported.Id);
        Assert.True(launch.RequiresTrust);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(imported.Id, imported.Revision, launch.LaunchToken));
        var trusted = await service.ApproveTrustAsync(imported.Id, imported.Revision);
        launch = await service.PreviewLaunchAsync(trusted.Id);
        var result = await service.LaunchAsync(trusted.Id, trusted.Revision, launch.LaunchToken);
        Assert.True(result.Succeeded); Assert.Equal(1, runtime.Executed);
    }
    [Fact]
    public async Task ImportToken_RejectsContentChangedAfterPreview()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root); var json = await ExportDefinitionAsync(service, TerminalWorkspace());
        var preview = await service.PreviewImportAsync(json);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(json.Replace("demo", "changed", StringComparison.Ordinal), preview.ImportToken, 0));
    }

    [Fact]
    public async Task Import_RequiresZeroCreationRevisionAndRejectsStalePreview()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root);
        var json = await ExportDefinitionAsync(service, TerminalWorkspace());
        var preview = await service.PreviewImportAsync(json);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(json, preview.ImportToken, 1));
        await service.SaveAsync((await service.ListAsync()).Single() with { DisplayName = "changed" }, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(json, preview.ImportToken, 0));
    }

    [Fact]
    public async Task Duplicate_RequiresTheAtomicallySelectedSourceRevision()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root);
        var source = await service.SaveAsync(TerminalWorkspace(), 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DuplicateAsync(source.Id, "copy", 0));
        var duplicate = await service.DuplicateAsync(source.Id, "copy", source.Revision);
        Assert.Equal(1, duplicate.Revision);
        var changed = await service.SaveAsync(source with { DisplayName = "changed" }, source.Revision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DuplicateAsync(source.Id, "stale", source.Revision));
        Assert.Equal(2, changed.Revision);
    }

    [Fact]
    public async Task Export_RequiresTheAtomicallySelectedWorkspaceRevision()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root);
        var saved = await service.SaveAsync(TerminalWorkspace(), 0);
        var changed = await service.SaveAsync(saved with { DisplayName = "changed" }, saved.Revision);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(saved.Id, saved.Revision));
        var json = await service.ExportAsync(changed.Id, changed.Revision);

        Assert.Equal("changed", JsonDocument.Parse(json).RootElement.GetProperty("DisplayName").GetString());
    }

    [Fact]
    public async Task DryRunMutators_ValidateWithoutStateChangesOrExecutableTokens()
    {
        var runtime = new FakeRuntime(); var service = new WorkspaceService(runtime, _root);
        var saved = await service.SaveAsync(TerminalWorkspace(), 0);
        var json = await service.ExportAsync(saved.Id, saved.Revision);
        var action = saved.ActionGroups.Single().Actions.Single();

        var previews = new[]
        {
            await service.PreviewSaveAsync(saved with { DisplayName = "changed" }, saved.Revision),
            await service.PreviewDuplicateAsync(saved.Id, "copy", saved.Revision),
            await service.PreviewExportDryRunAsync(saved.Id, saved.Revision),
            await service.PreviewRemoveAsync(saved.Id, saved.Revision),
            await service.PreviewImportDryRunAsync(json, 0),
            await service.PreviewApproveTrustAsync(saved.Id, saved.Revision),
            await service.PreviewLaunchDryRunAsync(saved.Id),
            await service.PreviewRetryDryRunAsync(saved.Id, action.Id, saved.Revision)
        };

        Assert.All(previews, preview =>
        {
            Assert.True(preview.SchemaValid);
            Assert.NotEmpty(preview.Preconditions);
            var json = JsonSerializer.Serialize(preview);
            Assert.DoesNotContain("LaunchToken", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ImportToken", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RetryToken", json, StringComparison.OrdinalIgnoreCase);
        });
        var remaining = Assert.Single(await service.ListAsync());
        Assert.Equal(saved.Id, remaining.Id);
        Assert.Equal(saved.Revision, remaining.Revision);
        Assert.Equal(0, runtime.Executed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewExportDryRunAsync(saved.Id, saved.Revision + 1));
    }

    [Fact]
    public async Task Edit_DoesNotSilentlyTrustImportedWorkspace()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root);
        var local = await service.SaveAsync(CommandWorkspace() with { TrustState = WorkspaceTrustState.Untrusted }, 0);
        var edited = await service.SaveAsync(local with { DisplayName = "changed", TrustState = WorkspaceTrustState.Trusted }, local.Revision);
        Assert.Equal(WorkspaceTrustState.Untrusted, edited.TrustState);
    }

    [Fact]
    public async Task LaunchToken_BindsRevisionAndIsSingleUse()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root);
        var saved = await service.SaveAsync(TerminalWorkspace(), 0);
        var preview = await service.PreviewLaunchAsync(saved.Id);
        var changed = await service.SaveAsync(saved with { DisplayName = "changed" }, saved.Revision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(saved.Id, saved.Revision, preview.LaunchToken));
        preview = await service.PreviewLaunchAsync(changed.Id);
        await service.LaunchAsync(changed.Id, changed.Revision, preview.LaunchToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(changed.Id, changed.Revision, preview.LaunchToken));
    }

    [Fact]
    public void Validation_RejectsInjectionUrlTraversalAndNewline()
    {
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateLinuxPath("../etc"));
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateAction(new(Guid.NewGuid(), WorkspaceActionType.Browser, "x", ["file:///x"])));
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateAction(new(Guid.NewGuid(), WorkspaceActionType.LinuxCommand, "x", ["echo\nwhoami"])));
    }
    [Fact]
    public void BrowserAction_AcceptsOnlyHttpsUrl()
    {
        WorkspaceValidation.ValidateAction(new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Browser, "browser", ["https://example.test/path"]));
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateAction(new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Browser, "browser", ["http://example.test"])));
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateAction(new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Browser, "browser", ["https://example.test\r\ncmd"])));
    }

    [Fact]
    public async Task Shortcut_ContainsOnlyStableGuidInvocation()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root); var id = Guid.NewGuid();
        Assert.Equal("--workspace " + id.ToString("D"), service.BuildShortcutArguments(id));
    }

    [Fact]
    public async Task RequiredPreflight_PreventsActionExecution()
    {
        var runtime = new FakeRuntime { PreflightSucceeds = false }; var service = new WorkspaceService(runtime, _root);
        var saved = await service.SaveAsync(TerminalWorkspace() with { PreflightChecks = [new("tool", "code", true)] }, 0);
        var preview = await service.PreviewLaunchAsync(saved.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(saved.Id, saved.Revision, preview.LaunchToken));
        Assert.Equal(0, runtime.Executed);
    }

    [Fact]
    public async Task PreviewLaunch_ReturnsStructuredNonMutatingFailuresForUnavailableCapabilityPathAndPort()
    {
        var action = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.VisualStudioCode, "code", []);
        var definition = TerminalWorkspace() with
        {
            PreflightChecks = [new("directory", "/missing", true), new("port", "8080", true)],
            ActionGroups = [new(Guid.NewGuid(), "launch", false, [action])]
        };
        var runtime = new FakeRuntime { PreflightSucceeds = false };
        var service = new WorkspaceService(runtime, _root, handlers: [new WorkspaceActionHandler(WorkspaceActionType.VisualStudioCode, runtime, new FailingGate())]);
        var saved = await service.SaveAsync(definition, 0);

        var preview = await service.PreviewLaunchAsync(saved.Id);

        Assert.True(preview.InstanceAvailable);
        Assert.Contains(preview.PreflightResults!, result => !result.Succeeded && result.Kind == "directory");
        Assert.Contains(preview.PreflightResults!, result => !result.Succeeded && result.Kind == "port");
        Assert.Contains(preview.ActionResults!, result => result.ActionId == action.Id && result.Code == "Workspace.Action.Unavailable");
        Assert.Contains(preview.ActionResults!, result => result.ActionId == action.Id && result.Code == "Workspace.Preflight.Failed");
        Assert.Empty(preview.LaunchToken);
        Assert.Equal(0, runtime.Executed);
    }

    [Fact]
    public async Task ParallelGroup_RejectsDependencyOrUnsafeAction()
    {
        var action = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "x", [], SafeForParallel: false);
        var invalid = TerminalWorkspace() with { ActionGroups = [new(Guid.NewGuid(), "parallel", true, [action])] };
        await Assert.ThrowsAsync<ArgumentException>(() => new WorkspaceService(new FakeRuntime(), _root).SaveAsync(invalid, 0));
    }
    [Fact]
    public void Validation_RejectsActionIdReusedAcrossGroups()
    {
        var id = Guid.NewGuid();
        var action = new WorkspaceAction(id, WorkspaceActionType.Terminal, "terminal", []);
        var definition = TerminalWorkspace() with { ActionGroups = [new(Guid.NewGuid(), "one", false, [action]), new(Guid.NewGuid(), "two", false, [action])] };
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateDefinition(definition));
    }
    [Fact]
    public void Validation_RejectsUnsafePreflightAndCloseServiceNames()
    {
        var invalidTool = TerminalWorkspace() with { PreflightChecks = [new("tool", "git;id", true)] };
        var invalidService = TerminalWorkspace() with { ClosePolicy = new(WorkspaceCloseMode.StopSelectedServices, ["bad service"]) };
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateDefinition(invalidTool));
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateDefinition(invalidService));
    }
    [Fact]
    public void Validation_RejectsUnknownEnumAndSystemdOperation()
    {
        var invalidEnum = TerminalWorkspace() with { TrustState = (WorkspaceTrustState)42 };
        var invalidSystemd = TerminalWorkspace() with { ActionGroups = [new(Guid.NewGuid(), "group", false, [new(Guid.NewGuid(), WorkspaceActionType.Systemd, "systemd", ["enable", "demo.service"])])] };
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateDefinition(invalidEnum));
        Assert.Throws<ArgumentException>(() => WorkspaceValidation.ValidateDefinition(invalidSystemd));
    }
    [Fact]
    public async Task SchemaOne_RejectsNumericEnums()
    {
        var fixture = $$"""{"id":"{{Guid.NewGuid()}}","displayName":"demo","instanceName":"Ubuntu","projectPath":"/home/demo","preflightChecks":[],"actionGroups":[],"closePolicy":{"mode":0,"serviceNames":[]},"trustState":0,"revision":1,"missingInstanceRemediation":0}""";
        var service = new WorkspaceService(new FakeRuntime(), _root);
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewImportAsync(fixture));
    }
    [Fact]
    public async Task WorkspaceStore_RejectsNumericEnumsWithoutChangingOtherStores()
    {
        Directory.CreateDirectory(_root);
        var payload = $$"""{"schemaVersion":1,"revision":1,"updatedAt":"2026-01-01T00:00:00Z","value":[{"id":"{{Guid.NewGuid()}}","displayName":"demo","instanceName":"Ubuntu","preflightChecks":[],"actionGroups":[],"closePolicy":{"mode":0,"serviceNames":[]},"trustState":0,"revision":1,"missingInstanceRemediation":0}]}""";
        await File.WriteAllTextAsync(Path.Combine(_root, "workspaces.json"), payload);
        var service = new WorkspaceService(new FakeRuntime(), _root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync());
    }
    [Fact]
    public async Task SchemaOne_PowerShellCompatibleFixtureImportsAndPreservesEnumContract()
    {
        var id = Guid.NewGuid();
        var fixture = $$"""{"id":"{{id}}","displayName":"demo","instanceName":"Ubuntu","projectPath":"/home/demo","preflightChecks":[],"actionGroups":[],"closePolicy":{"mode":"None","serviceNames":[]},"trustState":"Trusted","revision":1,"trustedAt":null,"missingInstanceRemediation":"BlockWithGuidance"}""";
        var service = new WorkspaceService(new FakeRuntime(), _root);

        var preview = await service.PreviewImportAsync(fixture);

        Assert.Equal(WorkspaceTrustState.Untrusted, preview.Definition.TrustState);
        Assert.Equal(WorkspaceMissingInstanceRemediation.BlockWithGuidance, preview.Definition.MissingInstanceRemediation);
    }
    [Fact]
    public async Task SchemaOne_MissingOptionalShapeIsRecoveredWithControlledDefaults()
    {
        var fixture = $$"""{"id":"{{Guid.NewGuid()}}","displayName":"demo","instanceName":"Ubuntu"}""";
        var service = new WorkspaceService(new FakeRuntime(), _root);
        var preview = await service.PreviewImportAsync(fixture);
        Assert.Empty(preview.Definition.ActionGroups);
        Assert.Empty(preview.Definition.PreflightChecks);
    }
    [Fact]
    public async Task RetryToken_IsBoundToActionRevisionAndSingleUse()
    { var runtime=new FakeRuntime(); var service=new WorkspaceService(runtime,_root); var saved=await service.SaveAsync(TerminalWorkspace(),0); var action=saved.ActionGroups[0].Actions[0]; var preview=await service.PreviewRetryAsync(saved.Id,action.Id); await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RetryAsync(saved.Id,Guid.NewGuid(),preview.Revision,preview.LaunchToken)); await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RetryAsync(saved.Id,action.Id,preview.Revision,preview.LaunchToken)); preview=await service.PreviewRetryAsync(saved.Id,action.Id); await service.SaveAsync(saved with { DisplayName="changed"},saved.Revision); await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RetryAsync(saved.Id,action.Id,preview.Revision,preview.LaunchToken)); Assert.Equal(0,runtime.Executed); }

    [Fact]
    public async Task Launch_ResolvesRegisteredTypedHandler_ForPreviewAndExecution()
    {
        var runtime = new FakeRuntime(); var handler = new RecordingHandler();
        var service = new WorkspaceService(runtime, _root, handlers: [handler]);
        var saved = await service.SaveAsync(TerminalWorkspace(), 0);
        var preview = await service.PreviewLaunchAsync(saved.Id);
        Assert.Contains("typed-preview", preview.Commands.Single());
        await service.LaunchAsync(saved.Id, saved.Revision, preview.LaunchToken);
        Assert.Equal(1, handler.Executed);
        Assert.Equal(0, runtime.Executed);
    }

    [Fact]
    public async Task EveryActionHandler_EnforcesCapabilityGateBeforeRuntimeExecution()
    {
        var runtime = new FakeRuntime(); var gate = new RecordingGate(); var definition = TerminalWorkspace();
        foreach (var type in Enum.GetValues<WorkspaceActionType>())
        {
            var handler = new WorkspaceActionHandler(type, runtime, gate);
            await handler.ExecuteAsync(definition, ActionFor(type), CancellationToken.None);
        }
        Assert.Equal(9, gate.Checked.Count);
        Assert.Equal(9, runtime.Executed);
    }
    [Fact]
    public async Task UnregisteredHandlerFallback_FailsClosedWithCapabilityDiagnostic()
    {
        var runtime = new UngatedRuntime(); var service = new WorkspaceService(runtime, _root); var saved = await service.SaveAsync(TerminalWorkspace(), 0); var preview = await service.PreviewLaunchAsync(saved.Id);
        var result = await service.LaunchAsync(saved.Id, saved.Revision, preview.LaunchToken);
        Assert.Equal(WorkspaceActionOutcome.Failed, Assert.Single(result.Actions).Outcome);
        Assert.Contains("Workspace.Capability.Terminal.GateUnavailable", result.Actions[0].Detail);
        Assert.Equal(0, runtime.Executed);
    }
    [Fact]
    public async Task VisualStudioCodeCapabilityFailure_ReturnsActionIdReportsProgressAndSupportsRetry()
    {
        var action = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.VisualStudioCode, "code", []);
        var definition = TerminalWorkspace() with { ActionGroups = [new(Guid.NewGuid(), "launch", false, [action])] };
        var service = new WorkspaceService(new FakeRuntime(), _root, handlers: [new ThrowingHandler()]);
        var saved = await service.SaveAsync(definition, 0);
        var preview = await service.PreviewLaunchAsync(saved.Id);
        var progress = new List<WorkspaceActionResult>();
        var result = await service.LaunchAsync(saved.Id, saved.Revision, preview.LaunchToken, new InlineProgress(progress));

        var failed = Assert.Single(result.Actions);
        Assert.Equal(action.Id, failed.ActionId);
        Assert.Equal(WorkspaceActionOutcome.Failed, failed.Outcome);
        Assert.Equal(action.Id, Assert.Single(progress).ActionId);
        var retry = await service.PreviewRetryAsync(saved.Id, action.Id);
        var retryResult = await service.RetryAsync(saved.Id, action.Id, retry.Revision, retry.LaunchToken);
        Assert.Equal(action.Id, retryResult.ActionId);
        Assert.Equal(WorkspaceActionOutcome.Failed, retryResult.Outcome);
    }
    [Theory]
    [InlineData(false, "Workspace.Action.RuntimeFailed")]
    [InlineData(true, "Workspace.Action.Timeout")]
    public async Task RuntimeAndLinkedTimeoutFailures_AreActionScopedAndReported(bool timeout, string code)
    {
        var action = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.VisualStudioCode, "code", []);
        var definition = TerminalWorkspace() with { ActionGroups = [new(Guid.NewGuid(), "launch", false, [action])] };
        var service = new WorkspaceService(new FakeRuntime(), _root, handlers: [new RuntimeFailureHandler(timeout)]);
        var saved = await service.SaveAsync(definition, 0); var preview = await service.PreviewLaunchAsync(saved.Id); var progress = new List<WorkspaceActionResult>();
        var result = await service.LaunchAsync(saved.Id, saved.Revision, preview.LaunchToken, new InlineProgress(progress));
        Assert.Equal(code, Assert.Single(result.Actions).Code);
        Assert.Equal(action.Id, result.Actions[0].ActionId);
        Assert.Equal(action.Id, Assert.Single(progress).ActionId);
    }
    [Fact]
    public async Task CloseWithoutPreviewToken_IsRejected()
    {
        var service = new WorkspaceService(new FakeRuntime(), _root); var saved = await service.SaveAsync(TerminalWorkspace(), 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseAsync(saved.Id, saved.Revision));
    }

    private async Task<string> ExportDefinitionAsync(WorkspaceService service, WorkspaceDefinition definition)
    { var saved = await service.SaveAsync(definition, 0); return await service.ExportAsync(saved.Id, saved.Revision); }
    private static WorkspaceDefinition TerminalWorkspace() => new(Guid.NewGuid(), "demo", "Ubuntu", "/home/demo", [], [new(Guid.NewGuid(), "start", false, [new(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])])], new(), WorkspaceTrustState.Trusted);
    private static WorkspaceDefinition CommandWorkspace() => TerminalWorkspace() with { ActionGroups = [new(Guid.NewGuid(), "start", false, [new(Guid.NewGuid(), WorkspaceActionType.LinuxCommand, "command", ["echo", "hello"], RequiresTrust: true)])] };
    private sealed class FakeRuntime : IWorkspaceRuntime, IWorkspaceActionCapabilityGate
    { public int Executed { get; private set; } public bool PreflightSucceeds { get; init; } = true; public Task<bool> InstanceExistsAsync(string _, CancellationToken __) => Task.FromResult(true); public Task<WorkspacePreflightResult> CheckAsync(WorkspaceDefinition _, WorkspacePreflightCheck check, CancellationToken __) => Task.FromResult(new WorkspacePreflightResult(check.Kind, check.Value, PreflightSucceeds, "fake")); public Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition _, WorkspaceAction action, CancellationToken __) { Executed++; return Task.FromResult(new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Succeeded, "ok")); } public Task<WorkspaceActionResult> CloseAsync(WorkspaceDefinition _, CancellationToken __) => Task.FromResult(new WorkspaceActionResult(Guid.Empty, WorkspaceActionOutcome.Succeeded, "ok")); public Task EnsureAvailableAsync(WorkspaceDefinition _, WorkspaceActionType __, CancellationToken ___) => Task.CompletedTask; }
    private sealed class RecordingHandler : IWorkspaceActionHandler
    {
        public WorkspaceActionType Type => WorkspaceActionType.Terminal;
        public string CapabilityKey => "test.terminal";
        public bool SupportsRollback => false;
        public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(1);
        public int Executed { get; private set; }
        public void Validate(WorkspaceAction action) => Assert.Equal(Type, action.Type);
        public WorkspaceLaunchPreview Preview(WorkspaceDefinition definition, WorkspaceAction action) => new(definition.Id, definition.Revision, [], ["typed-preview"], [], false, string.Empty);
        public Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken cancellationToken) { Executed++; return Task.FromResult(new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Succeeded, "typed")); }
    }
    private sealed class ThrowingHandler : IWorkspaceActionHandler
    {
        public WorkspaceActionType Type => WorkspaceActionType.VisualStudioCode;
        public string CapabilityKey => "vscode";
        public bool SupportsRollback => false;
        public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(1);
        public void Validate(WorkspaceAction action) { }
        public WorkspaceLaunchPreview Preview(WorkspaceDefinition definition, WorkspaceAction action) => new(definition.Id, definition.Revision, [], ["vscode"], [], false, string.Empty);
        public Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken cancellationToken) => throw new InvalidOperationException("Workspace.Capability.VisualStudioCode.Unavailable");
    }
    private sealed class RuntimeFailureHandler(bool timeout) : IWorkspaceActionHandler
    {
        public WorkspaceActionType Type => WorkspaceActionType.VisualStudioCode; public string CapabilityKey => "vscode"; public bool SupportsRollback => false; public TimeSpan DefaultTimeout => TimeSpan.FromMilliseconds(1);
        public void Validate(WorkspaceAction action) { }
        public WorkspaceLaunchPreview Preview(WorkspaceDefinition definition, WorkspaceAction action) => new(definition.Id, definition.Revision, [], ["vscode"], [], false, string.Empty);
        public Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken cancellationToken) => timeout ? Task.FromException<WorkspaceActionResult>(new OperationCanceledException()) : Task.FromException<WorkspaceActionResult>(new IOException("runtime unavailable"));
    }
    private sealed class InlineProgress(List<WorkspaceActionResult> values) : IProgress<WorkspaceActionResult> { public void Report(WorkspaceActionResult value) => values.Add(value); }
    private sealed class RecordingGate : IWorkspaceActionCapabilityGate
    { public List<WorkspaceActionType> Checked { get; } = []; public Task EnsureAvailableAsync(WorkspaceDefinition _, WorkspaceActionType actionType, CancellationToken __) { Checked.Add(actionType); return Task.CompletedTask; } }
    private static WorkspaceAction ActionFor(WorkspaceActionType type) => type switch
    {
        WorkspaceActionType.Browser => new(Guid.NewGuid(), type, "browser", ["https://example.test"]),
        WorkspaceActionType.LinuxCommand => new(Guid.NewGuid(), type, "command", ["echo"]),
        WorkspaceActionType.ShellScript => new(Guid.NewGuid(), type, "script", ["echo ok"]),
        WorkspaceActionType.Systemd => new(Guid.NewGuid(), type, "systemd", ["start", "demo.service"]),
        WorkspaceActionType.DockerCompose or WorkspaceActionType.PodmanCompose => new(Guid.NewGuid(), type, "compose", ["up"]),
        _ => new(Guid.NewGuid(), type, "open", [])
    };
    private sealed class FailingGate : IWorkspaceActionCapabilityGate
    { public Task EnsureAvailableAsync(WorkspaceDefinition _, WorkspaceActionType __, CancellationToken ___) => Task.FromException(new InvalidOperationException("Workspace.Capability.VisualStudioCode.Unavailable")); }
    private sealed class UngatedRuntime : IWorkspaceRuntime
    { public int Executed { get; private set; } public Task<bool> InstanceExistsAsync(string _, CancellationToken __) => Task.FromResult(true); public Task<WorkspacePreflightResult> CheckAsync(WorkspaceDefinition _, WorkspacePreflightCheck check, CancellationToken __) => Task.FromResult(new WorkspacePreflightResult(check.Kind, check.Value, true, "ok")); public Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition _, WorkspaceAction action, CancellationToken __) { Executed++; return Task.FromResult(new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Succeeded, "ok")); } public Task<WorkspaceActionResult> CloseAsync(WorkspaceDefinition _, CancellationToken __) => Task.FromResult(new WorkspaceActionResult(Guid.Empty, WorkspaceActionOutcome.Succeeded, "ok")); }
}
