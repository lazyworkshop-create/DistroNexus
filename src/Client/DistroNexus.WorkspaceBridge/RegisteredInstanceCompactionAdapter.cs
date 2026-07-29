using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Win32;

namespace DistroNexus.WorkspaceBridge;

/// <summary>Owns registry/VHDX access and the sole fixed diskpart argument shape for reviewed compaction.</summary>
public sealed class RegisteredInstanceCompactionAdapter : IRegisteredInstanceCompactionAdapter
{
    private readonly IProcessRunner _processes;
    private readonly Func<string, Entry?> _lookup;
    private readonly Func<bool> _isAdministrator;

    public RegisteredInstanceCompactionAdapter(IProcessRunner processes) : this(processes, Find) { }
    public RegisteredInstanceCompactionAdapter(IProcessRunner processes, Func<string, Entry?> lookup, Func<bool>? isAdministrator = null) { _processes = processes; _lookup = lookup; _isAdministrator = isAdministrator ?? IsAdministrator; }

    public async Task<RegisteredInstanceCompactionState?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var entry = _lookup(name);
        if (entry is null) return null;
        var running = await IsRunningAsync(entry.Name, cancellationToken).ConfigureAwait(false);
        var outcome = _isAdministrator() ? "Ready" : "Lifecycle.CompactionPrivilegeUnavailable";
        return new(entry.Name, entry.Identity, entry.VhdxIdentity, running, entry.Size, "Diskpart", outcome);
    }

    public async Task<InstanceCompactionExecution> CompactAsync(RegisteredInstanceCompactionState state, CancellationToken cancellationToken = default)
    {
        var entry = _lookup(state.Name);
        var current = entry is null ? null : await GetAsync(state.Name, cancellationToken).ConfigureAwait(false);
        if (entry is null || current is null || !Matches(state, entry, current.IsRunning)) return new(false, "Lifecycle.CompactionStateChanged", null, "Diskpart", false);
        if (!_isAdministrator()) return new(false, "Lifecycle.CompactionPrivilegeUnavailable", null, "Diskpart", false);
        var stopped = false;
        var outcome = new InstanceCompactionExecution(false, "Lifecycle.CompactionFailed", null, "Diskpart", false);
        try
        {
            if (current.IsRunning)
            {
                // fstrim must run while the distribution is still active. A stopped instance is never started for trim.
                await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", entry.Name, "--exec", "fstrim", "-av"], TimeSpan.FromMinutes(2)), cancellationToken).ConfigureAwait(false);
                var stop = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--terminate", entry.Name], TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);
                if (!Succeeded(stop)) return new(false, "Lifecycle.CompactionStopFailed", null, "Diskpart", false);
                stopped = true;
            }
            var script = Path.Combine(Path.GetTempPath(), "distronexus-compact-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                await File.WriteAllTextAsync(script, $"select vdisk file=\"{entry.VhdxPath}\"{Environment.NewLine}compact vdisk{Environment.NewLine}exit{Environment.NewLine}", Encoding.ASCII, cancellationToken).ConfigureAwait(false);
                var compact = await _processes.RunAsync(new ProcessRequest("diskpart.exe", ["/s", script], TimeSpan.FromMinutes(10)), cancellationToken).ConfigureAwait(false);
                if (!Succeeded(compact)) outcome = new(false, "Lifecycle.CompactionFailed", null, "Diskpart", false);
                else outcome = new(true, "Lifecycle.Compacted", new FileInfo(entry.VhdxPath).Length, "Diskpart", false);
            }
            finally { TryDelete(script); }
        }
        catch (OperationCanceledException) { outcome = new(false, "Lifecycle.Cancelled", null, "Diskpart", false); }
        catch (Exception) { outcome = new(false, "Lifecycle.CompactionFailed", null, "Diskpart", false); }
        if (stopped)
        {
            var start = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", entry.Name, "--exec", "echo", "started"], TimeSpan.FromSeconds(30)), CancellationToken.None).ConfigureAwait(false);
            if (!Succeeded(start)) return new(false, "Lifecycle.CompactionRestartRecoveryRequired", outcome.AfterBytes, "Diskpart", false, "ManualRecoveryRequired");
            return outcome with { Restarted = true };
        }
        return outcome;
    }

    private static bool Matches(RegisteredInstanceCompactionState state, Entry entry, bool running) =>
        state.Identity == entry.Identity && state.VhdxIdentity == entry.VhdxIdentity && state.IsRunning == running && state.CurrentSizeBytes == entry.Size;
    private static bool Succeeded(ProcessResult result) => result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None;
    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    private static Entry? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\r', '\n', '\0']) >= 0) return null;
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var lxss = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (lxss is null) return null;
        foreach (var keyName in lxss.GetSubKeyNames())
        {
            using var key = lxss.OpenSubKey(keyName);
            if (key is null || !string.Equals(key.GetValue("DistributionName") as string, name, StringComparison.Ordinal)) continue;
            if (key.GetValue("BasePath") is not string basePath) return null;
            var vhdx = Path.Combine(basePath, "ext4.vhdx");
            var file = new FileInfo(vhdx);
            if (!file.Exists) return null;
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyName)));
            var vhdxIdentity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{keyName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}")));
            return new Entry(name, identity, vhdx, vhdxIdentity, file.Length);
        }
        return null;
    }
    private async Task<bool> IsRunningAsync(string name, CancellationToken cancellationToken)
    {
        var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--list", "--running", "--quiet"], TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        return Succeeded(result) && result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(item => string.Equals(item, name, StringComparison.Ordinal));
    }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    public sealed record Entry(string Name, string Identity, string VhdxPath, string VhdxIdentity, long Size);
}
