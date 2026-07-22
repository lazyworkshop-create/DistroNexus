using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>
/// Typed boundary between persisted workspace actions and the runtime adapter.
/// Every supported action type is registered explicitly; no action is selected from
/// user supplied executable text.
/// </summary>
public sealed class WorkspaceActionHandler : IWorkspaceActionHandler
{
    private readonly IWorkspaceRuntime _runtime;
    private readonly IWorkspaceActionCapabilityGate _capabilities;

    public WorkspaceActionHandler(WorkspaceActionType type, IWorkspaceRuntime runtime, IWorkspaceActionCapabilityGate capabilities)
    {
        Type = type;
        _runtime = runtime;
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    public WorkspaceActionType Type { get; }
    public string CapabilityKey => $"Workspace.{Type}";
    public bool SupportsRollback => false;
    public TimeSpan DefaultTimeout => Type switch
    {
        WorkspaceActionType.Terminal or WorkspaceActionType.VisualStudioCode or WorkspaceActionType.Explorer or WorkspaceActionType.Browser => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMinutes(2)
    };

    public void Validate(WorkspaceAction action)
    {
        if (action.Type != Type)
            throw new ArgumentException("Workspace action handler type mismatch.", nameof(action));
        WorkspaceValidation.ValidateAction(action);
    }

    public WorkspaceLaunchPreview Preview(WorkspaceDefinition definition, WorkspaceAction action)
    {
        Validate(action);
        return new WorkspaceLaunchPreview(
            definition.Id,
            definition.Revision,
            ["Uses structured process arguments."],
            [$"{CapabilityKey}: {action.Type}: {string.Join(' ', action.Arguments)}"],
            [SupportsRollback ? "Rollback is supported." : "This action has no automatic rollback."],
            action.RequiresTrust || action.Type is WorkspaceActionType.LinuxCommand or WorkspaceActionType.ShellScript,
            string.Empty);
    }

    public async Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken cancellationToken)
    {
        Validate(action);
        await _capabilities.EnsureAvailableAsync(definition, Type, cancellationToken);
        return await _runtime.ExecuteAsync(definition, action with { Timeout = action.Timeout ?? DefaultTimeout }, cancellationToken);
    }

    public Task EnsureAvailableAsync(WorkspaceDefinition definition, CancellationToken cancellationToken)
        => _capabilities.EnsureAvailableAsync(definition, Type, cancellationToken);
}
