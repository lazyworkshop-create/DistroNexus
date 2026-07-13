using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Win32;

namespace DistroNexus.Core.Services;

/// <summary>Typed WSL adapter. It never constructs a shell command from recovery input.</summary>
public sealed class WslRecoveryPointRuntime : IRecoveryPointRuntime
{
    private readonly IProcessRunner _process;
    private readonly IPlatformCapabilityService _capabilities;

    public WslRecoveryPointRuntime(IProcessRunner process, IPlatformCapabilityService capabilities)
    { _process = process; _capabilities = capabilities; }

    public async Task<RecoveryRuntimeSource> GetSourceAsync(string instanceName, CancellationToken ct = default)
    {
        var instance = await _capabilities.GetInstanceSnapshotAsync(instanceName, cancellationToken: ct);
        var host = await _capabilities.GetHostSnapshotAsync(cancellationToken: ct);
        var version = instance.Instance.WslVersion ?? 1;
        var vhd = host.Capabilities.TryGetValue(CapabilityId.VhdExport, out var export) && export.IsSupported;
        var inPlace = host.Capabilities.TryGetValue(CapabilityId.ImportInPlace, out var import) && import.IsSupported;
        var running = await IsRunningAsync(instanceName, ct);
        // A preview must not start a stopped distribution just to obtain a size estimate.
        // WSL has no portable offline TAR-size probe, so retain a conservative non-zero
        // planning floor until an explicitly running instance can answer the fixed probe.
        var estimate = running
            ? await EstimateFilesystemBytesAsync(instanceName, ct)
            : ConservativeOfflineEstimateBytes;
        return new(version, estimate, running, vhd, inPlace);
    }

    public async Task ExportAsync(string instanceName, string partialPayloadPath, RecoveryPointFormat format, CancellationToken ct = default)
    {
        ValidateInstanceName(instanceName); ValidatePath(partialPayloadPath);
        var args = new List<string> { "--export", instanceName, partialPayloadPath };
        if (format == RecoveryPointFormat.Vhdx) args.Add("--vhd");
        await EnsureSuccessAsync(new("wsl.exe", args, TimeSpan.FromMinutes(30)), ct, "export");
    }

    public async Task ImportAsync(string operationId, string instanceName, string payloadPath, string targetDirectory, RecoveryPointFormat format, bool importInPlace, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Any(char.IsControl)) throw new ArgumentException("Operation id is invalid.", nameof(operationId));
        ValidateInstanceName(instanceName); ValidatePath(payloadPath);
        if (importInPlace)
        {
            if (format != RecoveryPointFormat.Vhdx) throw new ArgumentException("Import-in-place is valid only for VHDX payloads.", nameof(importInPlace));
            if (!string.IsNullOrWhiteSpace(targetDirectory)) throw new ArgumentException("Import-in-place does not accept a managed target directory.", nameof(targetDirectory));
        }
        else
        {
            ValidatePath(targetDirectory);
        }
        var args = importInPlace
            ? new List<string> { "--import-in-place", instanceName, payloadPath }
            : new List<string> { "--import", instanceName, targetDirectory, payloadPath };
        if (!importInPlace && format == RecoveryPointFormat.Vhdx) args.Add("--vhd");
        await EnsureSuccessAsync(new("wsl.exe", args, TimeSpan.FromMinutes(30)), ct, "import");
    }

    public async Task<bool> InstanceExistsAsync(string instanceName, CancellationToken ct = default) => await IsRegisteredAsync(instanceName, ct);
    public async Task<bool> IsRegisteredAsync(string instanceName, CancellationToken ct = default)
    {
        var r = await _process.RunAsync(new("wsl.exe", ["--list", "--quiet"], TimeSpan.FromSeconds(15)), ct);
        return r.ExitCode == 0 && r.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Any(x => StringComparer.OrdinalIgnoreCase.Equals(x.Trim(), instanceName));
    }
    public Task<RecoveryRegistration?> GetRegistrationAsync(string instanceName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
            if (root is null) return Task.FromResult<RecoveryRegistration?>(null);
            foreach (var id in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(id);
                if (key?.GetValue("DistributionName") is not string name || !StringComparer.OrdinalIgnoreCase.Equals(name, instanceName)
                    || key.GetValue("BasePath") is not string basePath || string.IsNullOrWhiteSpace(basePath)) continue;
                return Task.FromResult<RecoveryRegistration?>(new RecoveryRegistration(id, NormalizePath(basePath)));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
        catch (PlatformNotSupportedException) { }
        return Task.FromResult<RecoveryRegistration?>(null);
    }
    public async Task<bool> IsRunningAsync(string instanceName, CancellationToken ct = default)
    {
        var r = await _process.RunAsync(new("wsl.exe", ["--list", "--running", "--quiet"], TimeSpan.FromSeconds(15)), ct);
        return r.ExitCode == 0 && r.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Any(x => StringComparer.OrdinalIgnoreCase.Equals(x.Trim(), instanceName));
    }
    public Task StopAsync(string instanceName, CancellationToken ct = default) => EnsureSuccessAsync(new("wsl.exe", ["--terminate", instanceName], TimeSpan.FromMinutes(2)), ct, "stop");
    public Task StartAsync(string instanceName, CancellationToken ct = default) => EnsureSuccessAsync(new("wsl.exe", ["--distribution", instanceName, "--exec", "true"], TimeSpan.FromMinutes(2)), ct, "start");
    public async Task<bool> VerifyBootAsync(string instanceName, CancellationToken ct = default)
    {
        var result = await _process.RunAsync(new("wsl.exe", ["--distribution", instanceName, "--exec", "true"], TimeSpan.FromMinutes(2)), ct);
        return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled;
    }
    private async Task EnsureSuccessAsync(ProcessRequest request, CancellationToken ct, string operation)
    { var r = await _process.RunAsync(request, ct); if (r.ExitCode != 0 || r.TimedOut || r.Cancelled) throw new IOException($"WSL recovery {operation} failed."); }

    private static void ValidateInstanceName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)) throw new ArgumentException("Distribution name is invalid.", nameof(value));
    }
    private static void ValidatePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || !Path.IsPathFullyQualified(value))
            throw new ArgumentException("Recovery path must be a fully-qualified local path.", nameof(value));
    }
    private static string NormalizePath(string value) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    private static bool PathEquals(string left, string right) => StringComparer.OrdinalIgnoreCase.Equals(NormalizePath(left), NormalizePath(right));

    private const long ConservativeOfflineEstimateBytes = 1024L * 1024 * 1024;

    private async Task<long> EstimateFilesystemBytesAsync(string instanceName, CancellationToken ct)
    {
        try
        {
            var result = await _process.RunAsync(new("wsl.exe", ["--distribution", instanceName, "--exec", "sh", "-c", "df -B1 --output=used / | tail -n 1"], TimeSpan.FromSeconds(20)), ct);
            var value = result.StandardOutput.Trim();
            return result.ExitCode == 0 && long.TryParse(value, out var bytes) && bytes > 0 ? bytes : ConservativeOfflineEstimateBytes;
        }
        catch (IOException) { return ConservativeOfflineEstimateBytes; }
    }
}
