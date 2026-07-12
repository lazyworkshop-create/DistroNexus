using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Initial non-mutating host and instance checks. Raw command output never leaves these adapters.</summary>
public sealed class CapabilityHealthCheck : IHealthCheck
{
    public HealthCheckDescriptor Descriptor { get; } = new("host.prerequisites", HealthScope.Host, []);
    public Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var f = new List<HealthFinding>();
        foreach (var item in context.Host.Capabilities.Values.Concat(context.Host.OptionalDependencies.Values).OrderBy(x => x.Id))
        {
            var severity = item.Status switch { CapabilityStatus.Supported => HealthSeverity.Healthy, CapabilityStatus.RequiresUpdate => HealthSeverity.Warning, CapabilityStatus.RequiresElevation => HealthSeverity.Information, _ => HealthSeverity.Information };
            if (item.Id is CapabilityId.Wsl or CapabilityId.HostFacts || item.Status != CapabilityStatus.Supported)
            {
                // Do not offer an elevation repair for an arbitrary unsupported integration. These
                // two platform prerequisites have a fixed, reviewed Windows Features flow.
                var repair = item.Id is CapabilityId.Wsl &&
                    item.Status is CapabilityStatus.Unsupported or CapabilityStatus.RequiresElevation
                    ? "enable.windows-features" : null;
                f.Add(new HealthFinding($"capability.{item.Id}", severity, HealthScope.Host, item.Id.ToString(), item.ReasonCode, RepairId: repair, Evidence: item.Evidence));
            }
        }
        if (context.Host.Host.UpdateAvailable == true)
            f.Add(new HealthFinding("wsl.update.pending", HealthSeverity.Warning, HealthScope.Host, "WSL update available", "Update WSL from Windows Update or wsl.exe --update.", RepairId: "wsl.update"));
        var facts = context.Host.Host;
        f.Add(VersionFinding("host.wsl.version", "WSL version", facts.WslVersion, facts.WslVersion is null ? HealthSeverity.Information : HealthSeverity.Healthy,
            facts.WslVersion is null ? "WSL version was not reported by wsl.exe." : "WSL version was detected.", "wsl.update"));
        f.Add(VersionFinding("host.kernel.version", "WSL kernel version", facts.KernelVersion, facts.KernelVersion is null ? HealthSeverity.Warning : HealthSeverity.Healthy,
            facts.KernelVersion is null ? "WSL is installed but no kernel version was reported." : "WSL kernel version was detected.", facts.KernelVersion is null ? "wsl.update" : null));
        f.Add(VersionFinding("host.wslg.version", "WSLg version", facts.WslgVersion, facts.WslgVersion is null ? HealthSeverity.Information : HealthSeverity.Healthy,
            facts.WslgVersion is null ? "WSLg is not installed or its version was not reported." : "WSLg version was detected."));
        return Task.FromResult(new HealthCheckResult(Descriptor.Id, f, DateTimeOffset.UtcNow));
    }

    private static HealthFinding VersionFinding(string id, string title, Version? version, HealthSeverity severity, string detail, string? repairId = null) =>
        new(id, severity, HealthScope.Host, title, detail, RepairId: repairId,
            Evidence: version is null ? null : new Dictionary<string, string> { ["version"] = version.ToString() });
}

public sealed class GlobalConfigurationHealthCheck : IHealthCheck
{
    private readonly IWslConfigurationService _config;
    public GlobalConfigurationHealthCheck(IWslConfigurationService config) => _config = config;
    public HealthCheckDescriptor Descriptor { get; } = new("host.wslconfig", HealthScope.Host, [CapabilityId.Wsl]);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var doc = await _config.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = doc.Diagnostics.Select(d => new HealthFinding($"wslconfig.{d.Code}.{d.Line}", d.Severity == ConfigurationDiagnosticSeverity.Error ? HealthSeverity.Warning : HealthSeverity.Information, HealthScope.Host,
            "WSL configuration issue", d.Message, RepairId: "config.global.known-values", Evidence: new Dictionary<string, string> { ["line"] = d.Line.ToString(), ["code"] = d.Code })).ToList();
        if (doc.UnknownKeyCount > 0) findings.Add(new HealthFinding("wslconfig.unknown", HealthSeverity.Information, HealthScope.Host, "Unknown .wslconfig settings", $"{doc.UnknownKeyCount} settings are preserved but not managed by this version."));
        if (findings.Count == 0) findings.Add(new HealthFinding("wslconfig.healthy", HealthSeverity.Healthy, HealthScope.Host, "Global WSL configuration", "The managed .wslconfig values are valid."));
        return new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow);
    }
}

