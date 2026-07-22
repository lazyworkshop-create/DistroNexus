using System.Text.RegularExpressions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.WorkspaceBridge;

/// <summary>
/// Minimal runtime adapter used only by WorkspaceRuntime.  It intentionally exposes
/// no destructive instance-management implementation through the bridge.
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

    public async Task<bool> StopInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--terminate", instanceName], TimeSpan.FromSeconds(30)), cancellationToken);
        return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None;
    }

    private static Task Unsupported() => Task.FromException(new NotSupportedException("WorkspaceBridge does not expose WSL instance management."));
    private static Task<bool> UnsupportedBool() => Task.FromException<bool>(new NotSupportedException("WorkspaceBridge does not expose WSL instance management."));
    private static Task<T> Unsupported<T>() => Task.FromException<T>(new NotSupportedException("WorkspaceBridge does not expose WSL instance management."));
    public Task InstallInstanceAsync(InstallOptions options, IProgress<(double Percentage, string Message)>? progress = null, CancellationToken cancellationToken = default) => Unsupported();
    public Task<bool> StartInstanceAsync(string instanceName, CancellationToken cancellationToken = default) => UnsupportedBool();
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
