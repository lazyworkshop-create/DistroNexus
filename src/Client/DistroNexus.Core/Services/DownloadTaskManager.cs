using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DistroNexus.Core.Services;

/// <summary>
/// Manages download tasks globally with concurrent download support.
/// </summary>
public class DownloadTaskManager : IDownloadTaskManager
{
    private readonly IDownloadService _downloadService;
    private readonly ICatalogService _catalogService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DownloadTaskManager> _logger;
    private readonly SemaphoreSlim _semaphore;
    
    /// <inheritdoc/>
    public ObservableCollection<DownloadTask> Tasks { get; } = new();
    
    /// <inheritdoc/>
    public event EventHandler<DownloadTask>? TaskStatusChanged;
    
    public DownloadTaskManager(
        IDownloadService downloadService,
        ICatalogService catalogService,
        ISettingsService settingsService,
        ILogger<DownloadTaskManager> logger)
    {
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load settings and configure max concurrent downloads
        var settings = _settingsService.LoadSettings();
        var maxConcurrent = settings.MaxConcurrentDownloads > 0 ? settings.MaxConcurrentDownloads : 3;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        _logger.LogInformation("DownloadTaskManager initialized with max concurrent downloads: {MaxConcurrent}", maxConcurrent);
    }
    
    /// <inheritdoc/>
    public DownloadTask AddTask(DistroPackage package, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var task = new DownloadTask
        {
            PackageId = package.Id,
            PackageName = package.Name,
            DownloadUrl = package.DownloadUrl,
            DestinationPath = destinationPath,
            TotalBytes = package.FileSize,
            Status = DownloadStatus.Pending,
            CancellationTokenSource = new CancellationTokenSource()
        };
        
        // Add to tasks collection (will be called from background thread)
        lock (Tasks)
        {
            Tasks.Add(task);
        }
        
        // Start download in background
        _ = Task.Run(() => ProcessTaskAsync(task));
        
        _logger.LogInformation("Added download task: {PackageName} ({PackageId})", package.Name, package.Id);
        return task;
    }
    
    /// <inheritdoc/>
    public List<DownloadTask> AddTasks(IEnumerable<DistroPackage> packages)
    {
        var tasks = new List<DownloadTask>();
        var settings = _settingsService.LoadSettings();

        foreach (var package in packages)
        {
            // Skip already cached packages
            if (package.IsCached)
            {
                _logger.LogInformation("Skipping cached package: {PackageName}", package.Name);
                continue;
            }
            
            var fileName = Path.GetFileName(new Uri(package.DownloadUrl).LocalPath);
            var destination = Path.Combine(settings.PackageCachePath, fileName);
            
            var task = AddTask(package, destination);
            tasks.Add(task);
        }
        
        _logger.LogInformation("Added {Count} download tasks", tasks.Count);
        return tasks;
    }
    
