using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using System.Net;

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
        var settings = new Mock<ISettingsService>(); settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "missing-cache") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(), durable, bundled);
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
    }

    [Fact]
    public async Task ReadPath_PropagatesCancellationAndReturnsEmptyWhenNoFiles()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var settings = new Mock<ISettingsService>(); settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "missing-cache") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(), Path.Combine(_root, "none"), Path.Combine(_root, "none2"));
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.LoadCatalogAsync(cancellationToken: cts.Token));
        Assert.Empty(await service.SearchDistributionsAsync("x"));
    }

    [Fact]
    public async Task ReadPath_FallsBackToBundledThenReturnsEmptyAndSupportsSearch()
    {
        var bundled = Path.Combine(_root, "bundled.json");
        await File.WriteAllTextAsync(bundled, JsonSerializer.Serialize(new[] { new DistroPackage { Id = "ubuntu", Name = "Ubuntu", Description = "Linux", Category = "Ubuntu" } }));
        var settings = new Mock<ISettingsService>(); settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "missing") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(), Path.Combine(_root, "none"), bundled);
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

    [Fact]
    public async Task Refresh_UsesLegacyOnlyWhenSourcesAreAbsentAndPreservesExistingCacheOnFailure()
    {
        var durable = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(durable, JsonSerializer.Serialize(new[] { new DistroPackage { Id = "old", Name = "Old" } }));
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { CatalogUrl = "https://legacy.test/catalog.json", PackageCachePath = Path.Combine(_root, "cache") });
        var handler = new TestHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(handler), durable, Path.Combine(_root, "none"));
        var result = await service.RefreshCatalogWithResultAsync();
        Assert.False(result.Succeeded); Assert.Equal("Preserved", result.CacheState);
        Assert.Equal("old", Assert.Single(await service.LoadCatalogAsync()).Id);
        Assert.Equal("https://legacy.test/catalog.json", Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task Refresh_UsesPersistedActiveSourcesByPriorityThenIdAndSkipsInvalidSources()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings {
            CatalogUrl = "https://legacy.test/catalog.json", PackageCachePath = Path.Combine(_root, "cache"),
            CustomData = new() { ["CatalogSources"] = JsonSerializer.Serialize(new[] {
                new CatalogSource { Id = "invalid", Url = "file:///no", IsActive = true, Priority = 0 },
                new CatalogSource { Id = "b", Url = "https://b.test/catalog.json", IsActive = true, Priority = 1 },
                new CatalogSource { Id = "a", Url = "https://a.test/catalog.json", IsActive = true, Priority = 1 }
            }) }
        });
        var handler = new TestHandler(request => request.RequestUri!.Host == "a.test"
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[{\"Id\":\"a\",\"Name\":\"A\"}]") }
            : new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(handler), Path.Combine(_root, "catalog.json"), Path.Combine(_root, "none"));
        var result = await service.RefreshCatalogWithResultAsync();
        Assert.True(result.Succeeded); Assert.Equal("a", result.SourceId);
        Assert.Single(handler.Requests); Assert.Equal("a.test", handler.Requests[0].RequestUri!.Host);
    }

    [Fact]
    public async Task Refresh_FallsBackFromFailedPersistedSourceToNextOrderedSource()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = Path.Combine(_root, "cache"), CustomData = new() {
            ["CatalogSources"] = JsonSerializer.Serialize(new[] {
                new CatalogSource { Id = "first", Url = "https://first.test/catalog.json", IsActive = true, Priority = 0 },
                new CatalogSource { Id = "second", Url = "https://second.test/catalog.json", IsActive = true, Priority = 1 }
            }) }});
        var handler = new TestHandler(request => request.RequestUri!.Host == "first.test"
            ? new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[{\"Id\":\"fallback\",\"Name\":\"Fallback\"}]") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(handler), Path.Combine(_root, "catalog.json"), Path.Combine(_root, "none"));
        var result = await service.RefreshCatalogWithResultAsync();
        Assert.True(result.Succeeded); Assert.Equal("second", result.SourceId);
        Assert.Equal(["first.test", "second.test"], handler.Requests.Select(r => r.RequestUri!.Host));
    }

    [Fact]
    public async Task Refresh_CancellationLeavesDurableAndMemorySnapshotsUnchanged()
    {
        var durable = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(durable, "[{\"Id\":\"old\",\"Name\":\"Old\"}]");
        var settings = SettingsFor("https://source.test/catalog.json");
        var service = new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(new TestHandler(_ => throw new OperationCanceledException())), durable, Path.Combine(_root, "none"));
        Assert.Equal("old", Assert.Single(await service.LoadCatalogAsync()).Id);
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RefreshCatalogWithResultAsync());
        Assert.Contains("\"old\"", await File.ReadAllTextAsync(durable));
        Assert.Equal("old", Assert.Single(await service.LoadCatalogAsync()).Id);
    }

    [Fact]
    public async Task Refresh_OverLimitStreamWithoutContentLengthPreservesKnownGoodState()
    {
        var durable = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(durable, "[{\"Id\":\"old\",\"Name\":\"Old\"}]");
        var body = new string('x', (10 * 1024 * 1024) + 1);
        var handler = new TestHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new UnknownLengthContent(body) });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, SettingsFor("https://source.test/catalog.json").Object, new HttpClient(handler), durable, Path.Combine(_root, "none"));
        var result = await service.RefreshCatalogWithResultAsync();
        Assert.False(result.Succeeded); Assert.Equal("Preserved", result.CacheState);
        Assert.Contains("\"old\"", await File.ReadAllTextAsync(durable));
    }

    [Fact]
    public async Task Refresh_AtomicReplaceFailureLeavesDurableAndMemorySnapshotsUnchanged()
    {
        var durable = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(durable, "[{\"Id\":\"old\",\"Name\":\"Old\"}]");
        var handler = new TestHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[{\"Id\":\"new\",\"Name\":\"New\"}]") });
        var service = new CatalogService(NullLogger<CatalogService>.Instance, SettingsFor("https://source.test/catalog.json").Object, new HttpClient(handler), durable, Path.Combine(_root, "none"));
        Assert.Equal("old", Assert.Single(await service.LoadCatalogAsync()).Id);
        await using (new FileStream(durable, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await service.RefreshCatalogWithResultAsync();
            Assert.False(result.Succeeded); Assert.Equal("Preserved", result.CacheState);
        }
        Assert.Contains("\"old\"", await File.ReadAllTextAsync(durable));
        Assert.Equal("old", Assert.Single(await service.LoadCatalogAsync()).Id);
    }

    private Mock<ISettingsService> SettingsFor(string url)
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { CatalogUrl = url, PackageCachePath = Path.Combine(_root, "cache") });
        return settings;
    }

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests.Add(request); return Task.FromResult(handler(request)); }
    }

    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(body)).AsTask();
    }
}