public sealed class DistributionConfigurationHealthCheck : IHealthCheck
{
    private readonly IDistributionConfigurationService _config;
    public DistributionConfigurationHealthCheck(IDistributionConfigurationService config) => _config = config;
    public HealthCheckDescriptor Descriptor { get; } = new("instance.wslconf", HealthScope.Instance, [CapabilityId.Wsl]);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var result = new List<HealthFinding>();
        foreach (var instance in context.Instances)
        {
            if (!instance.IsRunning)
            {
                // A registered but stopped distribution is still in scope.  It must not silently
                // disappear from configuration health merely because reading /etc/wsl.conf would
                // start it or otherwise change runtime state.
                result.Add(new HealthFinding($"wslconf.{instance.Name}.unavailable", HealthSeverity.Information, HealthScope.Instance,
                    "Distribution configuration unavailable", "The distribution is stopped; /etc/wsl.conf was not read so Health Center would not start it.", instance.Name,
                    Evidence: new Dictionary<string, string> { ["instanceState"] = string.IsNullOrWhiteSpace(instance.State) ? "unknown" : instance.State, ["probe"] = "not-started" }));
                continue;
            }
            var doc = await _config.ReadAsync(instance.Name, cancellationToken).ConfigureAwait(false);
            foreach (var d in doc.Diagnostics) result.Add(new HealthFinding($"wslconf.{instance.Name}.{d.Code}.{d.Line}", d.Severity == ConfigurationDiagnosticSeverity.Error ? HealthSeverity.Warning : HealthSeverity.Information, HealthScope.Instance, "Distribution configuration issue", d.Message, instance.Name, "config.instance.known-values", new Dictionary<string, string> { ["line"] = d.Line.ToString() }));
            if (doc.Diagnostics.Count == 0) result.Add(new HealthFinding($"wslconf.{instance.Name}.healthy", HealthSeverity.Healthy, HealthScope.Instance, "Distribution configuration", "The managed /etc/wsl.conf values are valid.", instance.Name));
        }
        return new HealthCheckResult(Descriptor.Id, result, DateTimeOffset.UtcNow);
    }
}

public sealed class StorageHealthCheck : IHealthCheck
{
    public HealthCheckDescriptor Descriptor { get; } = new("host.storage", HealthScope.Host, []);
    public Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var output = new List<HealthFinding>();
        foreach (var drive in DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == DriveType.Fixed))
        {
            var ratio = drive.TotalSize == 0 ? 0 : (double)drive.AvailableFreeSpace / drive.TotalSize;
            var severity = ratio < .05 ? HealthSeverity.Critical : ratio < .10 ? HealthSeverity.Warning : HealthSeverity.Healthy;
            output.Add(new HealthFinding($"disk.{drive.Name.TrimEnd('\\')}", severity, HealthScope.Host, "Host disk space", $"{drive.Name} has {ratio:P0} free space.", Evidence: new Dictionary<string, string> { ["freeBytes"] = drive.AvailableFreeSpace.ToString(), ["totalBytes"] = drive.TotalSize.ToString() }));
        }
        return Task.FromResult(new HealthCheckResult(Descriptor.Id, output, DateTimeOffset.UtcNow));
    }
}

public sealed class BackupHealthCheck : IHealthCheck
{
    private readonly IBackupService _backup;
    public BackupHealthCheck(IBackupService backup) => _backup = backup;
    public HealthCheckDescriptor Descriptor { get; } = new("host.backups", HealthScope.Host, []);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var findings = new List<HealthFinding>();
        foreach (var schedule in await _backup.GetSchedulesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!Directory.Exists(schedule.Destination)) findings.Add(new HealthFinding($"backup.{schedule.Name}.destination", HealthSeverity.Warning, HealthScope.Instance, "Backup destination unavailable", "The scheduled backup destination does not exist.", schedule.Name));
        }
        if (findings.Count == 0) findings.Add(new HealthFinding("backup.healthy", HealthSeverity.Healthy, HealthScope.Host, "Backup schedules", "Configured backup destinations are available."));
        return new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow);
    }
}

