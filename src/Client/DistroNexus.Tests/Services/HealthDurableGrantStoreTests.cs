using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class HealthDurableGrantStoreTests
{
    private static readonly Func<byte[], byte[]> Identity = x => x;
    private static HealthFinding Finding(string id = "f") => new(id, HealthSeverity.Warning, HealthScope.Host, "title", "detail", RepairId: "repair");
    private static RepairPreview Preview => new("repair", "title", RepairSafety.Safe, RepairIdempotency.Idempotent, [], []);

    [Fact]
    public async Task RepairGrant_FreshReplayCorruptSidCanonicalAndParallelConsumptionFailClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var sid = "S-1";
        try
        {
            var issue = new HealthRepairGrantStore(root, () => sid, Identity, Identity); await issue.IssueAsync("a", Finding(), Preview, default);
            Assert.Equal("f", (await new HealthRepairGrantStore(root, () => sid, Identity, Identity).ConsumeAsync("a", default)).Finding.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => issue.ConsumeAsync("a", default));
            await issue.IssueAsync("b", Finding(), Preview, default); var file = Directory.EnumerateFiles(Path.Combine(root, "health-repair-grants"), "*.grant").Single(); await File.WriteAllBytesAsync(file, [1, 2]); await Assert.ThrowsAsync<InvalidOperationException>(() => issue.ConsumeAsync("b", default));
            await issue.IssueAsync("c", Finding(), Preview, default); await Assert.ThrowsAsync<InvalidOperationException>(() => new HealthRepairGrantStore(root, () => "S-2", Identity, Identity).ConsumeAsync("c", default));
            await issue.IssueAsync("d", Finding(), Preview, default); var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { try { await issue.ConsumeAsync("d", default); return true; } catch { return false; } })); Assert.Equal(1, results.Count(x => x));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DiagnosticGrant_FreshReplaySidExpiryAndBoundedCleanupFailClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var now = DateTimeOffset.UtcNow; var clock = new FakeTimeProvider(now);
        try
        {
            var store = new DiagnosticSnapshotGrantStore(root, () => "S-1", Identity, Identity, clock); await store.IssueAsync("a", DiagnosticReportFormat.Json, [], "{}", now.AddMinutes(1), default);
            Assert.Equal(DiagnosticReportFormat.Json, (await new DiagnosticSnapshotGrantStore(root, () => "S-1", Identity, Identity, clock).ConsumeAsync("a", default)).Format);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("a", default));
            await store.IssueAsync("b", DiagnosticReportFormat.Json, [], "{}", now.AddMinutes(1), default); await Assert.ThrowsAsync<InvalidOperationException>(() => new DiagnosticSnapshotGrantStore(root, () => "S-2", Identity, Identity, clock).ConsumeAsync("b", default));
            await store.IssueAsync("c", DiagnosticReportFormat.Json, [], "{}", now.AddSeconds(-1), default); await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("c", default));
            for (var i = 0; i < 64; i++) await store.IssueAsync("x" + i, DiagnosticReportFormat.Json, [], "{}", now.AddMinutes(-1), default);
            await store.IssueAsync("after", DiagnosticReportFormat.Json, [], "{}", now.AddMinutes(1), default);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RepairGrant_ExpiryCanonicalMismatchAndCapacityCleanupFailClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        try
        {
            var store = new HealthRepairGrantStore(root, () => "S-1", Identity, Identity, clock);
            await store.IssueAsync("expired", Finding(), Preview, default); clock.Advance(TimeSpan.FromMinutes(11));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("expired", default));
            await store.IssueAsync("canonical", Finding(), Preview, default); var path = Directory.EnumerateFiles(Path.Combine(root, "health-repair-grants"), "*.grant").Single(); var grant = System.Text.Json.JsonSerializer.Deserialize<HealthRepairGrantStore.Grant>(await File.ReadAllBytesAsync(path))!; await File.WriteAllBytesAsync(path, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(grant with { Finding = Finding("changed") }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("canonical", default));
            clock.Advance(TimeSpan.FromMinutes(-11)); for (var i = 0; i < 64; i++) await store.IssueAsync("old" + i, Finding(), Preview, default); clock.Advance(TimeSpan.FromMinutes(11)); await store.IssueAsync("after", Finding(), Preview, default);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DiagnosticGrant_CorruptRecordAndParallelConsumeFailClosed()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); var store = new DiagnosticSnapshotGrantStore(root, () => "S-1", Identity, Identity);
        try
        {
            await store.IssueAsync("corrupt", DiagnosticReportFormat.Json, [], "{}", DateTimeOffset.UtcNow.AddMinutes(1), default); await File.WriteAllBytesAsync(Directory.EnumerateFiles(root, "*.grant").Single(), [1, 2, 3]); await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync("corrupt", default));
            await store.IssueAsync("parallel", DiagnosticReportFormat.Json, [], "{}", DateTimeOffset.UtcNow.AddMinutes(1), default); var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { try { await store.ConsumeAsync("parallel", default); return true; } catch { return false; } })); Assert.Equal(1, results.Count(x => x));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FakeTimeProvider(DateTimeOffset value) : TimeProvider { private DateTimeOffset _value = value; public override DateTimeOffset GetUtcNow() => _value; public void Advance(TimeSpan value) => _value = _value.Add(value); }
}
