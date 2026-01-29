namespace DistroNexus.Core.Models;

/// <summary>
/// Represents the result of a PowerShell script execution.
/// </summary>
public class PowerShellScriptResult
{
    /// <summary>
    /// Gets the exit code from the PowerShell process.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets the standard output from the PowerShell script.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Gets the standard error from the PowerShell script.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the script executed successfully.
    /// </summary>
    public bool Success => ExitCode == 0;
}