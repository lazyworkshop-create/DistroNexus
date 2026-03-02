using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Abstraction over direct wsl.exe process invocations for testability.
/// </summary>
public interface IWslCliRunner : IDisposable
{
    /// <summary>
    /// Runs wsl.exe with the specified arguments and returns the captured output.
    /// </summary>
    /// <param name="arguments">Arguments to pass to wsl.exe (e.g., "--list --verbose --all").</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<WslCliResult> RunAsync(string arguments, CancellationToken cancellationToken = default);
}
