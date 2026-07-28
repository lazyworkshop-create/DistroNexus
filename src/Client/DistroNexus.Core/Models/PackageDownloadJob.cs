namespace DistroNexus.Core.Models;

public sealed record PackageJobStartPreviewResult(string? PreviewToken, DateTimeOffset? ExpiresAt, string PackageId, string PackageLabel, string OutcomeCode);
public sealed record PackageJobStartResult(string? JobId, string OutcomeCode);
public sealed record PackageJobActionPreviewResult(string? PreviewToken, DateTimeOffset? ExpiresAt, string JobId, string OutcomeCode);
public sealed record PackageJobActionResult(string JobId, string OutcomeCode);
public sealed record PackageDownloadJob(string JobId, string PackageId, string PackageLabel, string State, int ProgressPercent, string OutcomeCode);
