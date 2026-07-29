using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class CatalogServicePackageCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus-cache-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UsageToken_SurvivesIndependentServiceConstructionAndBindsCurrentFile()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var file = Path.Combine(cache, "ubuntu.wsl");
        await File.WriteAllTextAsync(file, "first");
        var first = Create(cache, keyRoot);
        var token = Assert.Single((await first.GetCacheUsageAsync()).CachedPackages).CacheEntryId;

        var second = Create(cache, keyRoot);
        Assert.True((await second.DeletePackageCacheEntryAsync(token)).Deleted);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ConcurrentIndependentServices_ConvergeOnOnePersistentKey()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        await File.WriteAllTextAsync(Path.Combine(cache, "ubuntu.wsl"), "first");
        using var barrier = new Barrier(2);
        var first = Create(cache, keyRoot, beforeTokenKeyPublish: () => barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
        var second = Create(cache, keyRoot, beforeTokenKeyPublish: () => barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
        var tokens = await Task.WhenAll(
            Task.Run(() => first.GetCacheUsageAsync()),
            Task.Run(() => second.GetCacheUsageAsync()));
        var firstToken = Assert.Single(tokens[0].CachedPackages).CacheEntryId;
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(tokens[1].CachedPackages).CacheEntryId));
        // Token bytes include a time-based expiry, so equality is not the interoperability
        // contract. The peer service must verify and delete the current contained entry.
        Assert.True((await second.DeletePackageCacheEntryAsync(firstToken)).Deleted);
        Assert.False(File.Exists(Path.Combine(cache, "ubuntu.wsl")));
    }

    [Fact]
    public async Task ForgedAndStaleTokens_FailBeforeDeletion()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var file = Path.Combine(cache, "ubuntu.wsl");
        await File.WriteAllTextAsync(file, "first");
        var service = Create(cache, keyRoot);
        var token = Assert.Single((await service.GetCacheUsageAsync()).CachedPackages).CacheEntryId;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePackageCacheEntryAsync(token + "forged"));
        Assert.True(File.Exists(file));
        await File.WriteAllTextAsync(file, "changed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePackageCacheEntryAsync(token));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task ExpiredToken_FailsBeforeDeletion()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var file = Path.Combine(cache, "ubuntu.wsl");
        await File.WriteAllTextAsync(file, "first");
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = Create(cache, keyRoot, clock);
        var token = Assert.Single((await service.GetCacheUsageAsync()).CachedPackages).CacheEntryId;
        clock.Advance(TimeSpan.FromMinutes(16));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePackageCacheEntryAsync(token));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task Usage_StreamsAllTotalsButBoundsDisplayedEntries()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        for (var index = 0; index < 1001; index++) await File.WriteAllTextAsync(Path.Combine(cache, $"{index}.wsl"), "x");

        var usage = await Create(cache, keyRoot).GetCacheUsageAsync();

        Assert.Equal(1001, usage.PackageCount);
        Assert.Equal(1000, usage.CachedPackages.Count);
        Assert.True(usage.HasMoreEntries);
        Assert.Equal(1001, usage.TotalSizeBytes);
    }

    [Fact]
    public async Task CompatibilitySelector_RejectsOutsideRootBeforeMutation()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var outside = Path.Combine(_root, "outside.wsl");
        await File.WriteAllTextAsync(outside, "x");
        var service = Create(cache, keyRoot);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePackageCacheAsync(new PackageCacheDeleteRequest(LocalPath: outside)));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task CompatibilitySelector_DeletesExactlyOneCurrentContainedEntry()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var file = Path.Combine(cache, "ubuntu.wsl");
        await File.WriteAllTextAsync(file, "x");
        var result = await Create(cache, keyRoot).DeletePackageCacheAsync(new PackageCacheDeleteRequest(DefaultName: "ubuntu"));
        Assert.True(result.Deleted);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ContainedLocalPathSelector_DeletesExactlyOneCurrentEntry()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var file = Path.Combine(cache, "ubuntu.wsl");
        await File.WriteAllTextAsync(file, "x");
        Assert.True((await Create(cache, keyRoot).DeletePackageCacheAsync(new PackageCacheDeleteRequest(LocalPath: file))).Deleted);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task CompatibilitySelector_RejectsAmbiguityBeforeMutation()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var first = Path.Combine(cache, "ubuntu.wsl");
        var second = Path.Combine(cache, "ubuntu.tar");
        await File.WriteAllTextAsync(first, "x"); await File.WriteAllTextAsync(second, "x");
        var service = Create(cache, keyRoot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePackageCacheAsync(new PackageCacheDeleteRequest(DefaultName: "ubuntu")));
        Assert.True(File.Exists(first)); Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task ReparsePointCandidate_IsRejectedBeforeDeletion()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var file = Path.Combine(cache, "escape.wsl");
        await File.WriteAllTextAsync(file, "x");
        var deletes = 0;
        var service = Create(cache, keyRoot, getAttributes: path => string.Equals(path, file, StringComparison.OrdinalIgnoreCase)
            ? FileAttributes.ReparsePoint : File.GetAttributes(path), deleteFile: _ => deletes++);

        // The entry is selected by the modeled compatibility selector; metadata rejects it
        // before the injectable deletion operation can observe any mutation.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeletePackageCacheAsync(new PackageCacheDeleteRequest(DefaultName: "escape")));
        Assert.Equal(0, deletes);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task Clear_ReportsPartialFailuresAndContinues()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var failing = Path.Combine(cache, "locked.wsl");
        var succeeding = Path.Combine(cache, "ok.wsl");
        await File.WriteAllTextAsync(failing, "x");
        await File.WriteAllTextAsync(succeeding, "x");
        var deleted = new List<string>();
        var service = Create(cache, keyRoot, deleteFile: path =>
        {
            if (string.Equals(path, failing, StringComparison.OrdinalIgnoreCase)) throw new IOException("locked");
            deleted.Add(path);
        });

        var result = await service.ClearPackageCacheAsync();
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal("PackageCache.Partial", result.DiagnosticCode);
        Assert.Contains(succeeding, deleted);
    }

    [Fact]
    public async Task Clear_PreservesCancellationBeforeAnyDelete()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        await File.WriteAllTextAsync(Path.Combine(cache, "one.wsl"), "x");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Create(cache, keyRoot).ClearPackageCacheAsync(cancellation.Token));
    }

    [Fact]
    public async Task Clear_ObservesCancellationBetweenFiles()
    {
        var cache = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
        var keyRoot = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        var first = Path.Combine(cache, "a.wsl"); var second = Path.Combine(cache, "b.wsl");
        await File.WriteAllTextAsync(first, "x"); await File.WriteAllTextAsync(second, "x");
        using var cancellation = new CancellationTokenSource();
        var service = Create(cache, keyRoot, deleteFile: path => { File.Delete(path); cancellation.Cancel(); });
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ClearPackageCacheAsync(cancellation.Token));
        Assert.True(File.Exists(second));
    }

    private static CatalogService Create(string cache, string keyRoot, TimeProvider? clock = null, Func<string, FileAttributes>? getAttributes = null, Action<string>? deleteFile = null, Action? beforeTokenKeyPublish = null)
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { PackageCachePath = cache });
        return new CatalogService(NullLogger<CatalogService>.Instance, settings.Object, new HttpClient(), Path.Combine(keyRoot, "catalog"), Path.Combine(keyRoot, "bundled"), keyRoot, clock, getAttributes, deleteFile, beforeTokenKeyPublish);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
