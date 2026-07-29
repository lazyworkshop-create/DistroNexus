using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class WslgApplicationService : IWslgApplicationService
{
    private static readonly string[] Roots = ["/usr/share/applications", "/usr/local/share/applications", "/home"];
    private readonly IProcessRunner _runner; private readonly IPlatformCapabilityService? _capabilities; private readonly VersionedJsonStore<string[]> _pins; private readonly SemaphoreSlim _gate = new(1,1); private readonly WslgIconCache _icons = new(); private readonly WslgDiscoveryGrantStore _grants;
    public WslgApplicationService(IProcessRunner runner, IPlatformCapabilityService? capabilities = null, string? appDataDirectory = null, TimeProvider? timeProvider = null) { _runner=runner; _capabilities=capabilities; var root=appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus"); _pins=new VersionedJsonStore<string[]>(Path.Combine(root,"wslg-pins.json"),1); _grants = new WslgDiscoveryGrantStore(root, timeProvider); }
    public async Task<WslgDiscoveryResult> DiscoverWithGrantAsync(string instanceName, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(instanceName, ct);
        if (!status.IsAvailable) return new(status, null, null, []);
        var applications = await DiscoverAsync(instanceName, ct);
        var issued = await _grants.IssueAsync(instanceName, applications, ct);
        return new(status, issued.Token, issued.ExpiresAt, applications.Select(a => new WslgApplicationProjection(a.Id, a.Name, a.Categories, a.IsPinned, a.IconBytes)).ToArray());
    }
    public async Task<WslgActionResult> LaunchGrantedAsync(string discoveryToken, string applicationId, CancellationToken ct = default)
    {
        var app = await _grants.ResolveAsync(discoveryToken, applicationId, ct);
        var result = await LaunchAsync(app, ct);
        return new(result.Succeeded, result.Succeeded ? result.Diagnostic : "Wslg.EntryChanged");
    }
    public async Task<WslgActionResult> RevealGrantedAsync(string discoveryToken, string applicationId, CancellationToken ct = default)
    {
        var app = await _grants.ResolveAsync(discoveryToken, applicationId, ct);
        var result = await RevealAsync(app, ct);
        return new(result.Succeeded, result.Succeeded ? result.Diagnostic : "Wslg.EntryChanged");
    }
    public async Task<WslgActionResult> SetGrantedPinnedAsync(string discoveryToken, string applicationId, bool pinned, CancellationToken ct = default)
    {
        var app = await _grants.ResolveAsync(discoveryToken, applicationId, ct);
        var rebound = await ReadEntryAsync(app.InstanceName, app.DesktopFilePath, ct);
        if (rebound is null || rebound.Id != app.Id || rebound.Executable != app.Executable || !rebound.Arguments.SequenceEqual(app.Arguments, StringComparer.Ordinal)) throw new InvalidOperationException("Wslg.EntryChanged");
        await SetPinnedAsync(app.Id, pinned, ct);
        return new(true, "Application pin updated.");
    }
    public async Task<WslgApplicationStatus> GetStatusAsync(string instanceName, CancellationToken ct=default)
    {
        if (string.IsNullOrWhiteSpace(instanceName)) return new(false, "A WSL distribution is required.", []);
        var gate=await EnsureCapabilityAsync(ct); if(gate is not null) return gate;
        var result=await RunAsync(instanceName, ["/usr/bin/env"], ct);
        return result.ExitCode == 0 && result.StandardOutput.Split('\n').Any(x => x.StartsWith("WAYLAND_DISPLAY=", StringComparison.Ordinal) && x.Length > "WAYLAND_DISPLAY=".Length) ? new(true,"WSLg is available.",[]) : new(false,"WSLg display support is unavailable for this distribution.",["Start a WSLg-enabled distribution and verify display and audio integration."]);
    }
    public async Task<IReadOnlyList<WslgApplication>> DiscoverAsync(string instanceName, CancellationToken ct=default)
    {
        if(await EnsureCapabilityAsync(ct) is not null) return [];
        var list=await RunAsync(instanceName,["/usr/bin/find",Roots[0],Roots[1],Roots[2],"-type","f","-name","*.desktop","-print"],ct);
        var paths=list.StandardOutput.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(DesktopEntryParser.IsApprovedDesktopPath).Distinct(StringComparer.Ordinal).Take(2048).ToArray();
        var pins=await GetPinsAsync(ct); var apps=new List<WslgApplication>();
        foreach(var path in paths) { var before=await ProbeRegularFileAsync(instanceName,path,64*1024,ct); if(before is null)continue; var read=await RunAsync(instanceName,["/bin/cat","--",path],ct); if(before != await ProbeRegularFileAsync(instanceName,path,64*1024,ct))continue; var app=read.ExitCode==0 ? DesktopEntryParser.Parse(instanceName,path,read.StandardOutput) : null; if(app is not null) { var pinned=app with { IsPinned=pins.Contains(app.Id) }; apps.Add(pinned with { IconBytes=await GetIconAsync(pinned,ct) }); } }
        return apps.GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).Select(x=>x.OrderBy(a=>a.DesktopFilePath,StringComparer.Ordinal).First()).OrderByDescending(x=>x.IsPinned).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public async Task<WslgLaunchResult> LaunchAsync(WslgApplication app, CancellationToken ct=default)
    {
        if(await EnsureCapabilityAsync(ct) is { } unavailable) return new(false,app.InstanceName,app.Executable,unavailable.Reason);
        if (!DesktopEntryParser.IsApprovedDesktopPath(app.DesktopFilePath) || !DesktopEntryParser.IsSafeExecutable(app.Executable) || app.Arguments.Any(x=>string.IsNullOrWhiteSpace(x) || x.IndexOfAny(['\0','\n','\r'])>=0 || x.Length>4096)) return new(false,app.InstanceName,app.Executable,"The desktop entry is no longer safe to launch.");
        var bound=await ReadEntryAsync(app.InstanceName,app.DesktopFilePath,ct);
        if(bound is null || bound.Id != app.Id || bound.Executable != app.Executable || !bound.Arguments.SequenceEqual(app.Arguments,StringComparer.Ordinal)) return new(false,app.InstanceName,app.Executable,"The desktop entry changed and must be rediscovered before launch.");
        var request=new ProcessRequest("wsl.exe", ["--distribution",bound.InstanceName,"--exec",bound.Executable,..bound.Arguments], TimeSpan.FromSeconds(15), 64*1024,64*1024);
        var result=await _runner.RunAsync(request,ct); return new(result.ExitCode==0,app.InstanceName,app.Executable,result.ExitCode==0 ? "Application launched." : $"Launch failed for {app.InstanceName} ({app.Executable}). {Redact(result.StandardError)}");
    }
    private async Task<WslgApplication?> ReadEntryAsync(string instance,string path,CancellationToken ct)
    { var before=await ProbeRegularFileAsync(instance,path,64*1024,ct); if(before is null)return null; var read=await RunAsync(instance,["/bin/cat","--",path],ct); return read.ExitCode==0 && before==await ProbeRegularFileAsync(instance,path,64*1024,ct) ? DesktopEntryParser.Parse(instance,path,read.StandardOutput) : null; }
    public async Task<byte[]?> GetIconAsync(WslgApplication app, CancellationToken ct=default)
    {
        if(await EnsureCapabilityAsync(ct) is not null) return null;
        if (string.IsNullOrWhiteSpace(app.IconPath) || !DesktopEntryParser.IsApprovedIconPath(app.IconPath)) return null;
        var cacheKey=app.InstanceName+"\n"+app.IconPath;
        if (_icons.TryGet(cacheKey, out var cached)) return cached;
        var before=await ProbeRegularFileAsync(app.InstanceName, app.IconPath, 1024*1024, ct); if (before is null) return null;
        var result=await _runner.RunAsync(new ProcessRequest("wsl.exe",["--distribution",app.InstanceName,"--exec","/usr/bin/base64","--wrap=0","--",app.IconPath],TimeSpan.FromSeconds(10),1400*1024,4096),ct);
        if(result.ExitCode != 0 || result.OutputTruncated || before != await ProbeRegularFileAsync(app.InstanceName, app.IconPath, 1024*1024, ct)) return null;
        byte[] bytes; try { bytes=Convert.FromBase64String(result.StandardOutput.Trim()); } catch (FormatException) { return null; }
        return _icons.TryAdd(cacheKey,app.IconPath,bytes) ? bytes : null;
    }
    public async Task<WslgLaunchResult> RevealAsync(WslgApplication app, CancellationToken ct=default)
    {
        if(await EnsureCapabilityAsync(ct) is { } unavailable) return new(false,app.InstanceName,string.Empty,unavailable.Reason);
        if(!DesktopEntryParser.IsApprovedDesktopPath(app.DesktopFilePath)) return new(false,app.InstanceName,string.Empty,"The desktop entry path is unsafe.");
        var bound=await ReadEntryAsync(app.InstanceName,app.DesktopFilePath,ct);
        if(bound is null || bound.Id != app.Id || bound.Executable != app.Executable || !bound.Arguments.SequenceEqual(app.Arguments,StringComparer.Ordinal)) return new(false,app.InstanceName,string.Empty,"The desktop entry changed and must be rediscovered before reveal.");
        var directory=app.DesktopFilePath[..app.DesktopFilePath.LastIndexOf('/')];
        var result=await _runner.RunAsync(new ProcessRequest("wsl.exe",["--distribution",app.InstanceName,"--exec","/usr/bin/xdg-open",directory],TimeSpan.FromSeconds(10),64*1024,64*1024),ct);
        return new(result.ExitCode==0,app.InstanceName,"/usr/bin/xdg-open",result.ExitCode==0 ? "Desktop entry location opened." : "Could not reveal the desktop entry.");
    }
    public async Task SetPinnedAsync(string id,bool pinned,CancellationToken ct=default) { if(await EnsureCapabilityAsync(ct) is not null) throw new InvalidOperationException("WSLg capability is unavailable."); await _gate.WaitAsync(ct); try { var doc=await _pins.ReadAsync(ct); var pins=(doc.Value?.Value ?? []).ToHashSet(StringComparer.Ordinal); if(pinned) pins.Add(id); else pins.Remove(id); var write=await _pins.WriteAsync(pins.Order().ToArray(),doc.Value?.Revision ?? 0,ct); if(!write.Succeeded) throw new InvalidOperationException("Could not persist application pins."); } finally {_gate.Release();} }
    public async Task<IReadOnlySet<string>> GetPinsAsync(CancellationToken ct=default) => await EnsureCapabilityAsync(ct) is not null ? new HashSet<string>() : (await ReadPinsAsync(ct)).ToHashSet(StringComparer.Ordinal);
    private async Task<string[]> ReadPinsAsync(CancellationToken ct) { var doc=await _pins.ReadAsync(ct); return doc.Succeeded && doc.Value is not null ? doc.Value.Value : []; }
    private async Task<WslgApplicationStatus?> EnsureCapabilityAsync(CancellationToken ct)
    { if(_capabilities is null)return null; var snapshot=await _capabilities.GetHostSnapshotAsync(cancellationToken:ct); return !snapshot.Capabilities.TryGetValue(CapabilityId.Wslg,out var capability) || !capability.IsSupported ? new(false,$"WSLg unavailable: {(snapshot.Capabilities.TryGetValue(CapabilityId.Wslg,out var found) ? found.ReasonCode : "Unknown")}.",["Update or enable WSLg, then refresh."]) : null; }
    private Task<ProcessResult> RunAsync(string instance, IReadOnlyList<string> args, CancellationToken ct) => _runner.RunAsync(new ProcessRequest("wsl.exe",["--distribution",instance,"--exec",..args],TimeSpan.FromSeconds(15),2*1024*1024,64*1024),ct);
    private async Task<bool> IsRegularFileAsync(string instance, string path, int maxBytes, CancellationToken ct) => await ProbeRegularFileAsync(instance,path,maxBytes,ct) is not null;
    private async Task<string?> ProbeRegularFileAsync(string instance, string path, int maxBytes, CancellationToken ct)
    {
        if (!DesktopEntryParser.IsCanonicalLinuxPath(path)) return null;
        // lstat's %F rejects symlinks; inode ties the immediately-before and immediately-after observations.
        var probe=await RunAsync(instance,["/usr/bin/stat","--printf=%F:%s:%i","--",path],ct);
        if(probe.ExitCode != 0) return null;
        var parts=probe.StandardOutput.Trim().Split(':');
        return parts.Length==3 && parts[0]=="regular file" && long.TryParse(parts[1],out var size) && size is >= 0 && size <= maxBytes && long.TryParse(parts[2],out _) ? probe.StandardOutput.Trim() : null;
    }
    internal static bool IsDecodableImage(byte[] bytes)
    {
        // Header-level format and dimension limits keep hostile payloads out before any UI decoder sees them.
        if(bytes.Length<24)return false;
        if(bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})) { if(bytes.Length<33 || !bytes.AsSpan(12,4).SequenceEqual("IHDR"u8))return false; var w=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4)); var h=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4)); if(w is <=0 or >4096 || h is <=0 or >4096)return false; var offset=8; var hasEnd=false; var hasIdat=false; while(offset+12<=bytes.Length){var length=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset,4));if(length<0 || offset+12L+length>bytes.Length)return false; var kind=bytes.AsSpan(offset+4,4); if(kind.SequenceEqual("IDAT"u8))hasIdat|=length>0; if(kind.SequenceEqual("IEND"u8)){hasEnd=length==0 && offset+12==bytes.Length;break;} offset+=12+length;} return hasEnd && hasIdat; }
        if(bytes[0]!=0xff || bytes[1]!=0xd8) return false;
        var dimensions=false; for(var i=2;i+1<bytes.Length;) { if(bytes[i++]!=0xff)continue; while(i<bytes.Length && bytes[i]==0xff)i++; if(i>=bytes.Length)return false; var marker=bytes[i++]; if(marker==0xd9)return dimensions && i==bytes.Length; if(marker==0xd8)continue; if(i+1>=bytes.Length)return false; var length=(bytes[i]<<8)|bytes[i+1]; if(length<2 || i+length>bytes.Length)return false; if(marker is >=0xc0 and <=0xc3 or >=0xc5 and <=0xc7 or >=0xc9 and <=0xcb or >=0xcd and <=0xcf) { if(length<8)return false; var h=(bytes[i+3]<<8)|bytes[i+4];var w=(bytes[i+5]<<8)|bytes[i+6];dimensions=w is >0 and <=4096 && h is >0 and <=4096; if(!dimensions)return false; } i+=length; } return false;
    }
    private static string Redact(string value) => string.IsNullOrWhiteSpace(value) ? "No additional diagnostics." : value.Replace(Environment.UserName,"<user>",StringComparison.OrdinalIgnoreCase).Trim()[..Math.Min(512,value.Trim().Length)];
}
