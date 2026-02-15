using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public class AppResourcePathResolverTests : IDisposable
{
    private readonly string _tempRoot;

    public AppResourcePathResolverTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "DistroNexus-PathResolver-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void FindFileInBaseOrParents_WhenFileInBase_ReturnsBaseFile()
    {
        var baseDir = Path.Combine(_tempRoot, "base");
        var configDir = Path.Combine(baseDir, "config");
        Directory.CreateDirectory(configDir);

        var expectedPath = Path.Combine(configDir, "templates.json");
        File.WriteAllText(expectedPath, "{}");

        var resolved = AppResourcePathResolver.FindFileInBaseOrParents(baseDir, Path.Combine("config", "templates.json"));

        Assert.Equal(expectedPath, resolved);
    }

    [Fact]
    public void FindFileInBaseOrParents_WhenFileInParent_ReturnsParentFile()
    {
        var rootConfigDir = Path.Combine(_tempRoot, "config");
        Directory.CreateDirectory(rootConfigDir);

        var expectedPath = Path.Combine(rootConfigDir, "catalog.json");
        File.WriteAllText(expectedPath, "{}");

        var deepBase = Path.Combine(_tempRoot, "a", "b", "c", "d");
        Directory.CreateDirectory(deepBase);

        var resolved = AppResourcePathResolver.FindFileInBaseOrParents(deepBase, Path.Combine("config", "catalog.json"), maxParentLevels: 8);

        Assert.Equal(expectedPath, resolved);
    }

    [Fact]
    public void FindDirectoryWithFileInBaseOrParents_WhenManifestInBase_ReturnsPowerShellDirectory()
    {
        var baseDir = Path.Combine(_tempRoot, "base");
        var moduleDir = Path.Combine(baseDir, "PowerShell");
        Directory.CreateDirectory(moduleDir);

        var manifestPath = Path.Combine(moduleDir, "DistroNexus.psd1");
        File.WriteAllText(manifestPath, "@");

        var resolved = AppResourcePathResolver.FindDirectoryWithFileInBaseOrParents(baseDir, "PowerShell", "DistroNexus.psd1");

        Assert.Equal(moduleDir, resolved);
    }

    [Fact]
    public void FindDirectoryWithFileInBaseOrParents_WhenMissing_ReturnsNull()
    {
        var baseDir = Path.Combine(_tempRoot, "missing");
        Directory.CreateDirectory(baseDir);

        var resolved = AppResourcePathResolver.FindDirectoryWithFileInBaseOrParents(baseDir, "PowerShell", "DistroNexus.psd1");

        Assert.Null(resolved);
    }
}