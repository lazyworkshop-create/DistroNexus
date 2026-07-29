namespace DistroNexus.Desktop.Services;

/// <summary>Transient, parsed startup intent. It is deliberately data-only and never launches a workspace.</summary>
public sealed class WorkspaceStartupRequest
{
    public Guid? WorkspaceId { get; set; }
}
