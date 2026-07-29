using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace DistroNexus.Core.Services;

/// <summary>Maps a reviewed workspace action to an argument-list process request; it never builds a shell command from a workspace field.</summary>
public sealed class WorkspaceRuntime : IWorkspaceRuntime
{
    private readonly IWslManagerService _instances;
    private readonly IProcessRunner _processes;
    private readonly IWorkspaceTemplatePrerequisiteChecker _templates;
    private static readonly Regex TemplateIdentifier = new("^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);
    public WorkspaceRuntime(IWslManagerService instances, IProcessRunner processes, IWorkspaceTemplatePrerequisiteChecker? templates = null) => (_instances, _processes, _templates) = (instances, processes, templates ?? new UnavailableWorkspaceTemplatePrerequisiteChecker());
    public async Task<bool> InstanceExistsAsync(string instanceName, CancellationToken ct) => (await _instances.GetInstancesAsync(ct)).Any(x => x.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
    public async Task<WorkspacePreflightResult> CheckAsync(WorkspaceDefinition definition, WorkspacePreflightCheck check, CancellationToken ct)
    {
        if (check.Value.IndexOfAny(['\0', '\r', '\n']) >= 0) return new(check.Kind, check.Value, false, "Workspace.Preflight.Invalid");
        try { WorkspaceValidation.ValidatePreflight(check); } catch (ArgumentException) { return new(check.Kind, check.Value, false, check.Kind == "template" ? "Workspace.Preflight.TemplateInvalid" : "Workspace.Preflight.Invalid"); }
        return check.Kind switch
        {
            "directory" => CheckLinuxPath(check).Succeeded ? await ProbeAsync(definition, ["test", "-d", "--", check.Value], check, ct) : CheckLinuxPath(check),
            "tool" => await ProbeAsync(definition, ["test", "-x", ToolPath(check.Value)], check, ct),
            "service" => await ProbeAsync(definition, ["systemctl", "is-active", check.Value], check, ct),
            "port" => await CheckPortAsync(check, ct),
            "template" => await CheckTemplatePrerequisiteAsync(definition, check, ct),
            _ => new(check.Kind, check.Value, false, "Workspace.Preflight.Unsupported")
        };
    }
    public async Task<WorkspaceActionResult> CloseAsync(WorkspaceDefinition definition, CancellationToken ct)
    {
        if (definition.ClosePolicy.Mode == WorkspaceCloseMode.None) return new(Guid.Empty, WorkspaceActionOutcome.Skipped, "Workspace.Close.None");
        if (definition.ClosePolicy.Mode == WorkspaceCloseMode.StopInstance) { var ok = await _instances.StopInstanceAsync(definition.InstanceName, ct); return new(Guid.Empty, ok ? WorkspaceActionOutcome.Succeeded : WorkspaceActionOutcome.Failed, ok ? "Workspace.Close.InstanceStopped" : "Workspace.Close.Failed"); }
        var failures = new List<string>();
        foreach (var service in definition.ClosePolicy.ServiceNames ?? [])
        {
            var result = await _processes.RunAsync(new("wsl.exe", ["--distribution", definition.InstanceName, "--exec", "systemctl", "stop", service], TimeSpan.FromMinutes(1)), ct);
            if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None) failures.Add(service);
        }
        return failures.Count == 0 ? new(Guid.Empty, WorkspaceActionOutcome.Succeeded, "Workspace.Close.ServicesStopped") : new(Guid.Empty, WorkspaceActionOutcome.Failed, "Workspace.Close.ServicesFailed", string.Join(",", failures));
    }
    public async Task<WorkspaceActionResult> ExecuteAsync(WorkspaceDefinition definition, WorkspaceAction action, CancellationToken ct)
    {
        try
        {
            WorkspaceValidation.ValidateAction(action);
            var request = ToRequest(definition, action);
            var result = await _processes.RunAsync(request, ct);
            var outcome = result.Cancelled ? WorkspaceActionOutcome.Cancelled : result.TimedOut || result.ExitCode != 0 || result.Failure != ProcessFailureKind.None ? WorkspaceActionOutcome.Failed : WorkspaceActionOutcome.Succeeded;
            return new(action.Id, outcome, outcome == WorkspaceActionOutcome.Succeeded ? "Workspace.Action.Succeeded" : result.TimedOut ? "Workspace.Action.Timeout" : "Workspace.Action.Failed", outcome == WorkspaceActionOutcome.Succeeded ? null : Bounded(result.StandardError));
        }
        catch (OperationCanceledException) { return new(action.Id, WorkspaceActionOutcome.Cancelled, "Workspace.Action.Cancelled"); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return new(action.Id, WorkspaceActionOutcome.Failed, "Workspace.Action.Invalid", ex.Message); }
    }
    private static ProcessRequest ToRequest(WorkspaceDefinition d, WorkspaceAction a)
    {
        var timeout = a.Timeout ?? TimeSpan.FromMinutes(2);
        return a.Type switch
        {
            WorkspaceActionType.Terminal => new("wt.exe", ["new-tab", "--", "wsl.exe", "--distribution", d.InstanceName, "--cd", d.ProjectPath ?? "/"], timeout),
            WorkspaceActionType.VisualStudioCode => new("code.exe", ["--folder-uri", "vscode-remote://wsl+" + Uri.EscapeDataString(d.InstanceName) + (d.ProjectPath ?? "/")], timeout),
            WorkspaceActionType.Explorer => new("explorer.exe", ["\\\\wsl$\\" + d.InstanceName + (d.ProjectPath?.Replace('/', '\\') ?? "")], timeout),
            WorkspaceActionType.Browser => Browser(a, timeout),
            WorkspaceActionType.LinuxCommand => new("wsl.exe", ["--distribution", d.InstanceName, "--exec", .. a.Arguments], timeout),
            WorkspaceActionType.ShellScript => new("wsl.exe", ["--distribution", d.InstanceName, "--exec", "/bin/sh", "-lc", a.Arguments[0]], timeout),
            WorkspaceActionType.Systemd => new("wsl.exe", ["--distribution", d.InstanceName, "--exec", "systemctl", a.Arguments[0], a.Arguments[1]], timeout),
            WorkspaceActionType.DockerCompose => Compose(d, a, "docker", timeout),
            WorkspaceActionType.PodmanCompose => Compose(d, a, "podman", timeout),
            _ => throw new ArgumentOutOfRangeException(nameof(a))
        };
    }
    private static ProcessRequest Compose(WorkspaceDefinition d, WorkspaceAction a, string command, TimeSpan timeout)
    {
        if (a.Arguments.Count == 0) throw new ArgumentException("Compose action requires arguments.");
        return new("wsl.exe", ["--distribution", d.InstanceName, "--exec", command, "compose", .. a.Arguments], timeout, WorkingDirectory: null);
    }
    private static ProcessRequest Browser(WorkspaceAction action, TimeSpan timeout)
    {
        var url = action.Arguments.Single();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            throw new ArgumentException("Browser action requires an HTTPS URL.");
        // explorer.exe is a Windows-owned URL launcher; the URL remains a single validated argument.
        return new("explorer.exe", [uri.AbsoluteUri], timeout);
    }
    private static string ToolPath(string tool) => tool switch
    {
        "bash" => "/usr/bin/bash", "code" => "/usr/bin/code", "docker" => "/usr/bin/docker", "dotnet" => "/usr/bin/dotnet",
        "git" => "/usr/bin/git", "node" => "/usr/bin/node", "podman" => "/usr/bin/podman", "python3" => "/usr/bin/python3",
        _ => throw new ArgumentException("Workspace preflight tool is invalid.")
    };
    private static WorkspacePreflightResult CheckLinuxPath(WorkspacePreflightCheck check) { try { WorkspaceValidation.ValidateLinuxPath(check.Value); return new(check.Kind, check.Value, true, "Workspace.Preflight.Deferred"); } catch (ArgumentException) { return new(check.Kind, check.Value, false, "Workspace.Preflight.Invalid"); } }
    private async Task<WorkspacePreflightResult> CheckTemplatePrerequisiteAsync(WorkspaceDefinition definition, WorkspacePreflightCheck check, CancellationToken ct)
    {
        if (!TemplateIdentifier.IsMatch(check.Value))
            return new(check.Kind, check.Value, false, "Workspace.Preflight.TemplateInvalid");
        var result = await _templates.CheckAsync(definition, check.Value, ct);
        if (!result.IsAvailable)
            return new(check.Kind, check.Value, !check.Required, check.Required ? "Workspace.Preflight.TemplateUnavailable" : "Workspace.Preflight.TemplateUnavailableOptional", result.Detail);
        return new(check.Kind, check.Value, result.Succeeded, result.Succeeded ? "Workspace.Preflight.TemplateSatisfied" : "Workspace.Preflight.TemplateFailed", result.Detail ?? result.Code);
    }
    private static async Task<WorkspacePreflightResult> CheckPortAsync(WorkspacePreflightCheck check, CancellationToken ct)
    { if (!int.TryParse(check.Value, out var port) || port is < 1 or > 65535) return new(check.Kind, check.Value, false, "Workspace.Preflight.Invalid"); try { var listener = new TcpListener(IPAddress.Loopback, port); listener.Start(); listener.Stop(); await Task.CompletedTask; return new(check.Kind, check.Value, true, "Workspace.Preflight.PortAvailable"); } catch (SocketException ex) { return new(check.Kind, check.Value, false, "Workspace.Preflight.PortBusy", ex.SocketErrorCode.ToString()); } }
    private async Task<WorkspacePreflightResult> ProbeAsync(WorkspaceDefinition d, IReadOnlyList<string> args, WorkspacePreflightCheck check, CancellationToken ct) { var r = await _processes.RunAsync(new("wsl.exe", ["--distribution", d.InstanceName, "--exec", .. args], TimeSpan.FromSeconds(20)), ct); return new(check.Kind, check.Value, r.ExitCode == 0 && !r.TimedOut && !r.Cancelled, r.ExitCode == 0 ? "Workspace.Preflight.Succeeded" : "Workspace.Preflight.Failed", Bounded(r.StandardError)); }
    private static string? Bounded(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Length > 512 ? text[..512] : text;
}
