using System.Text.Json;

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
    public bool Success => ExitCode == 0 && string.IsNullOrWhiteSpace(Error);

    /// <summary>
    /// Gets the parsed PowerShell objects from JSON output.
    /// </summary>
    public List<JsonElement>? ParsedObjects { get; set; }

    /// <summary>
    /// Gets or sets whether the command was executed via module (true) or inline script (false).
    /// </summary>
    public bool UsedModule { get; set; } = false;

    /// <summary>
    /// Gets or sets the exception that occurred during script execution.
    /// </summary>
    public Exception? Exception { get; set; }
}
