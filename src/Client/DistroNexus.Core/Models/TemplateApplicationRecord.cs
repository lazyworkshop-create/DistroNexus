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
}
