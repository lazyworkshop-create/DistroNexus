namespace DistroNexus.Desktop.Services;

/// <summary>Accepts only the stable workspace GUID form; execution remains subject to Workspace preview/trust confirmation.</summary>
public static class WorkspaceStartupRoute
{
    public static bool TryParse(string[]? args, out Guid workspaceId)
    {
        workspaceId = Guid.Empty;
        return args is { Length: 2 } && string.Equals(args[0], "--workspace", StringComparison.Ordinal) && Guid.TryParseExact(args[1], "D", out workspaceId);
    }
}
