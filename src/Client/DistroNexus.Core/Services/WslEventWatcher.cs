using System.Management;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Watches for WSL process start/stop events using WMI <see cref="ManagementEventWatcher"/>
/// subscriptions on <c>Win32_ProcessStartTrace</c> and <c>Win32_ProcessStopTrace</c> for
/// <c>wsl.exe</c>. Fires <see cref="CacheInvalidationRequested"/> after a 2-second debounce window.
/// </summary>
public sealed class WslEventWatcher : IWslEventWatcher
{
    private readonly ILogger<WslEventWatcher> _logger;
    private readonly int _debounceMs;

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private readonly System.Timers.Timer _debounce;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler? CacheInvalidationRequested;

    public WslEventWatcher(ILogger<WslEventWatcher> logger, int debounceMs = 2000)
    {
        _logger     = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceMs = debounceMs;
        _debounce   = new System.Timers.Timer(_debounceMs) { AutoReset = false };
        _debounce.Elapsed += (_, _) =>
        {
            _logger.LogInformation("WslEventWatcher: debounce elapsed — raising CacheInvalidationRequested");
            CacheInvalidationRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <inheritdoc/>
    public void Start()
    {
        _startWatcher = new ManagementEventWatcher(
            new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = 'wsl.exe'"));
        _startWatcher.EventArrived += OnWslEvent;
        _startWatcher.Start();

        _stopWatcher = new ManagementEventWatcher(
            new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName = 'wsl.exe'"));
        _stopWatcher.EventArrived += OnWslEvent;
        _stopWatcher.Start();

        _logger.LogDebug("WslEventWatcher started (debounce={Debounce}ms)", _debounceMs);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _debounce.Stop();
        _startWatcher?.Stop();
        _stopWatcher?.Stop();
        _logger.LogDebug("WslEventWatcher stopped");
    }

    /// <inheritdoc/>
    public void SimulateProcessEvent(string processName)
    {
        _logger.LogDebug("WslEventWatcher: process event for '{Process}' — scheduling debounced refresh", processName);
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnWslEvent(object sender, EventArrivedEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    internal void FireCacheInvalidatedForTest()
        => CacheInvalidationRequested?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _debounce.Dispose();
        _startWatcher?.Dispose();
        _stopWatcher?.Dispose();
    }
}
