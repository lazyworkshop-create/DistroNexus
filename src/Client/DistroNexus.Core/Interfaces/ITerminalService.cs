namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for launching and managing terminal instances.
/// </summary>
public interface ITerminalService
{
    /// <summary>
    /// Opens a terminal for the specified WSL instance.
    /// </summary>
    /// <param name="instanceName">The name of the WSL instance.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the terminal was opened successfully, otherwise false.</returns>
    Task<bool> OpenTerminalAsync(string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a terminal for the specified WSL instance in a specific working directory.
    /// </summary>
    /// <param name="instanceName">The name of the WSL instance.</param>
    /// <param name="workingDirectory">The working directory to start in.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the terminal was opened successfully, otherwise false.</returns>
    Task<bool> OpenTerminalInDirectoryAsync(string instanceName, string workingDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file explorer for the specified path.
    /// </summary>
    /// <param name="folderPath">The folder path to open.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the explorer was opened successfully, otherwise false.</returns>
    Task<bool> OpenFileExplorerAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of available terminal applications on the system.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of available terminal names.</returns>
    Task<List<string>> GetAvailableTerminalsAsync(CancellationToken cancellationToken = default);
}
