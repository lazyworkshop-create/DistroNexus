namespace DistroNexus.Core.Models;

public sealed record MonitoringSample(
    DateTimeOffset CapturedAt,
    double? CpuPercent,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes,
    long? SwapUsedBytes,
    long? SwapTotalBytes,
    long? FilesystemUsedBytes,
    long? FilesystemTotalBytes,
    long? DiskReadBytesPerSecond,
    long? DiskWriteBytesPerSecond,
    long? NetworkReceiveBytesPerSecond,
    long? NetworkTransmitBytesPerSecond,
    long? VhdxPhysicalBytes,
    long? EstimatedReclaimableBytes,
    IReadOnlyList<MonitoredProcess> Processes,
    IReadOnlyDictionary<string, string> UnavailableMetrics,
    HostResourceLimits? HostLimits = null,
    IReadOnlyList<MonitoringWarning>? Warnings = null,
    IReadOnlyDictionary<string, long>? CounterState = null,
    IReadOnlyList<ListeningPort>? ListeningPorts = null);

/// <summary>Explicit limits declared in .wslconfig, not inferred from current usage.</summary>
public sealed record HostResourceLimits(long? MemoryLimitBytes, long? SwapLimitBytes, int? ProcessorLimit);
public sealed record MonitoringThresholds(double CpuPercent = 90, double MemoryPercent = 90, double FilesystemPercent = 90)
{
    public static MonitoringThresholds Default { get; } = new();
    public bool IsValid => CpuPercent is > 0 and <= 100 && MemoryPercent is > 0 and <= 100 && FilesystemPercent is > 0 and <= 100;
}
public sealed record MonitoringWarning(string Metric, double Value, double Threshold, string Detail);

/// <summary>One bounded, best-effort listener observed from the distribution network namespace.</summary>
public sealed record ListeningPort(string Protocol, string LocalAddress, int Port, int? ProcessId = null);

public sealed record MonitoredProcess(int Pid, long StartTimeTicks, string User, double CpuPercent, double MemoryPercent, string Command, IReadOnlyList<int> ListeningPorts)
{
    public bool RequiresAdditionalWarning => Pid <= 1 || User.Equals("root", StringComparison.OrdinalIgnoreCase) || Command.StartsWith("[", StringComparison.Ordinal);
    public string ListeningPortsDisplay => ListeningPorts.Count == 0 ? "—" : string.Join(", ", ListeningPorts);
}

public enum MonitoringProcessAction { Terminate, Kill, Renice }

public sealed record ProcessActionPreview(Guid Token, string DistributionName, MonitoredProcess Process, MonitoringProcessAction Action, int? NiceValue, string Message, bool RequiresAdditionalWarning);
public sealed record ProcessActionResult(bool Succeeded, string OutcomeCode, string? Guidance = null);

/// <summary>Bounded public monitoring result. The token is opaque and is the only process authority returned by a snapshot.</summary>
public sealed record MonitoringSnapshotResult(MonitoringSample Sample, string SnapshotToken, DateTimeOffset ExpiresAt);
public sealed record MonitoringProcessActionPreview(string PreviewToken, int ProcessId, MonitoringProcessAction Action, string Message, bool RequiresAdditionalWarning, DateTimeOffset ExpiresAt);
