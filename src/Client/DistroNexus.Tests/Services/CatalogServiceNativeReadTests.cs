using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace DistroNexus.Tests.Services;

public sealed class CatalogServiceNativeReadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public CatalogServiceNativeReadTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ReadPath_UsesMemoryThenDurableThenBundledWithoutPowerShellAndReturnsCopies()
    {
        var durable = Path.Combine(_root, "durable.json");
        var bundled = Path.Combine(_root, "bundled.json");
        await File.WriteAllTextAsync(durable, JsonSerializer.Serialize(new[] { new DistroPackage { Id = "durable", Name = "Durable" } }));
        await File.WriteAllTextAsync(bundled, JsonSerializer.Serialize(new[] { new DistroPackage { Id = "bundled", Name = "Bundled" } }));
        var ps = new Mock<IPowerShellService>(MockBehavior.Strict);
        var settings = new Mock<ISettingsService>(); settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "missing-cache") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, ps.Object, new HttpClient(), durable, bundled);
        var first = await service.LoadCatalogAsync();
        first[0].Name = "changed";
        var memory = await service.LoadCatalogAsync();
        Assert.Equal("durable", first[0].Id);
        Assert.Equal("Durable", memory[0].Name);
        await File.WriteAllTextAsync(durable, JsonSerializer.Serialize(new[] { new DistroPackage { Id = "replaced", Name = "Replaced" } }));
        Assert.Equal("durable", Assert.Single(await service.LoadCatalogAsync()).Id);
        File.Delete(durable);
        var forced = await service.LoadCatalogAsync(true);
        Assert.Equal("bundled", Assert.Single(forced).Id);
        Assert.Equal("bundled", (await service.GetDistributionByIdAsync("bundled"))!.Id);
        ps.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadPath_PropagatesCancellationAndReturnsEmptyWhenNoFiles()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var settings = new Mock<ISettingsService>(); settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "missing-cache") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, Mock.Of<IPowerShellService>(), new HttpClient(), Path.Combine(_root, "none"), Path.Combine(_root, "none2"));
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.LoadCatalogAsync(cancellationToken: cts.Token));
        Assert.Empty(await service.SearchDistributionsAsync("x"));
    }

    [Fact]
    public async Task ReadPath_FallsBackToBundledThenReturnsEmptyAndSupportsSearch()
    {
        var bundled = Path.Combine(_root, "bundled.json");
        await File.WriteAllTextAsync(bundled, JsonSerializer.Serialize(new[] { new DistroPackage { Id = "ubuntu", Name = "Ubuntu", Description = "Linux", Category = "Ubuntu" } }));
        var settings = new Mock<ISettingsService>(); settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "missing") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, Mock.Of<IPowerShellService>(), new HttpClient(), Path.Combine(_root, "none"), bundled);
        Assert.Equal("ubuntu", Assert.Single(await service.SearchDistributionsAsync("linux")).Id);
        File.Delete(bundled);
        Assert.Empty(await service.LoadCatalogAsync(true));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public void CatalogReadMethods_DoNotContainPowerShellModuleReadCalls()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Core", "Services", "CatalogService.cs"));
        var readSection = source[..source.IndexOf("public async Task RefreshCatalogAsync", StringComparison.Ordinal)];
        Assert.DoesNotContain("Get-DistroNexusPackage", readSection, StringComparison.Ordinal);
        Assert.DoesNotContain("_powerShellService.ExecuteAsync", readSection, StringComparison.Ordinal);
    }
}
