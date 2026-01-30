using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for executing PowerShell commands and scripts.
/// </summary>
public interface IPowerShellService
{
    /// <summary>
    /// Executes a PowerShell cmdlet and returns the result.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="cmdlet">The cmdlet to execute.</param>
    /// <param name="parameters">Optional parameters for the cmdlet.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the cmdlet execution.</returns>
    Task<T?> ExecuteAsync<T>(string cmdlet, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PowerShell script and returns the output as a string.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The script output.</returns>
    Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PowerShell script and returns detailed result information.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The script execution result with exit code, output, and error information.</returns>
    Task<PowerShellScriptResult> ExecuteScriptWithResultAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the DistroNexus PowerShell module.
    /// </summary>
    /// <param name="modulePath">The path to the module.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ImportModuleAsync(string modulePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the DistroNexus PowerShell module is loaded.
    /// </summary>
    /// <returns>True if the module is loaded, otherwise false.</returns>
    Task<bool> IsModuleLoadedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a DistroNexus PowerShell module cmdlet and returns the raw result.
    /// </summary>
    /// <param name="cmdletName">Name of the cmdlet (e.g., "Get-DistroNexusInstance").</param>
    /// <param name="parameters">Cmdlet parameters.</param>
    /// <param name="options">Module call options.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>PowerShell execution result.</returns>
    Task<PowerShellScriptResult> ExecuteModuleCmdletAsync(
        string cmdletName,
        Dictionary<string, object>? parameters = null,
        ModuleCallOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a DistroNexus PowerShell module cmdlet with typed result.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="cmdletName">Name of the cmdlet (e.g., "Get-DistroNexusInstance").</param>
    /// <param name="parameters">Cmdlet parameters.</param>
    /// <param name="options">Module call options.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Parsed result of type T.</returns>
    Task<T?> ExecuteModuleCmdletAsync<T>(
        string cmdletName,
        Dictionary<string, object>? parameters = null,
        ModuleCallOptions? options = null,
        CancellationToken cancellationToken = default);
}
