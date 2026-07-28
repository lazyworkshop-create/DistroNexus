namespace DistroNexus.Core.Models;

/// <summary>Path-free request and result records for the fixed backup capability.</summary>
public sealed record BackupScheduleRequest(string InstanceName, string Frequency, int RetentionCount, TimeSpan Time, string? Destination = null);
public sealed record BackupScheduleSummary(string InstanceName, string Frequency, int RetentionCount, TimeSpan Time, bool Enabled);
public sealed record BackupOperationPreview(string Token, string InstanceName, string Operation, int RetentionCount, DateTimeOffset ExpiresAt);
public sealed record BackupOperationResult(bool Succeeded, string OutcomeCode, string InstanceName, DateTimeOffset CompletedAt);
public sealed record BackupNotification(string InstanceName, string Code, string Message);
