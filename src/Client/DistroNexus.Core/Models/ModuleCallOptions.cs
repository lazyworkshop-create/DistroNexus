namespace DistroNexus.Core.Models;

/// <summary>
/// Options for PowerShell module cmdlet invocation.
/// </summary>
public class ModuleCallOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to fall back to inline script if module call fails.
    /// </summary>
    public bool UseModuleFallback { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout in seconds for the module call.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets a value indicating whether to log verbose output.
    /// </summary>
    public bool LogVerbose { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to parse output as JSON.
    /// </summary>
    public bool ParseAsJson { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to force refresh cache (applies to Get-DistroNexusInstance).
    /// </summary>
    public bool ForceRefresh { get; set; } = false;
}
