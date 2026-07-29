using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public class HealthOrchestratorTests
{
    [Fact]
    public async Task Scan_OrdersFindings_AndLimitsIndependentChecksToFour()
    {
        var active = 0; var peak = 0;
        var checks = Enumerable.Range(0, 8).Select(i => new DelegateCheck($"check.{i}", async ct =>
        {
            var current = Interlocked.Increment(ref active); InterlockedExtensions.Max(ref peak, current);
            await Task.Delay(25, ct); Interlocked.Decrement(ref active);
            return [new HealthFinding($"f.{i}", i == 7 ? HealthSeverity.Critical : HealthSeverity.Healthy, HealthScope.Host, "title", "detail")];
        })).Cast<IHealthCheck>().ToArray();
        var sut = NewOrchestrator(checks, TempPath());

        var result = await sut.ScanAsync();

        Assert.True(peak <= 4);
        Assert.Equal(HealthSeverity.Critical, result.Findings[0].Severity);
    }

    [Fact]
    public async Task Scan_IsSingleFlight_AndKeepsSevenDayHistory()
    {
        var calls = 0;
        var check = new DelegateCheck("one", async ct => { Interlocked.Increment(ref calls); await Task.Delay(50, ct); return [new HealthFinding("ok", HealthSeverity.Healthy, HealthScope.Host, "ok", "ok")]; });
        var path = TempPath(); var sut = NewOrchestrator([check], path);
        await Task.WhenAll(sut.ScanAsync(), sut.ScanAsync());
        await File.WriteAllTextAsync(path, "[{\"completedAt\":\"2000-01-01T00:00:00+00:00\",\"healthy\":0,\"information\":0,\"warning\":0,\"critical\":0}]");
        Assert.Equal(2, calls);
        Assert.Empty(await sut.GetHistoryAsync());
    }

    [Fact]
    public async Task History_ReadsLegacyArrayMigratesOnScanAndUsesRevisionedStore()
    {
        var path = TempPath();
        await File.WriteAllTextAsync(path, $"[{{\"completedAt\":\"{DateTimeOffset.UtcNow:O}\",\"healthy\":1,\"information\":0,\"warning\":0,\"critical\":0}}]");
        var check = new DelegateCheck("one", _ => Task.FromResult<IReadOnlyList<HealthFinding>>([new("ok", HealthSeverity.Healthy, HealthScope.Host, "ok", "ok")]));
        var sut = NewOrchestrator([check], path);

        await sut.ScanAsync();

        using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(2, json.RootElement.GetProperty("value").GetArrayLength());
    }

    [Fact]
    public async Task History_ConcurrentOrchestratorsRetainBothCompletedScans()
    {
        var path = TempPath();
        var first = NewOrchestrator([new DelegateCheck("one", _ => Task.FromResult<IReadOnlyList<HealthFinding>>([new("one", HealthSeverity.Healthy, HealthScope.Host, "one", "one")]))], path);
        var second = NewOrchestrator([new DelegateCheck("two", _ => Task.FromResult<IReadOnlyList<HealthFinding>>([new("two", HealthSeverity.Warning, HealthScope.Host, "two", "two")]))], path);

        await Task.WhenAll(first.ScanAsync(), second.ScanAsync());

        Assert.Equal(2, (await first.GetHistoryAsync()).Count);
    }

    [Fact]
    public async Task Scan_Cancellation_ReturnsCancelledAndDoesNotThrow()
    {
        var check = new DelegateCheck("slow", async ct => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return []; });
        var sut = NewOrchestrator([check], TempPath()); using var cts = new CancellationTokenSource(30);
        var result = await sut.ScanAsync(cancellationToken: cts.Token);
        Assert.True(result.WasCancelled);
    }

    [Fact]
    public async Task Scan_DoesNotExecuteCheckWhenDeclaredCapabilityPrerequisiteIsUnavailable()
    {
        var executed = false;
        var check = new PrerequisiteCheck(() => executed = true);
        var capability = new Mock<IPlatformCapabilityService>();
        var unsupported = new CapabilityResult(CapabilityId.Wsl, CapabilityStatus.Unsupported, "wsl-not-installed", CapabilitySource.OperatingSystem, DateTimeOffset.UtcNow);
        capability.Setup(x => x.GetHostSnapshotAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformCapabilitySnapshot(Snapshot().Host, new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Wsl] = unsupported }, new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow));
        var manager = new Mock<IWslManagerService>(); manager.Setup(x => x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = new HealthOrchestrator([check], capability.Object, manager.Object, TempPath());

        var result = await sut.ScanAsync();

        Assert.False(executed);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("requires.wsl.prerequisite", finding.Id);
        Assert.Contains("wsl-not-installed", finding.Detail);
    }

    private static HealthOrchestrator NewOrchestrator(IEnumerable<IHealthCheck> checks, string path)
    {
        var capability = new Mock<IPlatformCapabilityService>(); capability.Setup(x => x.GetHostSnapshotAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot());
        var manager = new Mock<IWslManagerService>(); manager.Setup(x => x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return new HealthOrchestrator(checks, capability.Object, manager.Object, path);
    }
    private static PlatformCapabilitySnapshot Snapshot() => new(new HostPlatformFacts("Windows", new Version(10, 0), "x64", false, null, new Version(2, 0), null, null, false), new Dictionary<CapabilityId, CapabilityResult>(), new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow);
    private static string TempPath() => Path.Combine(Path.GetTempPath(), "DistroNexus-health-" + Guid.NewGuid() + ".json");
    private sealed class DelegateCheck(string id, Func<CancellationToken, Task<IReadOnlyList<HealthFinding>>> action) : IHealthCheck
    {
        public HealthCheckDescriptor Descriptor { get; } = new(id, HealthScope.Host, []);
        public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken) => new(Descriptor.Id, await action(cancellationToken), DateTimeOffset.UtcNow);
    }
    private sealed class PrerequisiteCheck(Action execute) : IHealthCheck
    {
        public HealthCheckDescriptor Descriptor { get; } = new("requires.wsl", HealthScope.Host, [CapabilityId.Wsl]);
        public Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
        {
            execute();
            return Task.FromResult(new HealthCheckResult(Descriptor.Id, [], DateTimeOffset.UtcNow));
        }
    }
}

internal static class InterlockedExtensions
{
    public static void Max(ref int target, int candidate) { int current; while ((current = target) < candidate && Interlocked.CompareExchange(ref target, candidate, current) != current) { } }
}
