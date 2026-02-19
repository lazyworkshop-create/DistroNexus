namespace DistroNexus.Desktop.Wizard;

/// <summary>
/// Optional startup payload for install wizard initialization.
/// </summary>
public sealed class InstallWizardStartupRequest
{
    /// <summary>
    /// Optional template identifier for preselection.
    /// </summary>
    public string? TemplateId { get; init; }

    /// <summary>
    /// Optional distribution identifier for preselection.
    /// </summary>
    public string? SelectedDistributionId { get; init; }
}
