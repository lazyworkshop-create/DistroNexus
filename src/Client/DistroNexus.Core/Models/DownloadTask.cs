using CommunityToolkit.Mvvm.ComponentModel;

namespace DistroNexus.Core.Models;

/// <summary>
/// Represents the status of a download task.
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// Task is waiting in queue.
    /// </summary>
    Pending,
    
    /// <summary>
    /// Task is currently downloading.
    /// </summary>
    Downloading,
    
    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Task failed with an error.
    /// </summary>
    Failed,
    
    /// <summary>
    /// Task was cancelled by user.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a single download task with progress tracking.
/// </summary>
public partial class DownloadTask : ObservableObject
{
    /// <summary>
    /// Gets the unique identifier for this task.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Gets or sets the package ID.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the package name.
    /// </summary>
    public string PackageName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the download URL.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the destination file path.
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the current status of the download.
    /// </summary>
    [ObservableProperty]
    private DownloadStatus _status = DownloadStatus.Pending;
    
    /// <summary>
    /// Gets or sets the download progress (0-100).
    /// </summary>
    [ObservableProperty]
    private double _progress;
    
    /// <summary>
    /// Gets or sets the error message if the download failed.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;
    
    /// <summary>
    /// Gets or sets the number of bytes downloaded.
    /// </summary>
    [ObservableProperty]
    private long _downloadedBytes;
    
    /// <summary>
    /// Gets or sets the total file size in bytes.
    /// </summary>
    [ObservableProperty]
    private long _totalBytes;
    
    /// <summary>
    /// Gets or sets when the task was created.
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets or sets when the download started.
    /// </summary>
    public DateTime? StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets when the download completed.
    /// </summary>
    public DateTime? CompletedTime { get; set; }
    
    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the current download speed in bytes per second.
    /// </summary>
    [ObservableProperty]
    private long _bytesPerSecond;

    /// <summary>
    /// Gets or sets the formatted download speed string (e.g., "1.5 MB/s").
    /// </summary>
    [ObservableProperty]
    private string _formattedSpeed = string.Empty;

    /// <summary>
    /// Gets or sets the formatted progress string (e.g., "150 MB / 1.2 GB").
    /// </summary>
    [ObservableProperty]
    private string _formattedProgress = string.Empty;
    
    /// <summary>
    /// Gets or sets the cancellation token source for this task.
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    
    /// <summary>
    /// Gets the progress text for display.
    /// </summary>
    public string ProgressText => Status switch
    {
        DownloadStatus.Downloading => $"{Progress:F1}% ({FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)})",
        DownloadStatus.Completed => $"Completed ({FormatBytes(TotalBytes)})",
        DownloadStatus.Failed => $"Failed: {ErrorMessage}",
        DownloadStatus.Pending => "Waiting...",
        DownloadStatus.Cancelled => "Cancelled",
        _ => string.Empty
    };
    
    /// <summary>
    /// Formats bytes to human-readable format.
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:F2} {sizes[order]}";
    }
    
    /// <summary>
    /// Notifies that progress text has changed when status or progress changes.
    /// </summary>
    partial void OnStatusChanged(DownloadStatus value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }
    
    /// <summary>
    /// Notifies that progress text has changed when progress changes.
    /// </summary>
    partial void OnProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }
    
    /// <summary>
    /// Notifies that progress text has changed when downloaded bytes change.
    /// </summary>
    partial void OnDownloadedBytesChanged(long value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }
    
    /// <summary>
    /// Notifies that progress text has changed when total bytes change.
    /// </summary>
    partial void OnTotalBytesChanged(long value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }
}
