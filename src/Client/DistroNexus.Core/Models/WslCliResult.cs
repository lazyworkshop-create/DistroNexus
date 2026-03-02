namespace DistroNexus.Core.Models;

/// <summary>
/// Result of a direct wsl.exe CLI invocation.
/// </summary>
public class WslCliResult
{
    /// <summary>Process exit code (0 = success).</summary>
    public int ExitCode { get; set; }

    /// <summary>Standard output captured from wsl.exe.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Standard error captured from wsl.exe.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>True when ExitCode is 0.</summary>
    public bool Success => ExitCode == 0;
}
