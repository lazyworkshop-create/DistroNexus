using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for WslEventWatcher (E-07).
/// </summary>
public class WslEventWatcherTests
{
    private readonly Mock<ILogger<WslEventWatcher>> _mockLogger;

    public WslEventWatcherTests()
    {
        _mockLogger = new Mock<ILogger<WslEventWatcher>>();
    }

    [Fact]
    public void WslEventWatcher_Implements_IWslEventWatcher()
    {
        // Verify the service implements the interface (compile-time check)
        var watcher = new WslEventWatcher(_mockLogger.Object);
        Assert.IsAssignableFrom<IWslEventWatcher>(watcher);
        watcher.Dispose();
    }

    [Fact]
    public void WslEventWatcher_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WslEventWatcher(null!));
    }

    [Fact]
    public void WslEventWatcher_RaisesCacheInvalidationRequested_Event()
    {
        // Arrange
        var watcher = new WslEventWatcher(_mockLogger.Object, debounceMs: 100);
        using var eventSignal = new ManualResetEventSlim(false);
        watcher.CacheInvalidationRequested += (s, e) => eventSignal.Set();

        // Act — simulate an external event trigger via the test helper
        watcher.SimulateProcessEvent("wsl.exe");

        // Assert — wait up to 2 seconds for the event (debounce is 100ms)
        bool eventRaised = eventSignal.Wait(TimeSpan.FromSeconds(2));
        Assert.True(eventRaised);
        watcher.Dispose();
    }

    [Fact]
    public void WslEventWatcher_CoalescesMultipleEvents()
    {
        // Arrange
        var watcher = new WslEventWatcher(_mockLogger.Object, debounceMs: 50);
        int invocationCount = 0;
        using var eventSignal = new ManualResetEventSlim(false);
        watcher.CacheInvalidationRequested += (s, e) =>
        {
            Interlocked.Increment(ref invocationCount);
            eventSignal.Set();
        };

        // Act — fire 5 events quickly
        for (int i = 0; i < 5; i++)
            watcher.SimulateProcessEvent("wslhost.exe");

        // Assert — wait for debounce to settle (50ms debounce; wait up to 5 seconds for CI headroom)
        eventSignal.Wait(TimeSpan.FromSeconds(5));

        // Give a small grace period to catch any unexpected extra events
        System.Threading.Thread.Sleep(150);

        // Assert — coalesced to 1
        Assert.Equal(1, invocationCount);
        watcher.Dispose();
    }

    [Fact]
    public void WslEventWatcher_Dispose_DoesNotThrow()
    {
        var watcher = new WslEventWatcher(_mockLogger.Object);
        var ex = Record.Exception(() => watcher.Dispose());
        Assert.Null(ex);
    }
}
