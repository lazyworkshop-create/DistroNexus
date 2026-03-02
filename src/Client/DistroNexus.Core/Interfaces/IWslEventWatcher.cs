namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Watches WSL process activity (via WMI) and fires cache invalidation events.
/// </summary>
public interface IWslEventWatcher : IDisposable
{
    /// <summary>
    /// Fires when a WSL process start or stop event is detected, after the debounce window has elapsed.
    /// </summary>
    event EventHandler CacheInvalidationRequested;

    /// <summary>
    /// Starts watching for WSL process events.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops watching for WSL process events.
    /// </summary>
    void Stop();

    /// <summary>
    /// Simulates a process event for testing purposes.
    /// </summary>
    void SimulateProcessEvent(string processName);
}
