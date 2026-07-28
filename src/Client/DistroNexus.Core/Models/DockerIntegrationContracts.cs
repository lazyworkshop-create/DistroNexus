namespace DistroNexus.Core.Models;

/// <summary>Public-safe Docker Desktop integration state. It intentionally contains no host paths or settings content.</summary>
public sealed record DockerIntegrationSnapshot(bool IsAvailable, bool IsEligible, string Status, string? Reason, string? Version, string? RestartGuidance);
public sealed record DockerIntegrationPreview(string Token, bool Enabled, DateTimeOffset ExpiresAt, IReadOnlyList<string> Effects, IReadOnlyList<string> Warnings);
public sealed record DockerIntegrationResult(bool Succeeded, string OutcomeCode, bool RestartRequired, string? Guidance);
