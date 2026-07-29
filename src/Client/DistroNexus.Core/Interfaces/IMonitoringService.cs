using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>Creates bounded, in-memory monitoring sessions for a running distribution.</summary>
public interface IMonitoringService
{
    IMonitoringSession CreateSession(WslInstance instance, TimeSpan interval);
}

/// <summary>In-memory projection of active monitor threshold breaches for Health Center scans.</summary>
public interface IMonitoringWarningSource
{
    IReadOnlyDictionary<string, IReadOnlyList<MonitoringWarning>> ActiveWarnings { get; }
}
public interface IMonitoringWarningSink
{
    void Update(string distributionName, IReadOnlyList<MonitoringWarning> warnings);
}

public interface IMonitoringSession : IAsyncDisposable
{
    IReadOnlyList<MonitoringSample> Samples { get; }
    bool IsRunning { get; }
    string? UnavailableReason { get; }
    event EventHandler<MonitoringSample>? SampleAvailable;
    IAsyncEnumerable<MonitoringSample> StreamAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task SetThresholdsAsync(MonitoringThresholds thresholds, CancellationToken cancellationToken = default);
    Task<ProcessActionPreview> PreviewProcessActionAsync(MonitoredProcess process, MonitoringProcessAction action, CancellationToken cancellationToken = default);
    Task<ProcessActionResult> ExecuteProcessActionAsync(ProcessActionPreview preview, CancellationToken cancellationToken = default);
}
