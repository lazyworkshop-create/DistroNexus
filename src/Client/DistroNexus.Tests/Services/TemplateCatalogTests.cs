using System.Text.Json;
using System.Text.Json.Serialization;
using DistroNexus.Core.Models;

namespace DistroNexus.Tests.Services;

public class TemplateCatalogTests
{
    [Fact]
    public void TemplatesJson_ShouldDeserialize_WithExtendedMetadata()
    {
        var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var templatesPath = Path.Combine(repoRoot, "config", "templates.json");

        Assert.True(File.Exists(templatesPath), $"templates.json not found: {templatesPath}");

        var json = File.ReadAllText(templatesPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var templates = JsonSerializer.Deserialize<List<Template>>(json, options);

        Assert.NotNull(templates);
        Assert.NotEmpty(templates);
        Assert.Contains(templates!, t => t.Id == "dotnet-multi-sdk-dev");
        Assert.Contains(templates!, t => t.Id == "ai-ml-gpu-dev");
        Assert.All(templates!, t => Assert.False(string.IsNullOrWhiteSpace(t.Id)));
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "config")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
