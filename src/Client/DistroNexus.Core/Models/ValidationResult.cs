namespace DistroNexus.Core.Models;

public sealed record ValidationIssue(string Code, string Message, string? Field = null);
public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
    public static ValidationResult Valid { get; } = new(Array.Empty<ValidationIssue>());
}
