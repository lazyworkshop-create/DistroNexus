using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed partial class PlatformCapabilityService : IPlatformCapabilityService
{
    private static readonly TimeSpan StableLifetime = Timeout.InfiniteTimeSpan;
    private static readonly TimeSpan DependencyLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VolatileLifetime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan NegativeLifetime = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly IProcessRunner _runner;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<FlightKey, Lazy<Task<object>>> _inflight = new();
    private readonly ConcurrentDictionary<string, long> _epochs = new(StringComparer.OrdinalIgnoreCase);

    public PlatformCapabilityService(IProcessRunner runner, TimeProvider? timeProvider = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PlatformCapabilitySnapshot> GetHostSnapshotAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var host = await GetCachedAsync("host", CapabilityCacheKind.Stable, forceRefresh, ProbeHostAsync, cancellationToken).ConfigureAwait(false);
        var dependencies = new Dictionary<CapabilityId, CapabilityResult>();
        foreach (var descriptor in DependencyCommands)
        {
            dependencies[descriptor.Id] = await GetCachedAsync("dependency:" + descriptor.Id, CapabilityCacheKind.Dependency, forceRefresh,
                ct => ProbeDependencyAsync(descriptor, ct), cancellationToken).ConfigureAwait(false);
        }
        var capabilities = host.Capabilities.ToDictionary();
        capabilities[CapabilityId.UsbIp] = dependencies[CapabilityId.UsbIpd] with { Id = CapabilityId.UsbIp };
        return host with
        {
            Capabilities = PlatformCapabilitySnapshot.ReadOnly(capabilities),
            OptionalDependencies = PlatformCapabilitySnapshot.ReadOnly(dependencies),
            RefreshedAt = new[] { host.RefreshedAt }.Concat(dependencies.Values.Select(x => x.CheckedAt)).Max()
        };
    }

    public Task<InstanceCapabilitySnapshot> GetInstanceSnapshotAsync(string instanceName, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        return GetCachedAsync("instance:" + instanceName, CapabilityCacheKind.Volatile, forceRefresh,
            ct => ProbeInstanceAsync(instanceName, ct), cancellationToken);
    }

    public void InvalidateHostCapabilities() => Invalidate("host");

    public void InvalidateOptionalDependency(CapabilityId dependency)
    {
        if (IsDependency(dependency)) Invalidate("dependency:" + dependency);
    }

    public void InvalidateInstance(string instanceName)
    {
        if (!string.IsNullOrWhiteSpace(instanceName)) Invalidate("instance:" + instanceName);
    }

    public void InvalidateAll()
    {
        foreach (var key in _cache.Keys.Concat(_epochs.Keys).Distinct(StringComparer.OrdinalIgnoreCase)) Invalidate(key);
    }

    private void Invalidate(string key)
    {
        _epochs.AddOrUpdate(key, 1, static (_, current) => checked(current + 1));
        _cache.TryRemove(key, out _);
    }

    private async Task<T> GetCachedAsync<T>(string key, CapabilityCacheKind kind, bool forceRefresh,
        Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        if (forceRefresh) Invalidate(key);
        var epoch = _epochs.GetOrAdd(key, 0);
        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && cached.Epoch == epoch && !IsExpired(cached, now, kind)) return (T)cached.Value;

        var flightKey = new FlightKey(key.ToUpperInvariant(), epoch);
        var lazy = _inflight.GetOrAdd(flightKey, _ => new Lazy<Task<object>>(
            async () =>
            {
                var value = await factory(CancellationToken.None).ConfigureAwait(false);
                if (_epochs.GetOrAdd(key, 0) == epoch)
                    _cache[key] = new CacheEntry(value!, _timeProvider.GetUtcNow(), IsNegative(value), epoch);
                return (object)value!;
            },
            LazyThreadSafetyMode.ExecutionAndPublication));
        var flight = lazy.Value;
        _ = flight.ContinueWith((_, state) =>
        {
            var tuple = ((PlatformCapabilityService Owner, FlightKey Key, Lazy<Task<object>> Lazy))state!;
            tuple.Owner._inflight.TryRemove(new KeyValuePair<FlightKey, Lazy<Task<object>>>(tuple.Key, tuple.Lazy));
        }, (this, flightKey, lazy), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            var value = (T)await flight.WaitAsync(cancellationToken).ConfigureAwait(false);
            return value;
        }
        finally
        {
            if (flight.IsCompleted) _inflight.TryRemove(new KeyValuePair<FlightKey, Lazy<Task<object>>>(flightKey, lazy));
        }
    }

    private bool IsExpired(CacheEntry entry, DateTimeOffset now, CapabilityCacheKind kind)
    {
        var lifetime = entry.IsNegative ? NegativeLifetime : kind switch
        {
            CapabilityCacheKind.Stable => StableLifetime,
            CapabilityCacheKind.Dependency => DependencyLifetime,
            _ => VolatileLifetime
        };
        return lifetime != Timeout.InfiniteTimeSpan && now - entry.CreatedAt >= lifetime;
    }

    private static bool IsNegative<T>(T value) => value switch
    {
        PlatformCapabilitySnapshot s => s.Capabilities.TryGetValue(CapabilityId.Wsl, out var wsl) && wsl.Status == CapabilityStatus.Unknown,
        InstanceCapabilitySnapshot s => s.Capabilities.Values.Any(x => x.Status == CapabilityStatus.Unknown),
        _ => false
    };

    private async Task<PlatformCapabilitySnapshot> ProbeHostAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var versionResult = await RunAsync("wsl.exe", ["--version"], ct).ConfigureAwait(false);
        var statusResult = await RunAsync("wsl.exe", ["--status"], ct).ConfigureAwait(false);
        var helpResult = await RunAsync("wsl.exe", ["--help"], ct).ConfigureAwait(false);
        var manageHelpResult = await RunAsync("wsl.exe", ["--manage", "--help"], ct).ConfigureAwait(false);
        var wsl = ClassifyProcess(CapabilityId.Wsl, versionResult, now, CapabilitySource.WslCli);
        var versions = ParseWslVersions(versionResult.StandardOutput);
        var updateAvailable = ParseUpdateAvailable(statusResult.StandardOutput);
        if (updateAvailable == true && wsl.Status == CapabilityStatus.Supported)
            wsl = Result(CapabilityId.Wsl, CapabilityStatus.RequiresUpdate, "Capability.Wsl.UpdateAvailable", CapabilitySource.WslCli, now, versions.Wsl);
        var hostFacts = new HostPlatformFacts(
            GetWindowsEdition(),
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture.ToString(),
            await IsElevatedAsync(ct).ConfigureAwait(false),
            versions.Wsl is null ? (statusResult.ExitCode == 0 ? "WindowsInbox" : null) : "MicrosoftStore",
            versions.Wsl,
            versions.Kernel,
            versions.Wslg,
            updateAvailable);

        var capabilities = new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Wsl] = wsl };
        capabilities[CapabilityId.SparseVhd] = FeatureFromHelp(CapabilityId.SparseVhd, wsl, manageHelpResult, "--set-sparse", now);
        capabilities[CapabilityId.VhdExport] = FeatureFromHelp(CapabilityId.VhdExport, wsl, helpResult, "--vhd", now);
        capabilities[CapabilityId.ImportInPlace] = FeatureFromHelp(CapabilityId.ImportInPlace, wsl, helpResult, "--import-in-place", now);
        capabilities[CapabilityId.MirroredNetworking] = Result(CapabilityId.MirroredNetworking, CapabilityStatus.Unknown,
            "Capability.MirroredNetworking.RequiresVersionMatrix", CapabilitySource.WslCli, now, versions.Wsl);
        // The WSL CLI version is the authoritative host contract for whether wsl.conf may
        // enable systemd.  This is deliberately separate from the volatile systemctl probe:
        // a supported WSL 2 distribution can be configured while systemd is currently off.
        capabilities[CapabilityId.Systemd] = SystemdEnablementFromWsl(wsl, versions.Wsl, now);
        // These settings have no stable CLI feature token.  Keep their state explicitly Unknown until
        // a version matrix or runtime probe is available; consumers must not infer support from WSL itself.
        foreach (var id in new[] { CapabilityId.ConfigDnsTunneling, CapabilityId.ConfigFirewall, CapabilityId.ConfigAutoProxy,
                     CapabilityId.ConfigHostAddressLoopback, CapabilityId.ConfigIgnoredPorts, CapabilityId.ConfigBestEffortDnsParsing,
                     CapabilityId.ConfigProxyTimeout, CapabilityId.ConfigAutoMemoryReclaim })
            capabilities[id] = Result(id, CapabilityStatus.Unknown, "Capability.Configuration.RequiresVersionMatrix", CapabilitySource.WslCli, now, versions.Wsl);
        capabilities[CapabilityId.Wslg] = versions.Wslg is not null
            ? Result(CapabilityId.Wslg, CapabilityStatus.Supported, "Capability.Wslg.Supported", CapabilitySource.WslCli, now, versions.Wslg)
            : Result(CapabilityId.Wslg, wsl.Status == CapabilityStatus.Supported ? CapabilityStatus.Unavailable : wsl.Status, "Capability.Wslg.NotReported", CapabilitySource.WslCli, now);
        capabilities[CapabilityId.GpuCompute] = Result(CapabilityId.GpuCompute, CapabilityStatus.Unknown, "Capability.Gpu.RequiresRuntimeProbe", CapabilitySource.WslCli, now);

        return new PlatformCapabilitySnapshot(hostFacts, PlatformCapabilitySnapshot.ReadOnly(capabilities),
            PlatformCapabilitySnapshot.ReadOnly(new Dictionary<CapabilityId, CapabilityResult>()), now);
    }

    private async Task<InstanceCapabilitySnapshot> ProbeInstanceAsync(string name, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        var list = await RunAsync("wsl.exe", ["--list", "--verbose"], ct, ProcessOutputEncoding.Utf16LittleEndian).ConfigureAwait(false);
        var wslVersion = ParseInstanceWslVersion(list.StandardOutput, name);
        var identity = await RunAsync("wsl.exe", ["--distribution", name, "--exec", "cat", "/etc/os-release"], ct).ConfigureAwait(false);
        var systemd = await RunAsync("wsl.exe", ["--distribution", name, "--exec", "systemctl", "is-system-running"], ct).ConfigureAwait(false);
        var distro = ParseOsRelease(identity.StandardOutput);
        var systemdStatus = ClassifySystemd(systemd);
        var facts = new InstancePlatformFacts(name, wslVersion, distro.Id, distro.Version, systemdStatus.Available, systemdStatus.Running);
        var values = new Dictionary<CapabilityId, CapabilityResult>
        {
            [CapabilityId.InstanceWslVersion] = wslVersion is null
                ? Result(CapabilityId.InstanceWslVersion, CapabilityStatus.Unknown, "Capability.Instance.VersionMalformed", CapabilitySource.WslCli, now)
                : Result(CapabilityId.InstanceWslVersion, CapabilityStatus.Supported, "Capability.Instance.VersionDetected", CapabilitySource.WslCli, now, evidence: new Dictionary<string,string>{{"wslVersion", wslVersion.Value.ToString()}}),
            [CapabilityId.DistributionIdentity] = identity.ExitCode == 0 && distro.Id is not null
                ? Result(CapabilityId.DistributionIdentity, CapabilityStatus.Supported, "Capability.Instance.IdentityDetected", CapabilitySource.InstanceCli, now, evidence: new Dictionary<string,string>{{"id", distro.Id}, {"version", distro.Version ?? string.Empty}})
                : ClassifyProcess(CapabilityId.DistributionIdentity, identity, now, CapabilitySource.InstanceCli),
            [CapabilityId.InstanceSystemd] = Result(CapabilityId.InstanceSystemd, systemdStatus.Status, systemdStatus.Reason, CapabilitySource.InstanceCli, now)
        };
        return new InstanceCapabilitySnapshot(facts, PlatformCapabilitySnapshot.ReadOnly(values), now);
    }

    private async Task<CapabilityResult> ProbeDependencyAsync(DependencyProbe descriptor, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        ProcessResult result;
        if (descriptor.Id == CapabilityId.TaskScheduler)
        {
            result = await RunAsync(descriptor.Executable, descriptor.VersionArguments, ct).ConfigureAwait(false);
        }
        else
        {
            var located = await RunAsync("where.exe", [descriptor.Executable], ct).ConfigureAwait(false);
            if (ContainsPermissionDenied(located)) return Result(descriptor.Id, CapabilityStatus.RequiresElevation, "Capability.Dependency.RequiresElevation", CapabilitySource.DependencyCli, now);
            if (located.ExitCode != 0 || string.IsNullOrWhiteSpace(located.StandardOutput))
                return Result(descriptor.Id, located.Failure == ProcessFailureKind.StartFailed ? CapabilityStatus.Unknown : CapabilityStatus.Unavailable,
                    located.Failure == ProcessFailureKind.StartFailed ? "Capability.Dependency.ProbeUnavailable" : "Capability.Dependency.NotInstalled", CapabilitySource.DependencyCli, now);
            result = await RunAsync(descriptor.Executable, descriptor.VersionArguments, ct).ConfigureAwait(false);
        }

        if (ContainsPermissionDenied(result)) return Result(descriptor.Id, CapabilityStatus.RequiresElevation, "Capability.Dependency.RequiresElevation", CapabilitySource.DependencyCli, now);
        if (result.ExitCode != 0) return Result(descriptor.Id, CapabilityStatus.Unknown, "Capability.Dependency.VersionProbeFailed", CapabilitySource.DependencyCli, now);
        if (descriptor.Id == CapabilityId.TaskScheduler)
            return Result(descriptor.Id, CapabilityStatus.Supported, "Capability.TaskScheduler.Available", CapabilitySource.DependencyCli, now);
        var version = ParseFirstVersion(result.StandardOutput + "\n" + result.StandardError);
        if (version is null) return Result(descriptor.Id, CapabilityStatus.Unknown, "Capability.Dependency.VersionMalformed", CapabilitySource.DependencyCli, now);
        if (descriptor.Id == CapabilityId.UsbIpd)
            return Result(descriptor.Id, CapabilityStatus.Unknown, "Capability.UsbIpd.VersionRequiresValidation", CapabilitySource.DependencyCli, now, version,
                evidence: new Dictionary<string, string> { ["product"] = descriptor.Executable });
        return Result(descriptor.Id, CapabilityStatus.Supported, "Capability.Dependency.VersionDetected", CapabilitySource.DependencyCli, now, version,
            evidence: new Dictionary<string, string> { ["product"] = descriptor.Executable });
    }

    private async Task<bool> IsElevatedAsync(CancellationToken ct)
    {
        var result = await RunAsync("whoami.exe", ["/groups", "/fo", "csv", "/nh"], ct).ConfigureAwait(false);
        return result.ExitCode == 0 && result.StandardOutput.Contains("S-1-16-12288", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken ct,
        ProcessOutputEncoding encoding = ProcessOutputEncoding.Utf8)
    {
        try
        {
            return await _runner.RunAsync(new ProcessRequest(fileName, arguments, ProbeTimeout, 256 * 1024, 64 * 1024,
                OutputEncoding: encoding), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProcessResult(null, string.Empty, string.Empty, TimeSpan.Zero, false, false, false, null,
                ProcessFailureKind.StartFailed, "Unexpected probe failure: " + ex.GetType().Name);
        }
    }

    private static CapabilityResult ClassifyProcess(CapabilityId id, ProcessResult result, DateTimeOffset now, CapabilitySource source)
    {
        if (result.Cancelled) return Result(id, CapabilityStatus.Unknown, "Capability.Probe.Cancelled", source, now);
        if (result.TimedOut) return Result(id, CapabilityStatus.Unknown, "Capability.Probe.TimedOut", source, now);
        if (result.Failure == ProcessFailureKind.StartFailed)
            return Result(id, result.FailureMessage?.StartsWith("Unexpected probe failure:", StringComparison.Ordinal) == true
                ? CapabilityStatus.Unknown : CapabilityStatus.Unavailable,
                result.FailureMessage?.StartsWith("Unexpected probe failure:", StringComparison.Ordinal) == true
                    ? "Capability.Probe.UnexpectedFailure" : "Capability.Probe.ExecutableMissing", source, now);
        if (result.ExitCode == 0) return Result(id, CapabilityStatus.Supported, "Capability.Probe.Supported", source, now);
        if (ContainsPermissionDenied(result)) return Result(id, CapabilityStatus.RequiresElevation, "Capability.Probe.RequiresElevation", source, now);
        return Result(id, CapabilityStatus.Unknown, "Capability.Probe.Failed", source, now);
    }

    private static CapabilityResult FeatureFromHelp(CapabilityId id, CapabilityResult wsl, ProcessResult help, string token, DateTimeOffset now)
    {
        if (wsl.Status != CapabilityStatus.Supported) return Result(id, wsl.Status, "Capability.Feature.WslUnavailable", CapabilitySource.WslCli, now);
        if (help.ExitCode != 0) return ClassifyProcess(id, help, now, CapabilitySource.WslCli);
        if (help.StandardOutput.Contains(token, StringComparison.OrdinalIgnoreCase))
            return Result(id, CapabilityStatus.Supported, "Capability.Feature.CliSupported", CapabilitySource.WslCli, now);
        var recognizableHelp = help.StandardOutput.Contains("Usage", StringComparison.OrdinalIgnoreCase) ||
            help.StandardOutput.Contains("--install", StringComparison.OrdinalIgnoreCase);
        return Result(id, recognizableHelp ? CapabilityStatus.Unsupported : CapabilityStatus.Unknown,
            recognizableHelp ? "Capability.Feature.NotAdvertisedByCli" : "Capability.Feature.MalformedHelp", CapabilitySource.WslCli, now);
    }

    private static CapabilityResult SystemdEnablementFromWsl(CapabilityResult wsl, Version? version, DateTimeOffset now)
    {
        var minimum = new Version(0, 67, 6);
        if (wsl.Status != CapabilityStatus.Supported)
            return Result(CapabilityId.Systemd, wsl.Status, "Capability.Systemd.WslUnavailable", CapabilitySource.WslCli, now, version, minimum);
        if (version is null)
            return Result(CapabilityId.Systemd, CapabilityStatus.Unknown, "Capability.Systemd.VersionUnknown", CapabilitySource.WslCli, now, null, minimum);
        return version >= minimum
            ? Result(CapabilityId.Systemd, CapabilityStatus.Supported, "Capability.Systemd.EnablementSupported", CapabilitySource.WslCli, now, version, minimum)
            : Result(CapabilityId.Systemd, CapabilityStatus.Unsupported, "Capability.Systemd.VersionTooLow", CapabilitySource.WslCli, now, version, minimum);
    }

    private static CapabilityResult Result(CapabilityId id, CapabilityStatus status, string reason, CapabilitySource source,
        DateTimeOffset now, Version? version = null, Version? minimum = null, IReadOnlyDictionary<string, string>? evidence = null) =>
        new(id, status, reason, source, now, version, minimum, evidence);

    private static bool ContainsPermissionDenied(ProcessResult result) =>
        (result.StandardError + result.StandardOutput).Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
        (result.StandardError + result.StandardOutput).Contains("permission denied", StringComparison.OrdinalIgnoreCase);

    private static (Version? Wsl, Version? Kernel, Version? Wslg) ParseWslVersions(string output)
    {
        Version? Find(string key)
        {
            var match = Regex.Match(output, $@"(?im)^\s*{Regex.Escape(key)}[^:]*:\s*v?(?<v>\d+(?:\.\d+){{1,3}})");
            return match.Success && Version.TryParse(match.Groups["v"].Value, out var value) ? value : null;
        }
        return (Find("WSL version"), Find("Kernel version"), Find("WSLg version"));
    }

    private static bool? ParseUpdateAvailable(string output) =>
        Regex.IsMatch(output, @"update\s+(?:is\s+)?available", RegexOptions.IgnoreCase) ? true :
        output.Contains("most recent version", StringComparison.OrdinalIgnoreCase) ? false : null;

    private static int? ParseInstanceWslVersion(string output, string name)
    {
        foreach (var raw in output.Replace("\0", string.Empty).Split('\n'))
        {
            var match = Regex.Match(raw.Trim(), @"^\*?\s*(?<name>.+?)\s+(?<state>Running|Stopped)\s+(?<version>[12])\s*$", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                return int.Parse(match.Groups["version"].Value);
        }
        return null;
    }

    private static (string? Id, string? Version) ParseOsRelease(string output)
    {
        string? Read(string key) => output.Split('\n')
            .Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith(key + "=", StringComparison.Ordinal))?
            [(key.Length + 1)..].Trim().Trim('"');
        return (Read("ID"), Read("VERSION_ID"));
    }

    private static (CapabilityStatus Status, string Reason, bool? Available, bool? Running) ClassifySystemd(ProcessResult result)
    {
        if (ContainsPermissionDenied(result)) return (CapabilityStatus.RequiresElevation, "Capability.Instance.SystemdPermissionDenied", true, null);
        var text = (result.StandardOutput + "\n" + result.StandardError).Trim();
        if (text.Contains("no distribution with the supplied name", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("distribution was not found", StringComparison.OrdinalIgnoreCase))
            return (CapabilityStatus.Unavailable, "Capability.Instance.DistributionAbsent", null, null);
        if (text.Contains("not been booted with systemd", StringComparison.OrdinalIgnoreCase))
            return (CapabilityStatus.Unsupported, "Capability.Instance.SystemdDisabled", false, false);
        if (text.Contains("systemctl: not found", StringComparison.OrdinalIgnoreCase) || text.Contains("systemctl: command not found", StringComparison.OrdinalIgnoreCase))
            return (CapabilityStatus.Unavailable, "Capability.Instance.SystemdUnavailable", false, false);
        var state = result.StandardOutput.Trim().ToLowerInvariant();
        if (state is "running" or "degraded") return (CapabilityStatus.Supported, "Capability.Instance.SystemdRunning", true, true);
        if (state is "offline" or "stopping" or "maintenance" or "initializing" or "starting")
            return (CapabilityStatus.Supported, "Capability.Instance.SystemdNotRunning", true, false);
        if (result.ExitCode == 0 && string.IsNullOrWhiteSpace(state))
            return (CapabilityStatus.Unknown, "Capability.Instance.SystemdMalformed", null, null);
        return (CapabilityStatus.Unknown, "Capability.Instance.SystemdProbeFailed", null, null);
    }

    private static bool IsDependency(CapabilityId id) => DependencyCommands.Any(x => x.Id == id);

    private static Version? ParseFirstVersion(string output)
    {
        var match = Regex.Match(output, @"(?<!\d)(?<v>\d+(?:\.\d+){1,3})(?!\d)");
        return match.Success && Version.TryParse(match.Groups["v"].Value, out var version) ? version : null;
    }

    private static string GetWindowsEdition()
    {
        if (!OperatingSystem.IsWindows()) return RuntimeInformation.OSDescription;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("ProductName") as string
                ?? key?.GetValue("EditionID") as string
                ?? RuntimeInformation.OSDescription;
        }
        catch
        {
            return RuntimeInformation.OSDescription;
        }
    }

    private static readonly DependencyProbe[] DependencyCommands =
    [
        new(CapabilityId.WindowsTerminal, "wt.exe", ["--version"]),
        new(CapabilityId.VisualStudioCode, "code.cmd", ["--version"]),
        new(CapabilityId.DockerDesktop, "com.docker.cli.exe", ["-Version"]),
        new(CapabilityId.Podman, "podman.exe", ["--version"]),
        new(CapabilityId.UsbIpd, "usbipd.exe", ["--version"]),
        new(CapabilityId.TaskScheduler, "schtasks.exe", ["/Query", "/FO", "CSV", "/NH"])
    ];

    private sealed record DependencyProbe(CapabilityId Id, string Executable, IReadOnlyList<string> VersionArguments);

    private sealed record CacheEntry(object Value, DateTimeOffset CreatedAt, bool IsNegative, long Epoch);
    private readonly record struct FlightKey(string Key, long Epoch);
}
