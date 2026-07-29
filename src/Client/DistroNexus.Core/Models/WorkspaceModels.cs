using System.Text.RegularExpressions;

namespace DistroNexus.Core.Models;

public enum WorkspaceTrustState { Trusted, Untrusted }
public enum WorkspaceActionType { Terminal, VisualStudioCode, Explorer, Browser, LinuxCommand, ShellScript, Systemd, DockerCompose, PodmanCompose }
public enum WorkspaceFailurePolicy { Stop, Continue, Ask }
public enum WorkspaceCloseMode { None, StopSelectedServices, StopInstance }
public enum WorkspaceMissingInstanceRemediation { BlockWithGuidance, PromptForInstallation }
public enum WorkspaceActionOutcome { Succeeded, Failed, Cancelled, Skipped }

public sealed record WorkspaceDefinition(
    Guid Id, string DisplayName, string InstanceName, string? ProjectPath,
    IReadOnlyList<WorkspacePreflightCheck> PreflightChecks,
    IReadOnlyList<WorkspaceActionGroup> ActionGroups,
    WorkspaceClosePolicy ClosePolicy, WorkspaceTrustState TrustState,
    long Revision = 0, DateTimeOffset? TrustedAt = null, WorkspaceMissingInstanceRemediation MissingInstanceRemediation = WorkspaceMissingInstanceRemediation.BlockWithGuidance);

public sealed record WorkspacePreflightCheck(string Kind, string Value, bool Required = true);
public sealed record WorkspaceActionGroup(Guid Id, string Name, bool AllowParallel, IReadOnlyList<WorkspaceAction> Actions);
public sealed record WorkspaceAction(Guid Id, WorkspaceActionType Type, string Name, IReadOnlyList<string> Arguments,
    WorkspaceFailurePolicy FailurePolicy = WorkspaceFailurePolicy.Stop, TimeSpan? Timeout = null, bool RequiresTrust = false,
    IReadOnlyList<Guid>? DependsOn = null, bool SafeForParallel = false);
public sealed record WorkspaceClosePolicy(WorkspaceCloseMode Mode = WorkspaceCloseMode.None, IReadOnlyList<string>? ServiceNames = null);
public sealed record WorkspaceImportPreview(WorkspaceDefinition Definition, IReadOnlyList<string> Commands, IReadOnlyList<string> Warnings, string ImportToken);
public sealed record WorkspaceLaunchPreview(Guid WorkspaceId, long Revision, IReadOnlyList<string> Effects, IReadOnlyList<string> Commands,
    IReadOnlyList<string> Preconditions, bool RequiresTrust, string LaunchToken,
    IReadOnlyList<WorkspaceActionResult>? ActionResults = null,
    IReadOnlyList<WorkspacePreflightResult>? PreflightResults = null,
    bool InstanceAvailable = true);
public sealed record WorkspaceActionResult(Guid ActionId, WorkspaceActionOutcome Outcome, string Code, string? Detail = null);
public sealed record WorkspaceOperationPreview(string PreviewToken, Guid? WorkspaceId, long? Revision, IReadOnlyList<string> Effects);
public sealed record WorkspaceExportResult(string Content);
public sealed record WorkspaceOperationStarted(string OperationId);
public sealed record WorkspaceOperationStatus(IReadOnlyList<WorkspaceActionResult> Progress, bool IsTerminal, WorkspaceLaunchResult? Result = null);
public sealed record WorkspaceLaunchResult(Guid WorkspaceId, IReadOnlyList<WorkspaceActionResult> Actions, bool Cancelled)
{ public bool Succeeded => !Cancelled && Actions.All(x => x.Outcome is WorkspaceActionOutcome.Succeeded or WorkspaceActionOutcome.Skipped); }
public sealed record WorkspacePreflightResult(string Kind, string Value, bool Succeeded, string Code, string? Detail = null);
public sealed record WorkspaceTemplatePrerequisiteResult(bool IsAvailable, bool Succeeded, string Code, string? Detail = null);

/// <summary>
/// A non-executable result returned by the workspace dry-run boundary.  It never
/// carries a confirmation token, so it cannot be replayed as an operation.
/// </summary>
public sealed record WorkspaceDryRunResult(
    string Operation,
    Guid? WorkspaceId,
    long? Revision,
    bool SchemaValid,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<WorkspaceActionResult> ActionResults,
    IReadOnlyList<WorkspacePreflightResult> PreflightResults);