public sealed class IntegrationHealthCheck : IHealthCheck
{
    public HealthCheckDescriptor Descriptor { get; } = new("host.integrations", HealthScope.Host, []);
    public Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var ids = new[] { CapabilityId.DockerDesktop, CapabilityId.Podman, CapabilityId.UsbIpd, CapabilityId.Wslg, CapabilityId.WindowsTerminal, CapabilityId.VisualStudioCode, CapabilityId.Systemd };
        var findings = ids.Select(id => context.Host.OptionalDependencies.TryGetValue(id, out var value) || context.Host.Capabilities.TryGetValue(id, out value)
            ? new HealthFinding($"integration.{id}", value.IsSupported ? HealthSeverity.Healthy : HealthSeverity.Information, HealthScope.Host, id.ToString(), value.ReasonCode, Evidence: value.Evidence)
            : new HealthFinding($"integration.{id}", HealthSeverity.Information, HealthScope.Host, id.ToString(), "Capability was not probed.")).ToArray();
        return Task.FromResult(new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow));
    }
}

public sealed class NetworkHealthCheck : IHealthCheck
{
    public HealthCheckDescriptor Descriptor { get; } = new("host.network", HealthScope.Host, [CapabilityId.Wsl]);
    public Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var findings = new List<HealthFinding>();
        foreach (var id in new[] { CapabilityId.MirroredNetworking, CapabilityId.ConfigDnsTunneling, CapabilityId.ConfigFirewall, CapabilityId.ConfigAutoProxy })
        {
            if (!context.Host.Capabilities.TryGetValue(id, out var state)) continue;
            findings.Add(new HealthFinding($"network.{id}", state.IsSupported ? HealthSeverity.Healthy : HealthSeverity.Information, HealthScope.Host,
                id.ToString(), state.ReasonCode, Evidence: state.Evidence));
        }
        if (findings.Count == 0) findings.Add(new HealthFinding("network.unavailable", HealthSeverity.Information, HealthScope.Host, "Network diagnostics", "Detailed network probes are unavailable until WSL is detected."));
        return Task.FromResult(new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow));
    }
}

public sealed class SystemdHealthCheck : IHealthCheck
{
    private readonly IPlatformCapabilityService _capabilities;
    public SystemdHealthCheck(IPlatformCapabilityService capabilities) => _capabilities = capabilities;
    public HealthCheckDescriptor Descriptor { get; } = new("instance.systemd", HealthScope.Instance, [CapabilityId.Systemd]);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var findings = new List<HealthFinding>();
        foreach (var instance in context.Instances.Where(x => x.IsRunning))
        {
            var snapshot = await _capabilities.GetInstanceSnapshotAsync(instance.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
            var state = snapshot.Capabilities.TryGetValue(CapabilityId.InstanceSystemd, out var item) ? item : null;
            findings.Add(new HealthFinding($"systemd.{instance.Name}", state?.IsSupported == true ? HealthSeverity.Healthy : HealthSeverity.Information,
                HealthScope.Instance, "systemd", state?.ReasonCode ?? "systemd state unavailable", instance.Name, Evidence: state?.Evidence));
        }
        return new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow);
    }
}

