namespace DistroNexus.Core.Models;

/// <summary>A validated Linux desktop entry. Arguments are never shell text.</summary>
public sealed record WslgApplication(
    string Id, string InstanceName, string Name, string Executable,
    IReadOnlyList<string> Arguments, IReadOnlyList<string> Categories,
    string DesktopFilePath, string? IconPath, bool IsPinned = false, byte[]? IconBytes = null)
{ public string CategoriesText => string.Join(", ", Categories.Where(x => !string.IsNullOrWhiteSpace(x))); }

public sealed record WslgApplicationStatus(bool IsAvailable, string Reason, IReadOnlyList<string> Guidance);
public sealed record WslgLaunchResult(bool Succeeded, string InstanceName, string Executable, string Diagnostic);

/// <summary>Safe WSLg application data that can cross the module presentation boundary.</summary>
public sealed record WslgApplicationProjection(string ApplicationId, string DisplayName, IReadOnlyList<string> Categories, bool IsPinned, byte[]? IconBytes)
{ public string Name => DisplayName; public string CategoriesText => string.Join(", ", Categories); }
public sealed record WslgDiscoveryResult(WslgApplicationStatus Status, string? DiscoveryToken, DateTimeOffset? ExpiresAt, IReadOnlyList<WslgApplicationProjection> Applications);
public sealed record WslgActionResult(bool Succeeded, string Diagnostic);