public static class WorkspaceValidation
{
    private static readonly Regex Instance = new("^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant);
    private static readonly Regex Executable = new("^[A-Za-z0-9_+./-]+$", RegexOptions.CultureInvariant);
    private static readonly Regex SystemdUnitName = new("^[A-Za-z0-9@_.-]+\\.service$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SupportedTools = new(StringComparer.Ordinal) { "bash", "code", "docker", "dotnet", "git", "node", "podman", "python3" };
    private static readonly HashSet<string> SystemdOperations = new(StringComparer.Ordinal) { "start", "restart", "stop" };
    public static void ValidateDefinition(WorkspaceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id == Guid.Empty || string.IsNullOrWhiteSpace(definition.DisplayName) || definition.DisplayName.Length > 120) throw new ArgumentException("Workspace identity is invalid.");
        if (!Enum.IsDefined(definition.TrustState) || !Enum.IsDefined(definition.MissingInstanceRemediation)) throw new ArgumentException("Workspace enum value is invalid.");
        if (!Instance.IsMatch(definition.InstanceName)) throw new ArgumentException("Workspace instance name is invalid.");
        if (definition.PreflightChecks is null || definition.ActionGroups is null || definition.ClosePolicy is null) throw new ArgumentException("Workspace definition shape is invalid.");
        if (definition.ProjectPath is { } path) ValidateLinuxPath(path);
        ValidateClosePolicy(definition.ClosePolicy);
        foreach (var check in definition.PreflightChecks) ValidatePreflight(check);
        var ids = new HashSet<Guid>();
        var actionIds = new HashSet<Guid>();
        foreach (var group in definition.ActionGroups)
        {
            if (group is null || group.Actions is null || group.Id == Guid.Empty || !ids.Add(group.Id) || group.Actions.Count == 0) throw new ArgumentException("Workspace action group is invalid.");
            var groupActionIds = new HashSet<Guid>();
            foreach (var action in group.Actions) { if (action.Id == Guid.Empty || !actionIds.Add(action.Id) || !groupActionIds.Add(action.Id)) throw new ArgumentException("Workspace action id is invalid."); ValidateAction(action); }
            foreach (var action in group.Actions)
                if ((action.DependsOn?.Any(id => id == action.Id || !groupActionIds.Contains(id)) ?? false) || (group.AllowParallel && (!action.SafeForParallel || (action.DependsOn?.Count ?? 0) != 0))) throw new ArgumentException("Workspace action dependencies are invalid for this group.");
        }
    }
    public static void ValidateAction(WorkspaceAction action)
    {
        if (action is null || !Enum.IsDefined(action.Type) || !Enum.IsDefined(action.FailurePolicy) || action.Arguments is null || string.IsNullOrWhiteSpace(action.Name) || action.Name.Length > 120 || action.Arguments.Any(x => x is null || x.IndexOfAny(['\0', '\r', '\n']) >= 0)) throw new ArgumentException("Workspace action arguments are invalid.");
        if (action.Timeout is { } timeout && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(30))) throw new ArgumentException("Workspace action timeout is invalid.");
        if (action.Type is WorkspaceActionType.LinuxCommand or WorkspaceActionType.ShellScript && action.Arguments.Count == 0) throw new ArgumentException("A command action requires content.");
        if (action.Type == WorkspaceActionType.LinuxCommand && !Executable.IsMatch(action.Arguments[0])) throw new ArgumentException("Linux executable is invalid.");
        if (action.Type == WorkspaceActionType.Browser && (!Uri.TryCreate(action.Arguments.SingleOrDefault(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || string.IsNullOrWhiteSpace(uri.Host))) throw new ArgumentException("Browser URL is invalid.");
        if (action.Type == WorkspaceActionType.Systemd && (action.Arguments.Count != 2 || !SystemdOperations.Contains(action.Arguments[0]) || !SystemdUnitName.IsMatch(action.Arguments[1]))) throw new ArgumentException("Systemd action is invalid.");
    }
    public static void ValidateLinuxPath(string value)
    {
        if (!value.StartsWith("/", StringComparison.Ordinal) || value.Contains("..", StringComparison.Ordinal) || value.IndexOfAny(['\0', '\r', '\n']) >= 0) throw new ArgumentException("Linux path is invalid.");
    }
    public static void ValidatePreflight(WorkspacePreflightCheck check)
    {
        if (check is null || string.IsNullOrWhiteSpace(check.Kind) || string.IsNullOrWhiteSpace(check.Value) || check.Value.IndexOfAny(['\0', '\r', '\n']) >= 0) throw new ArgumentException("Workspace preflight is invalid.");
        switch (check.Kind)
        {
            case "directory": ValidateLinuxPath(check.Value); break;
            case "tool": if (!SupportedTools.Contains(check.Value)) throw new ArgumentException("Workspace preflight tool is invalid."); break;
            case "service": if (!SystemdUnitName.IsMatch(check.Value)) throw new ArgumentException("Workspace preflight service is invalid."); break;
            case "port": if (!int.TryParse(check.Value, out var port) || port is < 1 or > 65535) throw new ArgumentException("Workspace preflight port is invalid."); break;
            case "template": if (!Regex.IsMatch(check.Value, "^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Workspace preflight template is invalid."); break;
            default: throw new ArgumentException("Workspace preflight kind is invalid.");
        }
    }
    private static void ValidateClosePolicy(WorkspaceClosePolicy close)
    {
        if (!Enum.IsDefined(close.Mode)) throw new ArgumentException("Workspace close policy is invalid.");
        var services = close.ServiceNames ?? [];
        if (close.Mode == WorkspaceCloseMode.StopSelectedServices && services.Count == 0) throw new ArgumentException("Workspace close policy is invalid.");
        if (close.Mode != WorkspaceCloseMode.StopSelectedServices && services.Count != 0) throw new ArgumentException("Workspace close policy is invalid.");
        if (services.Any(name => string.IsNullOrWhiteSpace(name) || !SystemdUnitName.IsMatch(name))) throw new ArgumentException("Workspace close service is invalid.");
    }
}
