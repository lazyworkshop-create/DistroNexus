namespace DistroNexus.Core.Models;

/// <summary>Internal request for a picker-selected template file. The path never crosses back to presentation.</summary>
public sealed record TemplateImportFilePreviewRequest(string SourcePath);
