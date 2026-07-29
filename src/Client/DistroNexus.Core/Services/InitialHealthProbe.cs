using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Initial B checks consume this fixture-friendly adapter; it does not make network or privileged repair calls.</summary>
public sealed class DefaultHealthProbe : IHealthProbe
{
    private readonly IBackupHealthSource _backups;
    private readonly ITemplateService _templates;
    private readonly IHealthRuntimeAdapter _runtime;
    public DefaultHealthProbe(IBackupHealthSource backups, ITemplateService templates, IHealthRuntimeAdapter runtime) => (_backups, _templates, _runtime) = (backups, templates, runtime);
    public async Task<HealthProbeSnapshot> ProbeAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var backups = await _backups.GetHealthAsync(cancellationToken).ConfigureAwait(false);
        var templates = new Dictionary<string, TemplateHealthState>(StringComparer.Ordinal);
        foreach (var record in await _templates.GetApplicationHistoryAsync().ConfigureAwait(false))
        {
            // Application history is immutable evidence.  A later catalog refresh must not turn a
            // successful install into an unhealthy one merely because its old declaration is gone.
            // Records created before the snapshot feature remain explicitly informational instead
            // of being guessed from the current catalog.
            var snapshot = record.DeclaredHealthSnapshot;
            var state = !record.Success ? "failed"
                : snapshot is null ? "legacy"
                : snapshot.IsHealthy ? "healthy"
                : "failed";
            var detail = !record.Success ? SensitiveDataRedactor.Redact(string.Join("; ", record.Errors))
                : snapshot is null ? "The historical application predates declaration health snapshots."
                : snapshot.IsHealthy ? "Template application completed with a healthy declaration snapshot."
                : SensitiveDataRedactor.Redact(string.Join("; ", snapshot.ValidationErrors));
            templates[record.Id] = new TemplateHealthState(state, detail);
        }
        return new HealthProbeSnapshot(
            await _runtime.ProbeNetworkAsync(context, cancellationToken).ConfigureAwait(false),
            await _runtime.ProbeSystemdAsync(context, cancellationToken).ConfigureAwait(false), backups, templates,
            await _runtime.ProbeStorageAsync(context, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class InitialProbeHealthCheck : IHealthCheck
{
    private readonly IHealthProbe _probe;
    public InitialProbeHealthCheck(IHealthProbe probe) => _probe = probe;
    public HealthCheckDescriptor Descriptor { get; } = new("initial.b", HealthScope.Host, [CapabilityId.Wsl]);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var probe = await _probe.ProbeAsync(context, cancellationToken).ConfigureAwait(false);
        var findings = new List<HealthFinding>();
        foreach (var (name, state) in probe.Network.OrderBy(x => x.Key, StringComparer.Ordinal))
            findings.Add(Finding("network." + name, state, HealthScope.Host, "Network " + name, null));
        foreach (var (unit, state) in probe.SystemdUnits.OrderBy(x => x.Key, StringComparer.Ordinal))
            findings.Add(Finding("systemd." + unit, state, HealthScope.Instance, "systemd " + unit, null));
        foreach (var (name, state) in probe.Backups.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var severity = !state.DestinationExists || state.ConsecutiveFailures >= 2 || (state.FreeBytes is not null && state.WarningThresholdBytes is not null && state.FreeBytes < state.WarningThresholdBytes) ? HealthSeverity.Warning : HealthSeverity.Healthy;
            findings.Add(new HealthFinding("backup." + name, severity, HealthScope.Instance, "Backup health", state.Detail, name, Evidence: new Dictionary<string, string> { ["destinationExists"] = state.DestinationExists.ToString(), ["consecutiveFailures"] = state.ConsecutiveFailures.ToString(), ["freeBytes"] = state.FreeBytes?.ToString() ?? "unavailable", ["warningThresholdBytes"] = state.WarningThresholdBytes?.ToString() ?? "unavailable" }));
        }
        foreach (var (name, state) in probe.Templates.OrderBy(x => x.Key, StringComparer.Ordinal))
            findings.Add(new HealthFinding("template." + name, state.DeclaredState.Equals("healthy", StringComparison.OrdinalIgnoreCase) ? HealthSeverity.Healthy : HealthSeverity.Warning, HealthScope.Instance, "Template declared health", state.Detail, name, Evidence: new Dictionary<string, string> { ["declaredState"] = state.DeclaredState }));
        foreach (var (name, state) in probe.Storage.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var instance = name.StartsWith("linux:", StringComparison.Ordinal) ? name[6..] : null;
            var lowHost = state.HostFreeBytes is > 0 && state.HostTotalBytes is > 0 && (double)state.HostFreeBytes / state.HostTotalBytes < .10;
            var lowLinux = state.LinuxFilesystemFreeBytes is > 0 && state.LinuxFilesystemFreeBytes < 2L * 1024 * 1024 * 1024;
            var reclaimable = state.ReclaimableBytes is > 512L * 1024 * 1024;
            var severity = lowHost || lowLinux ? HealthSeverity.Warning : reclaimable ? HealthSeverity.Information : HealthSeverity.Healthy;
            // Trim is deliberately only offered for a measured, running instance. Host free-space
            // warnings never claim that a VHDX operation can safely fix the host.
            var repairId = instance is not null && reclaimable ? "wsl.trim" : null;
            findings.Add(new HealthFinding("storage." + name, severity, instance is null ? HealthScope.Host : HealthScope.Instance, "Storage health", state.Detail, instance, repairId,
                new Dictionary<string, string> { ["hostFreeBytes"] = state.HostFreeBytes?.ToString() ?? "unavailable", ["hostTotalBytes"] = state.HostTotalBytes?.ToString() ?? "unavailable", ["vhdxBytes"] = state.VhdxBytes?.ToString() ?? "unavailable", ["linuxFilesystemFreeBytes"] = state.LinuxFilesystemFreeBytes?.ToString() ?? "unavailable", ["reclaimableBytes"] = state.ReclaimableBytes?.ToString() ?? "unavailable" }));
        }
        return new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow);
    }
    private static HealthFinding Finding(string id, HealthProbeState state, HealthScope scope, string title, string? instance)
    {
        var severity = state.State.ToLowerInvariant() switch { "healthy" or "ok" => HealthSeverity.Healthy, "failed" or "critical" => HealthSeverity.Critical, "warning" or "refused" or "timeout" => HealthSeverity.Warning, _ => HealthSeverity.Information };
        // A connectivity failure can be remediated by a bounded WSL restart, but only after the
        // preview identifies all affected running instances.
        var repair = severity is HealthSeverity.Warning or HealthSeverity.Critical && id.StartsWith("network.", StringComparison.Ordinal) ? "wsl.restart" : null;
        return new HealthFinding(id, severity, scope, title, state.Detail, instance, repair, state.Evidence);
    }
}
