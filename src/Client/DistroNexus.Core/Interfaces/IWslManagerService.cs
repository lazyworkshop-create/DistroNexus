using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for managing WSL instances.
/// </summary>
public interface IWslManagerService
{
    /// <summary>
    /// Gets all WSL instances on the system.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of WSL instances.</returns>
    Task<List<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a new WSL distribution instance.
    /// </summary>
    /// <param name="options">The installation options.</param>
    /// <param name="progress">Progress reporter for the installation (percentage and message).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task InstallInstanceAsync(InstallOptions options, IProgress<(double Percentage, string Message)>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a WSL instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to start.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the instance was started successfully, otherwise false.</returns>
    Task<bool> StartInstanceAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a WSL instance with a background keep-alive process to prevent auto-shutdown.
    /// </summary>
    /// <param name="instanceName">The name of the instance to start.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the instance was started successfully, otherwise false.</returns>
    Task<bool> StartInstanceWithKeepAliveAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running WSL instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance to stop.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the instance was stopped successfully, otherwise false.</returns>
    Task<bool> StopInstanceAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a WSL instance from the system.
    /// </summary>
    /// <param name="instanceName">The name of the instance to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the instance was removed successfully, otherwise false.</returns>
    Task<bool> RemoveInstanceAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a WSL instance to a new location.
    /// </summary>
    /// <param name="instanceName">The name of the instance to move.</param>
    /// <param name="newPath">The new installation path.</param>
    /// <param name="progress">Progress reporter for the move operation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task MoveInstanceAsync(string instanceName, string newPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a WSL instance.
    /// </summary>
    /// <param name="oldName">The current name of the instance.</param>
    /// <param name="newName">The new name for the instance.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RenameInstanceAsync(string oldName, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or updates the credentials for a WSL instance.
    /// </summary>
    /// <param name="instanceName">The name of the instance.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SetCredentialsAsync(string instanceName, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the disk size of a WSL instance by reading the VHDX file.
    /// WARNING: This may auto-start a stopped instance. Only call when the instance is already running.
    /// </summary>
    /// <param name="instanceName">The name of the instance.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The disk size in bytes, or 0 if unable to determine.</returns>
    Task<long> GetInstanceDiskSizeAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a complete refresh of a specific instance, including starting it and loading full information.
    /// This method will start the instance if it is stopped and load its disk size information.
    /// </summary>
    /// <param name="instanceName">The name of the instance to refresh.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The refreshed instance information, or null if the operation failed.</returns>
    Task<WslInstance?> ForceRefreshInstanceAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compacts the VHDX disk of a WSL instance to reclaim unused space.
    /// Runs fstrim inside the instance, then compacts the VHDX file using Optimize-VHD or diskpart.
    /// </summary>
    /// <param name="instanceName">The name of the instance to compact.</param>
    /// <param name="progress">Optional progress reporter receiving (percentage, phase message).</param>
    /// <param name="whatIf">If true, report estimated savings without performing compaction.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task CompactInstanceAsync(string instanceName, IProgress<(double Percentage, string Message)>? progress = null, bool whatIf = false, CancellationToken cancellationToken = default);
}
