using System.Text;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class InstanceResourceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus-tests", Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task FreshService_ConsumesDurableGrantExactlyOnce()
    {
        var adapter = new Adapter(); var clock = new Clock();
        var first = New(adapter, clock); var preview = await first.PreviewSparseAsync("Ubuntu", true);
        var second = New(adapter, clock);
        Assert.True((await second.ExecuteSparseAsync(preview.PreviewToken)).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.ExecuteSparseAsync(preview.PreviewToken));
        Assert.Equal(1, adapter.SetCalls);
    }

    [Fact]
    public async Task RejectsForgedExpiredSidAndChangedIdentityOrState()
    {
        var adapter = new Adapter(); var clock = new Clock();
        var service = New(adapter, clock); var preview = await service.PreviewSparseAsync("Ubuntu", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteSparseAsync(new string('0', 64)));
        adapter.State = adapter.State with { Identity = "changed" };
        Assert.Equal("InstanceSparse.StateChanged", (await service.ExecuteSparseAsync(preview.PreviewToken)).OutcomeCode);
        var changedState = await service.PreviewSparseAsync("Ubuntu", true); adapter.State = adapter.State with { SparseMode = true };
        Assert.Equal("InstanceSparse.StateChanged", (await service.ExecuteSparseAsync(changedState.PreviewToken)).OutcomeCode);
        var expired = await service.PreviewSparseAsync("Ubuntu", false); clock.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal("InstanceSparse.PreviewExpired", (await service.ExecuteSparseAsync(expired.PreviewToken)).OutcomeCode);
        var sidPreview = await New(adapter, clock, "a").PreviewSparseAsync("Ubuntu", false);
        Assert.Equal("InstanceSparse.PreviewInvalid", (await New(adapter, clock, "b").ExecuteSparseAsync(sidPreview.PreviewToken)).OutcomeCode);
    }

    [Fact]
    public async Task ParallelConsumption_AllowsOneExecutorAndCleansExpiredGrant()
    {
        var adapter = new Adapter(); var clock = new Clock(); var service = New(adapter, clock); var preview = await service.PreviewSparseAsync("Ubuntu", true);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => { try { return await service.ExecuteSparseAsync(preview.PreviewToken); } catch (InvalidOperationException) { return null; } }));
        Assert.Single(results.Where(x => x?.Succeeded == true)); Assert.Equal(1, adapter.SetCalls);
        var expired = await service.PreviewSparseAsync("Ubuntu", false); clock.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal("InstanceSparse.PreviewExpired", (await service.ExecuteSparseAsync(expired.PreviewToken)).OutcomeCode);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.grant"));
    }

    [Fact]
    public async Task RequiresRegisteredWsl2AndReturnsSanitizedSnapshot()
    {
        var adapter = new Adapter { State = new("Ubuntu", "identity", 1, false) }; var service = New(adapter, new Clock());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync("Ubuntu"));
        adapter.State = adapter.State with { WslVersion = 2 };
        var snapshot = await service.GetAsync("Ubuntu");
        Assert.Equal("Ubuntu", snapshot.Name); Assert.False(snapshot.SparseMode);
        Assert.DoesNotContain("Path", typeof(DistroNexus.Core.Models.InstanceResourceSnapshot).GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task Cleanup_RemovesStaleConsumedAndCorruptArtifactsWithinBound()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "stale.grant.consumed.dead"), "interrupted");
        File.SetLastWriteTimeUtc(Path.Combine(_root, "stale.grant.consumed.dead"), DateTime.Parse("2026-07-27T23:49:00Z").ToUniversalTime());
        foreach (var index in Enumerable.Range(0, 257)) File.WriteAllText(Path.Combine(_root, $"{index:D3}.grant"), "corrupt");
        var service = New(new Adapter(), new Clock());
        await service.PreviewSparseAsync("Ubuntu", true);
        Assert.False(File.Exists(Path.Combine(_root, "stale.grant.consumed.dead")));
        Assert.Equal(2, Directory.EnumerateFiles(_root, "*.grant").Count());
    }

    private InstanceResourceService New(Adapter adapter, Clock clock, string sid = "sid") => new(adapter, _root, clock, () => sid, bytes => bytes, bytes => bytes);
    private sealed class Adapter : IRegisteredInstanceSparseAdapter
    {
        public RegisteredInstanceSparseState State = new("Ubuntu", "identity", 2, false); public int SetCalls;
        public Task<RegisteredInstanceSparseState?> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<RegisteredInstanceSparseState?>(name == State.Name ? State : null);
        public Task<bool> SetSparseAsync(string registeredName, bool enabled, CancellationToken cancellationToken = default) { SetCalls++; State = State with { SparseMode = enabled }; return Task.FromResult(true); }
    }
    private sealed class Clock(DateTimeOffset? now = null) : TimeProvider { private DateTimeOffset _now = now ?? DateTimeOffset.Parse("2026-07-28T00:00:00Z"); public override DateTimeOffset GetUtcNow() => _now; public void Advance(TimeSpan amount) => _now += amount; }
}
