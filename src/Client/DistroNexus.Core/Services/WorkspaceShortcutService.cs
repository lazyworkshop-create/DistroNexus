using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Creates the fixed DistroNexus workspace shell link after validating its modeled target.</summary>
public sealed class WorkspaceShortcutService
{
    private readonly WorkspaceService _workspaces;
    private readonly Func<string?> _applicationTarget;
    private readonly Func<string?> _desktopDirectory;
    private readonly Action<string, string, string> _writeShortcut;

    public WorkspaceShortcutService(WorkspaceService workspaces, Func<string?>? applicationTarget = null,
        Action<string, string, string>? writeShortcut = null, Func<string?>? desktopDirectory = null)
    {
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _applicationTarget = applicationTarget ?? (() => Path.Combine(AppContext.BaseDirectory, "DistroNexus.Desktop.exe"));
        _writeShortcut = writeShortcut ?? WriteShortcut;
        _desktopDirectory = desktopDirectory ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
    }

    public async Task<WorkspaceShortcutResult> CreateAsync(WorkspaceShortcutRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.WorkspaceId == Guid.Empty)
            return new("Workspace.ShortcutInvalid");

        if (!(await _workspaces.ListAsync(cancellationToken)).Any(workspace => workspace.Id == request.WorkspaceId))
            return new("Workspace.ShortcutNotFound");

        var target = _applicationTarget();
        var desktop = _desktopDirectory();
        if (string.IsNullOrWhiteSpace(target) || !Path.IsPathFullyQualified(target) || !File.Exists(target) ||
            string.IsNullOrWhiteSpace(desktop) || !Path.IsPathFullyQualified(desktop) || !Directory.Exists(desktop))
            return new("Workspace.ShortcutUnavailable");

        try
        {
            var link = Path.Combine(desktop, $"DistroNexus Workspace {request.WorkspaceId:D}.lnk");
            _writeShortcut(link, target, $"--workspace {request.WorkspaceId:D}");
            return new("Workspace.ShortcutCreated");
        }
        catch (Exception)
        {
            return new("Workspace.ShortcutUnavailable");
        }
    }

    private static void WriteShortcut(string linkPath, string targetPath, string arguments)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new PlatformNotSupportedException();
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(linkPath);
        link.TargetPath = targetPath;
        link.Arguments = arguments;
        link.WorkingDirectory = Path.GetDirectoryName(targetPath);
        link.Description = "DistroNexus workspace";
        link.Save();
    }
}
