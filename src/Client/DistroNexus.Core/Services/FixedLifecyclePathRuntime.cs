using System.Text.RegularExpressions;
using Microsoft.Win32;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Closed WSL runtime: callers can select only registered names and Core-derived paths.</summary>
public sealed class FixedLifecyclePathRuntime(IProcessRunner processes, string root) : ILifecyclePathRuntime
{
    private static readonly Regex ListRow = new(@"^\*?\s*(?<name>.+?)\s+(?<state>Running|Stopped)\s+(?<version>[12])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly string _stagingRoot = Path.Combine(Path.GetFullPath(root), "lifecycle-staging");
    public async Task<List<WslInstance>> GetInstancesAsync(CancellationToken ct = default)
    {
        var r = await processes.RunAsync(new("wsl.exe", ["--list", "--verbose"], TimeSpan.FromSeconds(15), OutputEncoding: ProcessOutputEncoding.Utf16LittleEndian), ct);
        if (r.ExitCode != 0 || r.TimedOut || r.Cancelled || r.Failure != ProcessFailureKind.None) throw new IOException("Lifecycle list failed.");
        return r.StandardOutput.Replace("\0", string.Empty).Split('\n').Select(x => ListRow.Match(x.Trim())).Where(x => x.Success).Select(x => new WslInstance { Name = x.Groups["name"].Value.Trim(), State = x.Groups["state"].Value, Version = int.Parse(x.Groups["version"].Value) }).ToList();
    }
    public async Task RemoveAsync(string name, bool keepFiles, CancellationToken ct = default)
    {
        if (keepFiles) throw new InvalidOperationException("Lifecycle.KeepFilesUnavailable");
        await EnsureAsync(["--unregister", name], ct, "remove");
    }
    public async Task ExportAsync(string name, string destination, bool stopRunning, CancellationToken ct = default)
    {
        if (stopRunning) await EnsureAsync(["--terminate", name], ct, "stop");
        await EnsureAsync(["--export", name, destination], ct, "export");
    }
    public async Task ImportAsync(string name, string source, string target, CancellationToken ct = default) => await EnsureAsync(["--import", name, target, source], ct, "import");
    public async Task MoveAsync(string name, string target, CancellationToken ct = default)
    {
        var original = RegisteredPath(name); Directory.CreateDirectory(_stagingRoot); var archive = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N") + ".tar"); var removed = false;
        try { await ExportAsync(name, archive, true, ct); await RemoveAsync(name, false, ct); removed = true; await ImportAsync(name, archive, target, ct); }
        catch { if (removed) { try { await ImportAsync(name, archive, original, CancellationToken.None); throw new InvalidOperationException("Lifecycle.RollbackRestored"); } catch (InvalidOperationException ex) when (ex.Message == "Lifecycle.RollbackRestored") { throw; } catch { throw new InvalidOperationException("Lifecycle.RollbackFailed"); } } throw; }
        finally { try { File.Delete(archive); } catch (IOException) { } }
    }
    public async Task RenameAsync(string name, string newName, CancellationToken ct = default)
    {
        var original = RegisteredPath(name); Directory.CreateDirectory(_stagingRoot); var archive = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N") + ".tar"); var removed = false;
        try { await ExportAsync(name, archive, true, ct); await RemoveAsync(name, false, ct); removed = true; await ImportAsync(newName, archive, original, ct); }
        catch { if (removed) { try { await ImportAsync(name, archive, original, CancellationToken.None); throw new InvalidOperationException("Lifecycle.RollbackRestored"); } catch (InvalidOperationException ex) when (ex.Message == "Lifecycle.RollbackRestored") { throw; } catch { throw new InvalidOperationException("Lifecycle.RollbackFailed"); } } throw; }
        finally { try { File.Delete(archive); } catch (IOException) { } }
    }
    private static string RegisteredPath(string name)
    {
        using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (root is not null) foreach (var keyName in root.GetSubKeyNames()) { using var key = root.OpenSubKey(keyName); if (string.Equals(key?.GetValue("DistributionName") as string, name, StringComparison.OrdinalIgnoreCase) && key.GetValue("BasePath") is string path && Path.IsPathFullyQualified(path)) return Path.GetFullPath(path); }
        throw new InvalidOperationException("Lifecycle.RegistrationUnavailable");
    }
    private async Task EnsureAsync(IReadOnlyList<string> args, CancellationToken ct, string operation)
    { var r = await processes.RunAsync(new("wsl.exe", args, TimeSpan.FromMinutes(30)), ct); if (r.ExitCode != 0 || r.TimedOut || r.Cancelled || r.Failure != ProcessFailureKind.None) throw new IOException("Lifecycle " + operation + " failed."); }
}
