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
        bool eventRaised = false;
        watcher.CacheInvalidationRequested += (s, e) => eventRaised = true;

        // Act — simulate an external event trigger via the test helper
        watcher.SimulateProcessEvent("wsl.exe");

        // Give debounce time to fire (debounce is 100ms; wait 500ms for CI headroom)
        System.Threading.Thread.Sleep(500);

        // Assert
        Assert.True(eventRaised);
        watcher.Dispose();
    }

    [Fact]
    public void WslEventWatcher_CoalescesMultipleEvents()
    {
        // Arrange
        var watcher = new WslEventWatcher(_mockLogger.Object, debounceMs: 150);
        int invocationCount = 0;
        watcher.CacheInvalidationRequested += (s, e) => Interlocked.Increment(ref invocationCount);

        // Act — fire 5 events quickly
        for (int i = 0; i < 5; i++)
            watcher.SimulateProcessEvent("wslhost.exe");

        // Wait for debounce to settle (150ms debounce; wait 600ms for CI headroom)
        System.Threading.Thread.Sleep(600);

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
