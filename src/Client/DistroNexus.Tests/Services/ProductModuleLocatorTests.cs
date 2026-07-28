using System.Reflection;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class ProductModuleLocatorTests
{
    [Fact]
    public void Resolve_PrefersPackagedModuleOverDevelopmentFallback()
    {
        var root = CreateRoot(withPackaged: true, withDevelopment: true);
        try { Assert.EndsWith("PowerShell", Resolve(root, true)); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Resolve_UsesDevelopmentFallbackOnlyWhenEnabled()
    {
        var root = CreateRoot(withPackaged: false, withDevelopment: true);
        try { Assert.NotNull(Resolve(root, true)); Assert.Null(Resolve(root, false)); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Resolve_ReturnsUnavailableWhenTrustedLocationsDoNotContainModule()
    {
        var root = CreateRoot(false, false);
        try { Assert.Null(Resolve(root, true)); }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateRoot(bool withPackaged, bool withDevelopment)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (withPackaged) WriteModule(Path.Combine(root, "PowerShell"));
        if (withDevelopment) WriteModule(Path.Combine(root, "development"));
        return root;
    }
    private static void WriteModule(string path) { Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, "DistroNexus.psd1"), "@"); File.WriteAllText(Path.Combine(path, "DistroNexus.psm1"), "#"); }
    private static string? Resolve(string root, bool development)
    {
        var locator = typeof(ProductModuleLocator).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(bool), typeof(string)], null)!.Invoke([root, development, Path.Combine(root, "development")]);
        return (string?)locator.GetType().GetMethod("Resolve")!.Invoke(locator, null);
    }
}
