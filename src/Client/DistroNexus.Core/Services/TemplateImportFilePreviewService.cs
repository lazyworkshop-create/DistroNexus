using System.Text;
using System.Text.Json;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Owns safe local template-file validation and content preview creation.</summary>
public sealed class TemplateImportFilePreviewService
{
    private readonly TemplateService _templates;
    private readonly TemplateLocalPreviewStore _previews;

    public TemplateImportFilePreviewService(TemplateService templates, TemplateLocalPreviewStore previews)
    { _templates = templates; _previews = previews; }

    public async Task<TemplateLocalPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || sourcePath.Length > 2048 || sourcePath.Any(char.IsControl) || !Path.IsPathFullyQualified(sourcePath) || sourcePath.StartsWith("\\\\", StringComparison.Ordinal) || sourcePath.StartsWith("\\\\?\\", StringComparison.Ordinal))
            throw new ArgumentException("Template source path is invalid.", nameof(sourcePath));
        var full = Path.GetFullPath(sourcePath);
        if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase) || HasReparsePoint(full))
            throw new ArgumentException("Template source file is invalid.", nameof(sourcePath));
        var info = new FileInfo(full);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length > 1024 * 1024)
            throw new ArgumentException("Template source file is invalid.", nameof(sourcePath));
        var content = await File.ReadAllTextAsync(full, Encoding.UTF8, cancellationToken);
        info.Refresh();
        if (info.Length > 1024 * 1024 || HasReparsePoint(full) || Encoding.UTF8.GetByteCount(content) > 1024 * 1024)
            throw new ArgumentException("Template source file changed during validation.", nameof(sourcePath));
        Template template;
        try { template = JsonSerializer.Deserialize<Template>(content) ?? throw new JsonException(); }
        catch (JsonException) { throw new ArgumentException("Template content is invalid.", nameof(sourcePath)); }
        if (!(await _templates.ValidateTemplateAsync(template)).IsValid) throw new ArgumentException("Template content is invalid.", nameof(sourcePath));
        var token = await _previews.IssueAsync("import", content, cancellationToken);
        return new TemplateLocalPreview(token, "Import", template.Id, DateTimeOffset.UtcNow.AddMinutes(5));
    }

    private static bool HasReparsePoint(string path)
    {
        for (var current = path; ; current = Directory.GetParent(current)?.FullName ?? string.Empty)
        {
            if (string.IsNullOrEmpty(current)) return false;
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) return false;
        }
    }
}
