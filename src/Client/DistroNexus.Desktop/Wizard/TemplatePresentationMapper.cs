using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.Wizard;

/// <summary>Maps only the presentation-safe module catalog projection into the wizard selection model.</summary>
internal static class TemplatePresentationMapper
{
    public static Template ToTemplate(TemplateDisplay source) => new()
    {
        Id = source.Id, Name = source.Name, Description = source.Description,
        Category = source.Category, Version = source.Version, Author = source.Author,
        Tags = source.Tags.ToList(), ScenarioTags = source.Tags.ToList(),
        CompatibleDistros = source.CompatibleDistros.ToList(),
        EstimatedDurationMinutes = source.EstimatedDurationMinutes,
        EstimatedDiskSpaceMB = source.EstimatedDiskSpaceMB,
        IsOfficial = source.IsOfficial, IsCustom = source.IsCustom,
        TrustState = source.TrustState, Capabilities = source.Capabilities.ToList()
    };
}