public sealed class TemplateHealthCheck : IHealthCheck
{
    private readonly ITemplateService _templates;
    private readonly ITemplateRuntimePreflightEvaluator? _runtimePreflight;
    public TemplateHealthCheck(ITemplateService templates, ITemplateRuntimePreflightEvaluator? runtimePreflight = null) => (_templates, _runtimePreflight) = (templates, runtimePreflight);
    public HealthCheckDescriptor Descriptor { get; } = new("instance.templates", HealthScope.Instance, []);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var history = await _templates.GetApplicationHistoryAsync().ConfigureAwait(false);
        var findings = history.Where(x => !x.Success).Select(x => new HealthFinding($"template.{x.Id}", HealthSeverity.Warning, HealthScope.Instance,
            "Template application failed", x.Errors.Count == 0 ? "A recorded template application did not complete." : SensitiveDataRedactor.Redact(string.Join("; ", x.Errors)), x.InstanceName)).ToList();
        foreach (var record in history.Where(x => x.Success))
        {
            var snapshot = record.DeclaredHealthSnapshot;
            if (snapshot is null)
            {
                findings.Add(new HealthFinding($"template.{record.Id}.declaration", HealthSeverity.Information, HealthScope.Instance, "Template declaration snapshot unavailable", "This historical application predates declaration health snapshots.", record.InstanceName));
            }
            else if (!snapshot.IsHealthy)
            {
                findings.Add(new HealthFinding($"template.{record.Id}.declaration", HealthSeverity.Warning, HealthScope.Instance,
                    "Template declaration health check failed", SensitiveDataRedactor.Redact(string.Join("; ", snapshot.ValidationErrors)), record.InstanceName,
                    Evidence: new Dictionary<string, string> { ["templateId"] = record.TemplateId, ["templateVersion"] = snapshot.TemplateVersion, ["requiredPreflightCheckCount"] = snapshot.RequiredPreflightIds.Count.ToString() }));
            }
            else if (snapshot.ExpectedScriptIds is { Count: > 0 })
            {
                var applied = snapshot.AppliedScriptIds ?? [];
                var missing = snapshot.ExpectedScriptIds.Except(applied, StringComparer.Ordinal).ToArray();
                if (missing.Length != 0)
                    findings.Add(new HealthFinding($"template.{record.Id}.postinstall", HealthSeverity.Warning, HealthScope.Instance,
                        "Template post-install contract drift", "Declared template scripts were not recorded as applied: " + string.Join(", ", missing) + ".", record.InstanceName,
                        Evidence: new Dictionary<string, string> { ["templateId"] = record.TemplateId, ["templateVersion"] = snapshot.TemplateVersion, ["missingScriptCount"] = missing.Length.ToString() }));
            }
            if (_runtimePreflight is not null && snapshot?.RuntimePreflightContracts is { Count: > 0 })
            {
                var instance = context.Instances.FirstOrDefault(x => x.Name.Equals(record.InstanceName, StringComparison.OrdinalIgnoreCase));
                if (instance is null || !instance.IsRunning)
                {
                    var availability = instance is null
                        ? "The recorded distribution is no longer installed; runtime preflight was not run."
                        : "The recorded distribution is stopped; runtime preflight is available after it is started.";
                    foreach (var contract in snapshot.RuntimePreflightContracts)
                    {
                        findings.Add(new HealthFinding($"template.{record.Id}.preflight.{contract.Id}", HealthSeverity.Information, HealthScope.Instance,
                            "Template runtime preflight unavailable", availability, record.InstanceName,
                            Evidence: new Dictionary<string, string> { ["preflightId"] = contract.Id, ["state"] = "unavailable", ["reason"] = instance is null ? "instance-missing" : "instance-stopped", ["templateId"] = record.TemplateId }));
                    }
                    continue;
                }
                foreach (var observed in await _runtimePreflight.EvaluateAsync(record, cancellationToken).ConfigureAwait(false))
                {
                    var severity = observed.State.Equals("healthy", StringComparison.OrdinalIgnoreCase) ? HealthSeverity.Healthy
                        : observed.Required ? HealthSeverity.Warning : HealthSeverity.Information;
                    findings.Add(new HealthFinding($"template.{record.Id}.preflight.{observed.Id}", severity, HealthScope.Instance,
                        "Template runtime preflight", SensitiveDataRedactor.Redact(observed.Detail), record.InstanceName,
                        Evidence: new Dictionary<string, string> { ["preflightId"] = observed.Id, ["state"] = observed.State, ["templateId"] = record.TemplateId }));
                }
            }
        }
        if (findings.Count == 0) findings.Add(new HealthFinding("templates.healthy", HealthSeverity.Healthy, HealthScope.Host, "Template health", "No failed template applications are recorded."));
        return new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow);
    }
}
