using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Owns verified cache references and the closed install runtime.</summary>
public sealed class VerifiedInstallService(ICatalogService catalog, IProcessRunner processes, Func<string, CancellationToken, Task<bool>> exists, string root)
{
    private readonly string _root = Path.Combine(root, "verified-install-grants");
    private static string Sid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Lifecycle.GrantInvalid");
    public async Task<InstallTargetPreviewResult> PreviewTargetAsync(string installRoot, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(installRoot) || installRoot.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new InvalidOperationException("Install.TargetInvalid");
        string full; try { full = Path.GetFullPath(installRoot); } catch { throw new InvalidOperationException("Install.TargetInvalid"); }
        if (Path.IsPathRooted(full) == false || full.StartsWith("\\\\", StringComparison.Ordinal) || string.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase) || full.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase) || full.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.System), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Install.TargetInvalid");
        var parent = Directory.GetParent(full)?.FullName; if (parent is null || (!Directory.Exists(parent)) || HasReparsePoint(full) || !CanWriteDirectory(parent)) throw new InvalidOperationException("Install.TargetUnavailable");
        DriveInfo drive; try { drive = new DriveInfo(Path.GetPathRoot(full)!); } catch { throw new InvalidOperationException("Install.TargetUnavailable"); }
        const long required = 2L * 1024 * 1024 * 1024; if (!drive.IsReady) throw new InvalidOperationException("Install.TargetUnavailable");
        if (drive.AvailableFreeSpace < required) return new("", DateTimeOffset.UtcNow, drive.Name, drive.AvailableFreeSpace, required, false, "Install.TargetInsufficientCapacity");
        var issued = await IssueAsync("target", "target", null, full, ct, targetFingerprint: TargetFingerprint(full));
        return new(issued.PreviewToken, issued.ExpiresAt, drive.Name, drive.AvailableFreeSpace, required, true, "Install.TargetEligible");
    }
    public async Task<InstallSourceResolution> ResolveAsync(string packageId, CancellationToken ct = default)
    {
        var p = await PackageAsync(packageId, ct); var file = FileFor(p); var cached = false;
        try { if (File.Exists(file)) { await VerifyAsync(file, p, ct); cached = true; } } catch (InvalidOperationException) { }
        return new(p.Id, cached ? "Verified" : "Missing", !string.IsNullOrWhiteSpace(p.DownloadUrl), p.Sha256, p.FileSize, p.IsCustomSource ? "CustomCatalog" : "Catalog");
    }
    public async Task<PackageAcquisitionPreview> PreviewAcquireAsync(string packageId, CancellationToken ct = default)
    { var p = await PackageAsync(packageId, ct); if (string.IsNullOrWhiteSpace(p.DownloadUrl) || !Uri.TryCreate(p.DownloadUrl, UriKind.Absolute, out _)) throw new InvalidOperationException("Lifecycle.AcquisitionUnavailable"); var x=await IssueAsync("acquire", p.Id, null, null, ct); return new PackageAcquisitionPreview(x.PreviewToken,p.Id,x.ExpiresAt); }
    public async Task<PackageAcquisitionResult> AcquireAsync(string token, CancellationToken ct = default)
    {
        try { var g = await ConsumeAsync(token, "acquire", ct); var p = await PackageAsync(g.PackageId, ct); var file = FileFor(p); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            using var http = new HttpClient(); using var response = await http.GetAsync(p.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(ct)) await using (var output = new FileStream(file + ".partial", FileMode.Create, FileAccess.Write, FileShare.None)) { await input.CopyToAsync(output, ct); }
            File.Move(file + ".partial", file, true); var (hash,size)=await VerifyAsync(file,p,ct); var reference=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var expiry=DateTimeOffset.UtcNow.AddMinutes(10);
            await StoreAsync(reference, new Grant(Sid(), "reference", p.Id, file, null, expiry, hash, size), ct); return new(reference,p.Id,hash,size,expiry,"Lifecycle.Acquired"); }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch { throw new InvalidOperationException("Lifecycle.AcquisitionFailed"); }
    }
    public async Task<InstallPreview> PreviewInstallAsync(string packageReference,string name,string targetPreviewToken,string username,string shell,string? locale,bool setAsDefault,string? envelope,CancellationToken ct=default)
    {
        if (envelope is not null && (envelope.Length > 16384 || !Convert.TryFromBase64String(envelope,new byte[12288],out _))) throw new InvalidOperationException("Lifecycle.CredentialInvalid");
        name=LifecyclePathResolver.ValidateInstanceName(name); if (await exists(name,ct)) throw new InvalidOperationException("Lifecycle.InstanceStateChanged");
        if (username.Length is < 1 or > 32 || shell is not ("bash" or "zsh" or "fish" or "sh") || locale?.Length>128) throw new InvalidOperationException("Lifecycle.InstallInvalid");
        var targetGrant = await ConsumeAsync(targetPreviewToken,"target",ct); RevalidateTarget(targetGrant.Target!, targetGrant.TargetFingerprint); var r=await ConsumeAsync(packageReference,"reference",ct,false); await VerifyAsync(r.Path!,await PackageAsync(r.PackageId,ct),ct); var target=new LifecyclePathResolver().ResolveDestinationRoot(targetGrant.Target!,name); Reserve(target);
        return await IssueAsync("install",r.PackageId,name,target,ct, packageReference, username,shell,locale,setAsDefault,envelope,r.Path, targetGrant.TargetFingerprint);
    }
    public async Task<VerifiedInstallResult> InstallAsync(string token,CancellationToken ct=default)
    {
        var g=await ConsumeAsync(token,"install",ct); var recoveryId=string.Empty;
        string? staging = null;
        try { recoveryId=await RecoveryAsync(g,"Prepared",ct); if(await exists(g.Name!,ct) || !OwnsReservation(g.Target!) || Directory.Exists(g.Target!) || File.Exists(g.Target!)) throw new InvalidOperationException("Lifecycle.StateChanged"); RevalidateTarget(Path.GetDirectoryName(g.Target!)!, g.TargetFingerprint); new LifecyclePathResolver().Revalidate(Path.GetDirectoryName(g.Target!)!,true); var p=await PackageAsync(g.PackageId,ct); await VerifyAsync(g.Path ?? throw new InvalidOperationException("Lifecycle.GrantInvalid"),p,ct); var ext=Path.GetExtension(g.Path).ToLowerInvariant(); IReadOnlyList<string> args;
            if(ext==".wsl") { var help=await processes.RunAsync(new("wsl.exe",["--help"],TimeSpan.FromSeconds(10)),ct); if(!help.StandardOutput.Contains("--from-file",StringComparison.OrdinalIgnoreCase)||!help.StandardOutput.Contains("--name",StringComparison.OrdinalIgnoreCase)||!help.StandardOutput.Contains("--location",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Lifecycle.InstallRuntimeUnsupported"); args=["--install","--from-file",g.Path,"--name",g.Name!,"--location",g.Target!,"--version","2"]; }
            else if(ext==".tar") args=["--import",g.Name!,g.Target!,g.Path,"--version","2"];
            else if(g.Path.EndsWith(".tar.xz",StringComparison.OrdinalIgnoreCase)) { staging=Path.Combine(_root,"staging",Guid.NewGuid().ToString("N")); var extracted=Path.Combine(staging,"root"); var tar=Path.Combine(staging,"rootfs.tar"); Directory.CreateDirectory(extracted); await EnsureProcessAsync("tar.exe",["-xJf",g.Path,"-C",extracted],ct,"Lifecycle.InstallArchiveFailed"); var top=Directory.EnumerateFileSystemEntries(extracted).ToArray(); if(top.Length != 1 || Directory.Exists(top[0]) == false || Directory.EnumerateFileSystemEntries(extracted,"*",SearchOption.AllDirectories).Any(x=>(File.GetAttributes(x)&FileAttributes.ReparsePoint)!=0)) throw new InvalidOperationException("Lifecycle.InstallArchiveInvalid"); await EnsureProcessAsync("tar.exe",["-cf",tar,"-C",extracted,"."],ct,"Lifecycle.InstallArchiveFailed"); if(!File.Exists(tar)||(File.GetAttributes(tar)&FileAttributes.ReparsePoint)!=0||new FileInfo(tar).Length==0)throw new InvalidOperationException("Lifecycle.InstallArchiveInvalid"); args=["--import",g.Name!,g.Target!,tar,"--version","2"]; }
            else throw new InvalidOperationException("Lifecycle.InstallArtifactUnsupported");
            var r=await processes.RunAsync(new("wsl.exe",args,TimeSpan.FromMinutes(30)),ct); if(r.ExitCode!=0||r.TimedOut||r.Cancelled||r.Failure!=ProcessFailureKind.None) throw new IOException(); await ConfigureAsync(g,ct); if(g.SetDefault) await EnsureProcessAsync("wsl.exe",["--set-default",g.Name!],ct,"Lifecycle.InstallConfigurationFailed"); await RecoveryAsync(g,"Committed",ct,recoveryId); return new(true,"Install",g.Name!,"Lifecycle.Succeeded",LifecycleRecoveryAction.None,recoveryId); }
        catch(OperationCanceledException){await RecoveryAsync(g,"Cancelled",CancellationToken.None,recoveryId);return new(false,"Install",g.Name!,"Lifecycle.Cancelled",LifecycleRecoveryAction.ManualRecoveryRequired,recoveryId);}
        catch(InvalidOperationException ex){await RecoveryAsync(g,"Failed",CancellationToken.None,recoveryId);return new(false,"Install",g.Name!,Known(ex.Message)?ex.Message:"Lifecycle.Failed",LifecycleRecoveryAction.ManualRecoveryRequired,recoveryId);}
        catch { try { if(await exists(g.Name!,CancellationToken.None)) { await processes.RunAsync(new("wsl.exe",["--unregister",g.Name!],TimeSpan.FromMinutes(2)),CancellationToken.None); await RecoveryAsync(g,"RollbackRestored",CancellationToken.None,recoveryId); return new(false,"Install",g.Name!,"Lifecycle.RollbackRestored",LifecycleRecoveryAction.None,recoveryId); } } catch{} await RecoveryAsync(g,"RecoveryRequired",CancellationToken.None,recoveryId); return new(false,"Install",g.Name!,"Lifecycle.RollbackFailed",LifecycleRecoveryAction.ManualRecoveryRequired,recoveryId); }
        finally { Release(g.Target!); if(staging is not null) try { Directory.Delete(staging,true); } catch { } }
    }
    private async Task ConfigureAsync(Grant g,CancellationToken ct)
    { if(g.Username is not null && g.Username != "root") await EnsureProcessAsync("wsl.exe",["--distribution",g.Name!,"--user","root","--exec","useradd","-m","-s","/bin/"+g.Shell,g.Username],ct,"Lifecycle.InstallConfigurationFailed"); if(g.Locale is not null) await EnsureProcessAsync("wsl.exe",["--distribution",g.Name!,"--user","root","--exec","locale-gen",g.Locale],ct,"Lifecycle.InstallConfigurationFailed"); if(g.Envelope is not null) { byte[] secret; try { secret=ProtectedData.Unprotect(Convert.FromBase64String(g.Envelope),null,DataProtectionScope.CurrentUser); } catch { throw new InvalidOperationException("Lifecycle.CredentialInvalid"); } try { await EnsureProcessAsync("wsl.exe",["--distribution",g.Name!,"--user","root","--exec","chpasswd"],ct,"Lifecycle.CredentialFailed",g.Username+":"+Encoding.UTF8.GetString(secret)+"\n"); } finally { CryptographicOperations.ZeroMemory(secret); } } }
    private async Task EnsureProcessAsync(string executable,IReadOnlyList<string> arguments,CancellationToken ct,string code,string? input=null){var r=await processes.RunAsync(new ProcessRequest(executable,arguments,TimeSpan.FromMinutes(10),StandardInput:input),ct);if(r.ExitCode!=0||r.TimedOut||r.Cancelled||r.Failure!=ProcessFailureKind.None)throw new InvalidOperationException(code);}
    private async Task<DistroPackage> PackageAsync(string id,CancellationToken ct){if(string.IsNullOrWhiteSpace(id)||id.Length>128)throw new InvalidOperationException("Lifecycle.AcquisitionInvalid");return await catalog.GetDistributionByIdAsync(id,ct)??throw new InvalidOperationException("Lifecycle.AcquisitionInvalid");}
    private string FileFor(DistroPackage p)=>Path.Combine(catalog.GetPackageCachePath(),Path.GetFileName(new Uri(p.DownloadUrl).LocalPath));
    private static async Task<(string,long)> VerifyAsync(string file,DistroPackage p,CancellationToken ct){if(!File.Exists(file))throw new InvalidOperationException("Lifecycle.PackageMissing"); await using var s=File.OpenRead(file);var h=Convert.ToHexString(await SHA256.HashDataAsync(s,ct));var size=new FileInfo(file).Length;if((p.FileSize>0&&p.FileSize!=size)||(!string.IsNullOrWhiteSpace(p.Sha256)&&!string.Equals(h,p.Sha256.Replace("-",string.Empty),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Lifecycle.PackageInvalid");return(h,size);}
    private async Task<InstallPreview> IssueAsync(string op,string package,string? name,string? target,CancellationToken ct,string? reference=null,string? user=null,string? shell=null,string? locale=null,bool setDefault=false,string? env=null,string? path=null,string? targetFingerprint=null){var t=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var e=DateTimeOffset.UtcNow.AddMinutes(2);await StoreAsync(t,new Grant(Sid(),op,package,path,name,e,null,0,target,reference,user,shell,locale,setDefault,env,targetFingerprint),ct);return new(t,name??package,e);}
    private async Task StoreAsync(string token,Grant grant,CancellationToken ct){Directory.CreateDirectory(_root);await File.WriteAllBytesAsync(Path.Combine(_root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))+".grant"),ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grant),null,DataProtectionScope.CurrentUser),ct);}
    private async Task<Grant> ConsumeAsync(string token,string operation,CancellationToken ct,bool delete=true){if(token.Length!=64||token.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidOperationException("Lifecycle.GrantInvalid");var f=Path.Combine(_root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))+".grant");var consumed=f+".consumed";try { if(delete) File.Move(f,consumed,false); else consumed=f; } catch(IOException){throw new InvalidOperationException("Lifecycle.GrantInvalid");} byte[] bytes;try{bytes=await File.ReadAllBytesAsync(consumed,ct);}finally{if(delete)try{File.Delete(consumed);}catch{}} Grant g;try{g=JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(bytes,null,DataProtectionScope.CurrentUser))??throw new InvalidOperationException("Lifecycle.GrantInvalid");}catch(CryptographicException){throw new InvalidOperationException("Lifecycle.GrantInvalid");}if(g.Operation!=operation||g.ExpiresAt<=DateTimeOffset.UtcNow||g.Sid!=Sid())throw new InvalidOperationException(g.ExpiresAt<=DateTimeOffset.UtcNow?"Lifecycle.GrantExpired":"Lifecycle.GrantInvalid");return g;}
    private void Reserve(string target){using var stream=new FileStream(target+".distronexus-install",FileMode.CreateNew,FileAccess.Write,FileShare.None);}
    private static void RevalidateTarget(string root, string? expectedFingerprint = null)
    {
        try
        {
            var full = Path.GetFullPath(root); var parent = Directory.GetParent(full)?.FullName;
            if (parent is null || !Directory.Exists(parent) || HasReparsePoint(full) || !CanWriteDirectory(parent)) throw new InvalidOperationException("Install.TargetStateChanged");
            var drive = new DriveInfo(Path.GetPathRoot(full)!);
            if (!drive.IsReady || drive.AvailableFreeSpace < 2L * 1024 * 1024 * 1024 || (expectedFingerprint is not null && !string.Equals(expectedFingerprint, TargetFingerprint(full), StringComparison.Ordinal))) throw new InvalidOperationException("Install.TargetStateChanged");
        }
        catch (InvalidOperationException) { throw; }
        catch { throw new InvalidOperationException("Install.TargetStateChanged"); }
    }
    private static bool HasReparsePoint(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent ?? string.Empty;
        }
        return false;
    }
    private static bool CanWriteDirectory(string directory)
    {
        try
        {
            var attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.ReadOnly) != 0) return false;
            using var identity = WindowsIdentity.GetCurrent();
            var security = new DirectoryInfo(directory).GetAccessControl();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>();
            if (identity.User is null || !TryGetTokenSidSets(identity, out var allowSids, out var denySids)) return false;
            allowSids.Add(identity.User.Value); denySids.Add(identity.User.Value);
            return HasRequiredDirectoryAccess(rules, allowSids, denySids);
        }
        catch { return false; }
    }

    private static bool HasRequiredDirectoryAccess(IEnumerable<FileSystemAccessRule> rules, ISet<string> allowSids, ISet<string> denySids)
    {
        var remaining = FileSystemRights.CreateFiles | FileSystemRights.WriteData | FileSystemRights.DeleteSubdirectoriesAndFiles;
        foreach (var rule in rules.OrderBy(rule => rule.IsInherited ? 1 : 0).ThenBy(rule => rule.AccessControlType == AccessControlType.Deny ? 0 : 1))
        {
            if (rule.IdentityReference is not SecurityIdentifier sid) continue;
            var applicable = rule.FileSystemRights & remaining;
            if (applicable == 0) continue;
            if (rule.AccessControlType == AccessControlType.Deny)
            {
                if (denySids.Contains(sid.Value)) return false;
                continue;
            }
            if (!allowSids.Contains(sid.Value)) continue;
            remaining &= ~rule.FileSystemRights;
            if (remaining == 0) return true;
        }
        return false;
    }

    private static bool TryGetTokenSidSets(WindowsIdentity identity, out HashSet<string> allowSids, out HashSet<string> denySids)
    {
        allowSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase); denySids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!GetTokenInformation(identity.AccessToken, TokenGroupsInformationClass, IntPtr.Zero, 0, out var length) && Marshal.GetLastWin32Error() != ErrorInsufficientBuffer) return false;
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetTokenInformation(identity.AccessToken, TokenGroupsInformationClass, buffer, length, out _)) return false;
            var count = Marshal.ReadInt32(buffer); var offset = IntPtr.Size == 8 ? 8 : 4; var stride = Marshal.SizeOf<SidAndAttributes>();
            for (var index = 0; index < count; index++)
            {
                var group = Marshal.PtrToStructure<SidAndAttributes>(IntPtr.Add(buffer, offset + index * stride));
                var sid = new SecurityIdentifier(group.Sid).Value;
                if ((group.Attributes & (GroupEnabled | GroupUseForDenyOnly)) != 0) denySids.Add(sid);
                if ((group.Attributes & GroupEnabled) != 0 && (group.Attributes & GroupUseForDenyOnly) == 0) allowSids.Add(sid);
            }
            return true;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private const int TokenGroupsInformationClass = 2;
    private const int ErrorInsufficientBuffer = 122;
    private const uint GroupEnabled = 0x00000004;
    private const uint GroupUseForDenyOnly = 0x00000010;
    [StructLayout(LayoutKind.Sequential)] private struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);
    private static bool OwnsReservation(string target)=>File.Exists(target+".distronexus-install") && (File.GetAttributes(target+".distronexus-install")&FileAttributes.ReparsePoint)==0;
    private static void Release(string target){try{File.Delete(target+".distronexus-install");}catch{}}
    private async Task<string> RecoveryAsync(Grant grant,string checkpoint,CancellationToken ct,string? id=null){id??=Guid.NewGuid().ToString("N");var dir=Path.Combine(_root,"recovery");Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,id+".json"),JsonSerializer.Serialize(new { Id=id, Operation="Install", InstanceName=grant.Name, Checkpoint=checkpoint, At=DateTimeOffset.UtcNow }),ct);return id;}
    private static bool Known(string code)=>code.StartsWith("Lifecycle.Install",StringComparison.Ordinal)||code.StartsWith("Lifecycle.Credential",StringComparison.Ordinal)||code.StartsWith("Install.Target", StringComparison.Ordinal)||code is "Lifecycle.StateChanged" or "Lifecycle.PackageMissing" or "Lifecycle.PackageInvalid";
    private static string TargetFingerprint(string root) { var full = Path.GetFullPath(root); var drive = new DriveInfo(Path.GetPathRoot(full)!); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{full}|{drive.Name}|{drive.TotalSize}|{drive.AvailableFreeSpace}|2147483648"))); }
    private sealed record Grant(string Sid,string Operation,string PackageId,string? Path,string? Name,DateTimeOffset ExpiresAt,string? Hash,long Size,string? Target=null,string? Reference=null,string? Username=null,string? Shell=null,string? Locale=null,bool SetDefault=false,string? Envelope=null,string? TargetFingerprint=null);
}
