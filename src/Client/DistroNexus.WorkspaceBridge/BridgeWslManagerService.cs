using System.Text.RegularExpressions;
using Microsoft.Win32;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.WorkspaceBridge;

/// <summary>
/// Minimal runtime adapter used only by WorkspaceRuntime. It deliberately exposes
/// only the list, start, and stop instance lifecycle operations through the bridge.
/// </summary>
internal sealed class BridgeWslManagerService(IProcessRunner processes) : IWslManagerService
{
    private static readonly Regex ListRow = new(@"^\*?\s*(?<name>.+?)\s+(?<state>Running|Stopped)\s+(?<version>[12])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly IProcessRunner _processes = processes;

    public async Task<List<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--list", "--verbose"], TimeSpan.FromSeconds(15), OutputEncoding: ProcessOutputEncoding.Utf16LittleEndian), cancellationToken);
        if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None)
            return [];
        return result.StandardOutput.Replace("\0", string.Empty).Split('\n')
            .Select(line => ListRow.Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => new WslInstance { Name = match.Groups["name"].Value.Trim(), State = match.Groups["state"].Value, Version = int.Parse(match.Groups["version"].Value) })
            .ToList();
    }

    public async Task<IReadOnlyList<BridgeInstanceDetails>> GetInstanceDetailsAsync(InstanceListOptions options, CancellationToken cancellationToken = default)
    {
        List<WslInstance> listed;
        try { listed = await GetInstancesAsync(cancellationToken); }
        catch { listed = []; }
        var status = listed.ToDictionary(instance => instance.Name, StringComparer.OrdinalIgnoreCase);
        var instances = new List<BridgeInstanceDetails>();
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var lxss = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (lxss is null) return instances;

        foreach (var keyName in lxss.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var key = lxss.OpenSubKey(keyName);
            if (key is null) continue;
            var name = key.GetValue("DistributionName") as string;
            if (string.IsNullOrWhiteSpace(name)) continue;
            var basePath = WslInstance.NormalizeWindowsPath(key.GetValue("BasePath") as string);
            var item = status.TryGetValue(name, out var current)
                ? new BridgeInstanceDetails(name, current.State, current.Version, basePath, 0, null, name, keyName)
                : new BridgeInstanceDetails(name, "Stopped", 0, basePath, 0, null, name, keyName);

            if (!string.IsNullOrWhiteSpace(basePath) && Directory.Exists(basePath))
            {
                item = item with { InstallTime = Directory.GetCreationTime(basePath) };
                if (!options.SkipDiskSize)
                {
                    var vhdx = Path.Combine(basePath, "ext4.vhdx");
                    if (File.Exists(vhdx)) item = item with { DiskSize = new FileInfo(vhdx).Length };
                }
            }

            if (item.State.Equals("Running", StringComparison.OrdinalIgnoreCase))
                item = item with { Distribution = await ProbeAsync(name, ["--distribution", name, "--", "cat", "/etc/os-release"], "PRETTY_NAME", cancellationToken) ?? item.Distribution };
            if (options.IncludeRelease)
                item = item with { Release = await ProbeAsync(name, ["--distribution", name, "--", "bash", "-c", "lsb_release -d 2>/dev/null | cut -f2"], null, cancellationToken) };
            if (options.IncludeUser)
                item = item with { CurrentUser = await ProbeAsync(name, ["--distribution", name, "--", "whoami"], null, cancellationToken) };
            instances.Add(item);
        }
        return instances;
    }

    private async Task<string?> ProbeAsync(string instanceName, IReadOnlyList<string> arguments, string? osReleaseKey, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", arguments, TimeSpan.FromSeconds(15)), cancellationToken);
            if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None) return null;
            var output = result.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(osReleaseKey)) return output;
            var match = Regex.Match(output, $"^{osReleaseKey}=\\\"(?<value>[^\\\"]+)\\\"", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["value"].Value : null;
        }
        catch { return null; }
    }

    public async Task<bool> StopInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--terminate", instanceName], TimeSpan.FromSeconds(30)), cancellationToken);
        return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None;
    }

    public async Task<bool> StartInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", instanceName, "--exec", "echo", "started"], TimeSpan.FromSeconds(30)), cancellationToken);
        return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None;
    }

    private static Task Unsupported() => Task.FromException(new NotSupportedException("WorkspaceBridge does not expose this WSL instance-management operation."));
    private static Task<bool> UnsupportedBool() => Task.FromException<bool>(new NotSupportedException("WorkspaceBridge does not expose WSL instance management."));
    private static Task<T> Unsupported<T>() => Task.FromException<T>(new NotSupportedException("WorkspaceBridge does not expose WSL instance management."));
    public Task InstallInstanceAsync(InstallOptions options, IProgress<(double Percentage, string Message)>? progress = null, CancellationToken cancellationToken = default) => Unsupported();
    public Task<bool> StartInstanceWithKeepAliveAsync(string instanceName, CancellationToken cancellationToken = default) => UnsupportedBool();
    public Task<bool> RemoveInstanceAsync(string instanceName, CancellationToken cancellationToken = default) => UnsupportedBool();
    public Task MoveInstanceAsync(string instanceName, string newPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Unsupported();
    public Task RenameInstanceAsync(string oldName, string newName, CancellationToken cancellationToken = default) => Unsupported();
    public Task SetCredentialsAsync(string instanceName, string username, string password, CancellationToken cancellationToken = default) => Unsupported();
    public Task<long> GetInstanceDiskSizeAsync(string instanceName, CancellationToken cancellationToken = default) => Unsupported<long>();
    public Task<WslInstance?> ForceRefreshInstanceAsync(string instanceName, CancellationToken cancellationToken = default) => Unsupported<WslInstance?>();
    public Task CompactInstanceAsync(string instanceName, IProgress<(double Percentage, string Message)>? progress = null, bool whatIf = false, CancellationToken cancellationToken = default) => Unsupported();
    public Task ExportInstanceAsync(string name, string destination, bool force = false, CancellationToken cancellationToken = default) => Unsupported();
    public Task ImportInstanceAsync(string name, string source, string installPath, CancellationToken cancellationToken = default) => Unsupported();
    public Task<object?> GetInstanceConfigAsync(string name, CancellationToken cancellationToken = default) => Unsupported<object?>();
    public Task SetSparseModeAsync(string name, bool enabled, CancellationToken cancellationToken = default) => Unsupported();
    public Task ShutdownWslAsync(CancellationToken cancellationToken = default) => Unsupported();
}

internal sealed record InstanceListOptions(bool IncludeRelease, bool IncludeUser, bool SkipDiskSize);
internal sealed record BridgeInstanceDetails(string Name, string State, int Version, string BasePath, long DiskSize, DateTime? InstallTime, string Distribution, string Guid, string? Release = null, string? CurrentUser = null);
