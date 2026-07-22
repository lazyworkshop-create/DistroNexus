using System.Collections.Concurrent;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class MonitoringWarningRegistry : IMonitoringWarningSource, IMonitoringWarningSink
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<MonitoringWarning>> _warnings = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, IReadOnlyList<MonitoringWarning>> ActiveWarnings => new Dictionary<string, IReadOnlyList<MonitoringWarning>>(_warnings, StringComparer.OrdinalIgnoreCase);
    public void Update(string distributionName, IReadOnlyList<MonitoringWarning> warnings)
    {
        if (warnings.Count == 0) _warnings.TryRemove(distributionName, out _); else _warnings[distributionName] = warnings;
    }
}
