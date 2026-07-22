using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class MonitoringHealthCheck(IMonitoringWarningSource warnings) : IHealthCheck
{
    public HealthCheckDescriptor Descriptor { get; } = new("instance.monitoring", HealthScope.Instance, []);
    public Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var findings = warnings.ActiveWarnings.SelectMany(pair => pair.Value.Select(warning => new HealthFinding(
            $"monitor.{pair.Key}.{warning.Metric}", HealthSeverity.Warning, HealthScope.Instance,
            "Monitoring threshold exceeded", warning.Detail, pair.Key,
            Evidence: new Dictionary<string, string> { ["metric"] = warning.Metric, ["value"] = warning.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), ["threshold"] = warning.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture) }))).ToArray();
        return Task.FromResult(new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow));
    }
}
