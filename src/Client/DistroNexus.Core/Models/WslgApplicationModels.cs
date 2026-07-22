namespace DistroNexus.Core.Models;

/// <summary>A validated Linux desktop entry. Arguments are never shell text.</summary>
public sealed record WslgApplication(
    string Id, string InstanceName, string Name, string Executable,
    IReadOnlyList<string> Arguments, IReadOnlyList<string> Categories,
    string DesktopFilePath, string? IconPath, bool IsPinned = false, byte[]? IconBytes = null)
{ public string CategoriesText => string.Join(", ", Categories.Where(x => !string.IsNullOrWhiteSpace(x))); }

public sealed record WslgApplicationStatus(bool IsAvailable, string Reason, IReadOnlyList<string> Guidance);
public sealed record WslgLaunchResult(bool Succeeded, string InstanceName, string Executable, string Diagnostic);
