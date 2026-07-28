using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class InstanceCompactionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dn-compact-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IRegisteredInstanceCompactionAdapter> _adapter = new(MockBehavior.Strict);
    private readonly RegisteredInstanceCompactionState _state = new("Ubuntu", "instance-id", "vhdx-id", false, 1000, "Diskpart", "Ready");

    [Fact]
    public async Task Preview_IsReadOnly_AndNeverClaimsCurrentSizeIsReclaimable()
    {
        _adapter.Setup(x => x.GetAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(_state);
        var service = Create();

        var preview = await service.PreviewAsync("Ubuntu");

        Assert.Equal(1000, preview.CurrentSizeBytes);
        Assert.Equal("Measured", preview.EstimateKind);
        Assert.Contains(preview.Warnings, item => item.Contains("not an estimate", StringComparison.OrdinalIgnoreCase));
        _adapter.Verify(x => x.CompactAsync(It.IsAny<RegisteredInstanceCompactionState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ConsumesTokenOnce_AndRejectsStateDrift()
    {
        _adapter.SetupSequence(x => x.GetAsync("Ubuntu", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_state)
            .ReturnsAsync(_state with { VhdxIdentity = "changed" });
        var service = Create();
        var preview = await service.PreviewAsync("Ubuntu");

        var changed = await service.ExecuteAsync(preview.PreviewToken);

        Assert.False(changed.Succeeded);
        Assert.Equal("Lifecycle.CompactionStateChanged", changed.OutcomeCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken));
        _adapter.Verify(x => x.CompactAsync(It.IsAny<RegisteredInstanceCompactionState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ReturnsMeasuredSavingsOnlyAfterFixedAdapterExecution()
    {
        _adapter.SetupSequence(x => x.GetAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(_state).ReturnsAsync(_state);
        _adapter.Setup(x => x.CompactAsync(_state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstanceCompactionExecution(true, "Lifecycle.Compacted", 750, "Diskpart", false));
        var service = Create();
        var preview = await service.PreviewAsync("Ubuntu");

        var result = await service.ExecuteAsync(preview.PreviewToken);

        Assert.True(result.Succeeded);
        Assert.Equal(250, result.SavedBytes);
        Assert.Equal("Diskpart", result.Method);
    }

    [Fact]
    public async Task Execute_ExpiredGrant_IsConsumedWithoutCallingTheAdapter()
    {
        var clock = new AdjustableClock(DateTimeOffset.Parse("2026-07-29T00:00:00Z"));
        _adapter.Setup(x => x.GetAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(_state);
        var service = Create(clock: clock);
        var preview = await service.PreviewAsync("Ubuntu");
        clock.Advance(TimeSpan.FromMinutes(3));

        var result = await service.ExecuteAsync(preview.PreviewToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Lifecycle.CompactionGrantExpired", result.OutcomeCode);
        _adapter.Verify(x => x.CompactAsync(It.IsAny<RegisteredInstanceCompactionState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ForeignSidCannotConsumeAValidGrant()
    {
        _adapter.Setup(x => x.GetAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(_state);
        var owner = Create(sid: "owner-sid");
        var preview = await owner.PreviewAsync("Ubuntu");
        var foreign = Create(sid: "foreign-sid");

        var result = await foreign.ExecuteAsync(preview.PreviewToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Lifecycle.CompactionGrantInvalid", result.OutcomeCode);
        _adapter.Verify(x => x.CompactAsync(It.IsAny<RegisteredInstanceCompactionState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimultaneousExecute_InvokesTheFixedAdapterExactlyOnce()
    {
        var executions = 0;
        _adapter.Setup(x => x.GetAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(_state);
        _adapter.Setup(x => x.CompactAsync(_state, It.IsAny<CancellationToken>())).Returns(async () =>
        {
            Interlocked.Increment(ref executions);
            await Task.Yield();
            return new InstanceCompactionExecution(true, "Lifecycle.Compacted", 750, "Diskpart", false);
        });
        var service = Create();
        var preview = await service.PreviewAsync("Ubuntu");

        var first = service.ExecuteAsync(preview.PreviewToken);
        var second = service.ExecuteAsync(preview.PreviewToken);
        var outcomes = await Task.WhenAll(Wrap(first), Wrap(second));

        Assert.Equal(1, executions);
        Assert.Single(outcomes, outcome => outcome.Result?.Succeeded == true);
        Assert.Single(outcomes, outcome => outcome.Error is InvalidOperationException { Message: "Lifecycle.CompactionGrantInvalid" });
    }

    private static async Task<(InstanceCompactionResult? Result, Exception? Error)> Wrap(Task<InstanceCompactionResult> task)
    { try { return (await task, null); } catch (Exception error) { return (null, error); } }
    private InstanceCompactionService Create(TimeProvider? clock = null, string sid = "test-sid") => new(_adapter.Object, _root, clock, () => sid, bytes => bytes, bytes => bytes);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan period) => _now = _now.Add(period);
    }
}
