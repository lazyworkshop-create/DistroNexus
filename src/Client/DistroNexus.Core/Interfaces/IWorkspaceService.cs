using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceDefinition> SaveAsync(WorkspaceDefinition definition, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewSaveAsync(WorkspaceDefinition definition, long expectedRevision, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewRemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDefinition> DuplicateAsync(Guid id, string displayName, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewDuplicateAsync(Guid id, string displayName, long expectedRevision, CancellationToken cancellationToken = default);
    /// <summary>Exports the workspace snapshot only when its current entity revision matches <paramref name="expectedRevision"/>.</summary>
    Task<string> ExportAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewExportDryRunAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceImportPreview> PreviewImportAsync(string content, CancellationToken cancellationToken = default);
    Task<WorkspaceDefinition> ImportAsync(string content, string importToken, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewImportDryRunAsync(string content, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDefinition> ApproveTrustAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewApproveTrustAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<WorkspaceLaunchPreview> PreviewLaunchAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewLaunchDryRunAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkspaceLaunchPreview> PreviewRetryAsync(Guid id, Guid actionId, CancellationToken cancellationToken = default);
    Task<WorkspaceActionResult> RetryAsync(Guid id, Guid actionId, long revision, string retryToken, CancellationToken cancellationToken = default);
    Task<WorkspaceDryRunResult> PreviewRetryDryRunAsync(Guid id, Guid actionId, long revision, CancellationToken cancellationToken = default);
    Task<WorkspaceLaunchResult> LaunchAsync(Guid id, long revision, string launchToken, IProgress<WorkspaceActionResult>? progress = null, CancellationToken cancellationToken = default);
    Task<WorkspaceActionResult> CloseAsync(Guid id, long revision, CancellationToken cancellationToken = default);
    Task<WorkspaceLaunchPreview> PreviewCloseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkspaceActionResult> CloseAsync(Guid id, long revision, string closeToken, CancellationToken cancellationToken = default);
    string BuildShortcutArguments(Guid id);
}

public interface IWorkspaceRuntime
{
    Task<bool> InstanceExistsAsync(string instanceName, CancellationToken cancellationToken);
    Task<WorkspacePreflightResult> CheckAsync(WorkspaceDefinition definition, WorkspacePreflightCheck check, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> CloseAsync(WorkspaceDefinition definition, CancellationToken cancellationToken);
}

public interface IWorkspaceDecisionProvider { Task<bool> ContinueAfterFailureAsync(WorkspaceDefinition definition, WorkspaceAction action, WorkspaceActionResult result, CancellationToken cancellationToken); }

public interface IWorkspaceActionHandler
{
    WorkspaceActionType Type { get; }
    string CapabilityKey { get; }
    bool SupportsRollback { get; }
    TimeSpan DefaultTimeout { get; }
    void Validate(WorkspaceAction action);
    WorkspaceLaunchPreview Preview(WorkspaceDefinition definition, WorkspaceAction action);
    Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken cancellationToken);
}

/// <summary>Authoritative capability gate for workspace actions. Implementations must not launch a process while probing.</summary>
public interface IWorkspaceActionCapabilityGate
{
    Task EnsureAvailableAsync(WorkspaceDefinition definition, WorkspaceActionType actionType, CancellationToken cancellationToken);
}

/// <summary>Checks a supported template prerequisite without exposing template execution to workspace definitions.</summary>
public interface IWorkspaceTemplatePrerequisiteChecker
{
    Task<WorkspaceTemplatePrerequisiteResult> CheckAsync(WorkspaceDefinition definition, string templateIdentifier, CancellationToken cancellationToken);
}
