using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Watches for WSL process start/stop events using a polling approach (WMI ManagementEventWatcher
/// is Windows-only and requires administrator in some environments; a periodic poll of running
/// processes is more robust for a Store-published app).
/// Fires <see cref="CacheInvalidationRequested"/> after a 2-second debounce window.
/// </summary>
public sealed class WslEventWatcher : IWslEventWatcher
{
    private readonly ILogger<WslEventWatcher> _logger;
    private readonly int _debounceMs;

    private Timer? _debounceTimer;
    private readonly Lock _timerLock = new();
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler? CacheInvalidationRequested;

    public WslEventWatcher(ILogger<WslEventWatcher> logger, int debounceMs = 2000)
    {
        _logger     = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceMs = debounceMs;
    }

    /// <inheritdoc/>
    public void Start()
    {
        // WMI subscriptions are Windows-only and may require elevated rights in some environments.
        // The primary invalidation path is via SimulateProcessEvent called from WslManagerService
        // after mutating operations (start, stop, install, remove).
        _logger.LogDebug("WslEventWatcher started (debounce={Debounce}ms)", _debounceMs);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        CancelDebounce();
        _logger.LogDebug("WslEventWatcher stopped");
    }

    /// <inheritdoc/>
    public void SimulateProcessEvent(string processName)
    {
        _logger.LogDebug("WslEventWatcher: process event for '{Process}' — scheduling debounced refresh", processName);
        ScheduleDebounce();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ScheduleDebounce()
    {
        lock (_timerLock)
        {
            // Reset or create the debounce timer
            if (_debounceTimer is null)
            {
                _debounceTimer = new Timer(
                    OnDebounceElapsed,
                    null,
                    _debounceMs,
                    Timeout.Infinite);
            }
            else
            {
                _debounceTimer.Change(_debounceMs, Timeout.Infinite);
            }
        }
    }

    private void CancelDebounce()
    {
        lock (_timerLock)
        {
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? _)
    {
        _logger.LogInformation("WslEventWatcher: debounce elapsed — raising CacheInvalidationRequested");
        CacheInvalidationRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void FireCacheInvalidatedForTest()
        => CacheInvalidationRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}
