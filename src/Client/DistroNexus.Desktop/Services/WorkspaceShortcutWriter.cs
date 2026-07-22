using System.IO;
namespace DistroNexus.Desktop.Services;
public interface IWorkspaceShortcutWriter { string CreateDesktopShortcut(Guid workspaceId); }
public sealed class WorkspaceShortcutWriter : IWorkspaceShortcutWriter
{
 public string CreateDesktopShortcut(Guid workspaceId) { if (workspaceId==Guid.Empty) throw new ArgumentException("Workspace ID is required."); var target=Environment.ProcessPath??throw new InvalidOperationException("Application target is unavailable."); if(!Path.IsPathFullyQualified(target)||!File.Exists(target)) throw new InvalidOperationException("Application target is invalid."); var desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); if(string.IsNullOrWhiteSpace(desktop)||!Directory.Exists(desktop)) throw new InvalidOperationException("Desktop folder is unavailable."); var path=Path.Combine(desktop,"DistroNexus Workspace "+workspaceId.ToString("D")+".lnk"); var type=Type.GetTypeFromProgID("WScript.Shell")??throw new PlatformNotSupportedException(); dynamic shell=Activator.CreateInstance(type)!; dynamic link=shell.CreateShortcut(path); link.TargetPath=target; link.Arguments="--workspace "+workspaceId.ToString("D"); link.WorkingDirectory=Path.GetDirectoryName(target); link.Description="DistroNexus workspace"; link.Save(); return path; }
}
