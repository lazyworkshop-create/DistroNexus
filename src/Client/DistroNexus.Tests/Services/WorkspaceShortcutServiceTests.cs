using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Tests.Services;

public sealed class WorkspaceShortcutServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus-shortcut-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task CreateAsync_ValidatesPersistedTargetAndWritesOnlyTheFixedShortcutArguments()
    {
        Directory.CreateDirectory(_root);
        var target = Path.Combine(_root, "DistroNexus.Desktop.exe");
        File.WriteAllText(target, string.Empty);
        var workspace = await new WorkspaceService(new FakeRuntime(), _root).SaveAsync(Definition(), 0);
        string? link = null; string? arguments = null;
        var result = await new WorkspaceShortcutService(new WorkspaceService(new FakeRuntime(), _root), () => target,
            (path, _, value) => (link, arguments) = (path, value), () => _root)
            .CreateAsync(new(workspace.Id));

        Assert.Equal("Workspace.ShortcutCreated", result.OutcomeCode);
        Assert.Equal(Path.Combine(_root, $"DistroNexus Workspace {workspace.Id:D}.lnk"), link);
        Assert.Equal($"--workspace {workspace.Id:D}", arguments);
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyAndUnknownWorkspaceTargetsWithoutWriting()
    {
        var writes = 0;
        var service = new WorkspaceShortcutService(new WorkspaceService(new FakeRuntime(), _root), () => null, (_, _, _) => writes++, () => _root);
        Assert.Equal("Workspace.ShortcutInvalid", (await service.CreateAsync(new(Guid.Empty))).OutcomeCode);
        Assert.Equal("Workspace.ShortcutNotFound", (await service.CreateAsync(new(Guid.NewGuid()))).OutcomeCode);
        Assert.Equal(0, writes);
    }

    private static WorkspaceDefinition Definition() => new(Guid.NewGuid(), "demo", "Ubuntu", "/home/demo", [], [new(Guid.NewGuid(), "start", false, [new(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])])], new(), WorkspaceTrustState.Trusted);
    private sealed class FakeRuntime : IWorkspaceRuntime
    {
        public Task<bool> InstanceExistsAsync(string _, CancellationToken __) => Task.FromResult(true);
        public Task<WorkspacePreflightResult> CheckAsync(WorkspaceDefinition _, WorkspacePreflightCheck check, CancellationToken __) => Task.FromResult(new WorkspacePreflightResult(check.Kind, check.Value, true, "ok"));
        public Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition _, WorkspaceAction action, CancellationToken __) => Task.FromResult(new WorkspaceActionResult(action.Id, WorkspaceActionOutcome.Succeeded, "ok"));
        public Task<WorkspaceActionResult> CloseAsync(WorkspaceDefinition _, CancellationToken __) => Task.FromResult(new WorkspaceActionResult(Guid.Empty, WorkspaceActionOutcome.Succeeded, "ok"));
    }
}
