namespace DistroNexus.Core.Models;

/// <summary>
/// Records the history of template applications.
/// </summary>
public class TemplateApplicationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public List<string> ExecutedScripts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string LogFilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    /// <summary>Immutable-at-install declaration evidence used by Health Center. It avoids
    /// judging an already applied template against a later, unrelated catalog revision.</summary>
    public TemplateDeclaredHealthSnapshot? DeclaredHealthSnapshot { get; set; }
    /// <summary>Redacted install-time selections and immutable marketplace provenance for audit/recovery.</summary>
    public IReadOnlyDictionary<string, string> ResolvedVariables { get; set; } = new Dictionary<string, string>();
    public TemplateMarketplaceApplicationProvenance? MarketplaceProvenance { get; set; }
}

public sealed record TemplateMarketplaceApplicationProvenance(string SourceUrl, string PublisherFingerprint, string ArtifactSha256, string Version);

public sealed record TemplateDeclaredHealthSnapshot(
    bool IsHealthy,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> RequiredPreflightIds,
    IReadOnlyList<string> DeclaredPreflightIds,
    string TemplateVersion,
    IReadOnlyList<string>? ExpectedScriptIds = null,
    IReadOnlyList<string>? AppliedScriptIds = null,
    IReadOnlyList<TemplateRuntimePreflightContract>? RuntimePreflightContracts = null);

/// <summary>Only a deliberately tiny, non-interpolated command language is retained for a later
/// read-only health check.  Arbitrary template commands are never replayed by Health Center.</summary>
public sealed record TemplateRuntimePreflightContract(string Id, bool Required, string Command);
