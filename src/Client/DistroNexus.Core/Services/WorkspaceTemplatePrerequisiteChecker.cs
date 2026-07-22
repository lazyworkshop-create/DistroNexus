using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Safe default when no template catalog integration is configured.</summary>
public sealed class UnavailableWorkspaceTemplatePrerequisiteChecker : IWorkspaceTemplatePrerequisiteChecker
{
    public Task<WorkspaceTemplatePrerequisiteResult> CheckAsync(WorkspaceDefinition definition, string templateIdentifier, CancellationToken cancellationToken) =>
        Task.FromResult(new WorkspaceTemplatePrerequisiteResult(false, false, "Workspace.Preflight.TemplateUnavailable", "Template prerequisite checking is not configured."));
}