    /// <summary>
    /// Processes a download task with retry logic and progress tracking.
    /// </summary>
    private async Task ProcessTaskAsync(DownloadTask task)
    {
        // Wait for semaphore slot
        await _semaphore.WaitAsync(task.CancellationTokenSource!.Token);
        
        try
        {
            task.Status = DownloadStatus.Downloading;
            task.StartTime = DateTime.Now;
            OnTaskStatusChanged(task);
            
            _logger.LogInformation("Starting download: {PackageName}", task.PackageName);
            
            // Speed calculation variables
            var lastUpdate = DateTime.Now;
            var lastBytes = 0L;
            // Throttle speed updates to every 500ms
            var speedUpdateInterval = TimeSpan.FromMilliseconds(500); 
            
            // Create progress reporter
            var progress = new Progress<(long BytesRead, long TotalBytes)>(report =>
            {
                var now = DateTime.Now;
                var currentBytes = report.BytesRead;
                
                // Calculate percentage
                if (report.TotalBytes > 0)
                {
                    task.TotalBytes = report.TotalBytes;
                    task.Progress = (double)currentBytes / report.TotalBytes * 100.0;
                }
                
                task.DownloadedBytes = currentBytes;
                
                // Calculate speed and throttle heavy updates using local variable closure
                if (now - lastUpdate >= speedUpdateInterval)
                {
                    var deltaBytes = currentBytes - lastBytes;
                    var deltaSeconds = (now - lastUpdate).TotalSeconds;
                    
                    if (deltaSeconds > 0)
                    {
                        var bytesPerSecond = (long)(deltaBytes / deltaSeconds);
                        // Prevent unrealistic spikes
                        if (bytesPerSecond < 0) bytesPerSecond = 0;
                        
                        task.BytesPerSecond = bytesPerSecond;
                        task.FormattedSpeed = FormatSpeed(bytesPerSecond);
                        task.FormattedProgress = FormatProgress(currentBytes, task.TotalBytes);
                    }
                    
                    lastBytes = currentBytes;
                    lastUpdate = now;
                }
            });
            
            var settings = _settingsService.LoadSettings();
            var maxRetries = settings.MaxRetryAttempts;
            bool success = false;
            Exception? lastException = null;
            
            // Retry logic
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (task.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    task.Status = DownloadStatus.Cancelled;
                    _logger.LogInformation("Download cancelled: {PackageName}", task.PackageName);
                    OnTaskStatusChanged(task);
                    return;
                }
                
                try
                {
                    success = await _downloadService.DownloadFileAsync(
                        task.DownloadUrl,
                        task.DestinationPath,
                        progress,
                        task.CancellationTokenSource.Token);
                    
                    if (success)
                        break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    task.RetryCount = attempt + 1;
                    
                    if (attempt < maxRetries && settings.AutoRetryDownloads)
                    {
                        _logger.LogWarning(ex, "Download attempt {Attempt}/{Max} failed for {PackageName}. Retrying...",
                            attempt + 1, maxRetries + 1, task.PackageName);
                        
                        // Exponential backoff
                        await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), task.CancellationTokenSource.Token);
                    }
                }
            }
            
            if (success)
            {
                task.Status = DownloadStatus.Completed;
                task.Progress = 100;
                task.CompletedTime = DateTime.Now;
                task.DownloadedBytes = task.TotalBytes;
                task.FormattedSpeed = "Completed";
                
                _logger.LogInformation("Download completed: {PackageName}", task.PackageName);
                
                // Update package cache status
                await UpdatePackageCacheStatusAsync(task.PackageId, task.DestinationPath);
            }
            else
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = lastException?.Message ?? "Download failed";
                
                _logger.LogError(lastException, "Download failed after {Retries} retries: {PackageName}",
                    task.RetryCount, task.PackageName);
            }
            
            OnTaskStatusChanged(task);
        }
        catch (OperationCanceledException)
        {
            task.Status = DownloadStatus.Cancelled;
            _logger.LogInformation("Download cancelled: {PackageName}", task.PackageName);
            OnTaskStatusChanged(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error processing download task: {PackageName}", task.PackageName);
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = ex.Message;
            OnTaskStatusChanged(task);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Updates the cache status of a package in the catalog.
    /// </summary>
    private async Task UpdatePackageCacheStatusAsync(string packageId, string localPath)
    {
        try
        {
            var package = await _catalogService.GetDistributionByIdAsync(packageId);
            if (package != null)
            {
                // Update package properties
                lock (package)
                {
                    package.IsCached = true;
                    package.LocalPath = localPath;
                    package.IsDownloading = false;
                }
                
                _logger.LogInformation("Updated cache status for package: {PackageId}", packageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update package cache status for: {PackageId}", packageId);
        }
    }
    
    /// <inheritdoc/>
    public DownloadTask? GetTask(string taskId)
    {
        lock (Tasks)
        {
            return Tasks.FirstOrDefault(t => t.Id.ToString() == taskId);
        }
    }
    
    /// <inheritdoc/>
    public bool RemoveTask(string taskId)
    {
        lock (Tasks)
        {
            var task = Tasks.FirstOrDefault(t => t.Id.ToString() == taskId);
            if (task != null)
            {
                task.CancellationTokenSource?.Dispose();
                Tasks.Remove(task);
                _logger.LogInformation("Removed download task: {PackageName}", task.PackageName);
                return true;
            }
            return false;
        }
    }
    
    /// <inheritdoc/>
    public bool CancelTask(string taskId)
    {
        var task = GetTask(taskId);
        if (task != null && (task.Status == DownloadStatus.Downloading || task.Status == DownloadStatus.Pending))
        {
            task.CancellationTokenSource?.Cancel();
            if (task.Status == DownloadStatus.Pending)
            {
                task.Status = DownloadStatus.Cancelled;
                OnTaskStatusChanged(task);
            }
            _logger.LogInformation("Cancelled download task: {PackageName}", task.PackageName);
            return true;
        }
        return false;
    }
    
    /// <inheritdoc/>
    public async Task CancelTaskAsync(Guid taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null && task.Status == DownloadStatus.Downloading)
        {
            task.CancellationTokenSource?.Cancel();
            _logger.LogInformation("Cancelled download task: {PackageName}", task.PackageName);
        }
        
        await Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public async Task RetryTaskAsync(Guid taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null && task.Status == DownloadStatus.Failed)
        {
            task.Status = DownloadStatus.Pending;
            task.ErrorMessage = null;
            task.RetryCount = 0;
            task.Progress = 0;
            task.DownloadedBytes = 0;
            task.CancellationTokenSource = new CancellationTokenSource();
            
            _ = Task.Run(() => ProcessTaskAsync(task));
            _logger.LogInformation("Retrying download task: {PackageName}", task.PackageName);
        }
        
        await Task.CompletedTask;
    }
    
    private string FormatSpeed(long bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        int unitIndex = 0;
        double speed = bytesPerSecond;

        // Start from KB
        if (speed >= 1024)
        {
            speed /= 1024;
            unitIndex = 1;
        }

        while (speed >= 1024 && unitIndex < units.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }

        return $"{speed:0.#} {units[unitIndex]}";
    }

    private string FormatProgress(long currentBytes, long totalBytes)
    {
        return $"{FormatBytes(currentBytes)} / {FormatBytes(totalBytes)}";
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 0) return "Unknown";
        if (bytes == 0) return "0 B";
        
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unitIndex = 0;
        double size = bytes;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }

    /// <inheritdoc/>
    public void ClearCompletedTasks()
    {
        var completed = Tasks.Where(t => 
            t.Status == DownloadStatus.Completed || 
            t.Status == DownloadStatus.Failed || 
            t.Status == DownloadStatus.Cancelled).ToList();
        
        lock (Tasks)
        {
            foreach (var task in completed)
            {
                task.CancellationTokenSource?.Dispose();
                Tasks.Remove(task);
            }
        }
        
        _logger.LogInformation("Cleared {Count} completed tasks", completed.Count);
    }
    
    /// <inheritdoc/>
    public int GetActiveTasksCount()
    {
        return Tasks.Count(t => t.Status == DownloadStatus.Downloading || t.Status == DownloadStatus.Pending);
    }
    
    /// <summary>
    /// Raises the TaskStatusChanged event.
    /// </summary>
    private void OnTaskStatusChanged(DownloadTask task)
    {
        TaskStatusChanged?.Invoke(this, task);
    }
}
