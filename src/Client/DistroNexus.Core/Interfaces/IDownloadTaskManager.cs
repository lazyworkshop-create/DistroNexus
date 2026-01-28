using DistroNexus.Core.Models;
using System.Collections.ObjectModel;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides global download task management functionality.
/// </summary>
public interface IDownloadTaskManager
{
    /// <summary>
    /// Gets the observable collection of all download tasks.
    /// </summary>
    ObservableCollection<DownloadTask> Tasks { get; }
    
    /// <summary>
    /// Adds a single download task to the queue.
    /// </summary>
    /// <param name="package">The package to download.</param>
    /// <param name="destinationPath">The destination path for the download.</param>
    /// <returns>The created download task.</returns>
    DownloadTask AddTask(DistroPackage package, string destinationPath);
    
    /// <summary>
    /// Adds multiple download tasks to the queue.
    /// </summary>
    /// <param name="packages">The packages to download.</param>
    /// <returns>A list of created download tasks.</returns>
    List<DownloadTask> AddTasks(IEnumerable<DistroPackage> packages);
    
    /// <summary>
    /// Cancels a download task.
    /// </summary>
    /// <param name="taskId">The ID of the task to cancel.</param>
    Task CancelTaskAsync(Guid taskId);
    
    /// <summary>
    /// Retries a failed download task.
    /// </summary>
    /// <param name="taskId">The ID of the task to retry.</param>
    Task RetryTaskAsync(Guid taskId);
    
    /// <summary>
    /// Clears all completed, failed, and cancelled tasks from the list.
    /// </summary>
    void ClearCompletedTasks();
    
    /// <summary>
    /// Gets the count of active (pending or downloading) tasks.
    /// </summary>
    /// <returns>The number of active tasks.</returns>
    int GetActiveTasksCount();
    
    /// <summary>
    /// Event fired when a task status changes.
    /// </summary>
    event EventHandler<DownloadTask>? TaskStatusChanged;
}
