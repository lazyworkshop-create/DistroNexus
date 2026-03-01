namespace DistroNexus.Core.Models;

/// <summary>
/// Represents global WSL configuration settings from ~/.wslconfig.
/// </summary>
public class WslConfig
{
    /// <summary>Maximum memory, e.g. "4GB" or "512MB".</summary>
    public string? Memory { get; set; }

    /// <summary>Number of CPU processors to allocate to WSL2 VMs.</summary>
    public int? Processors { get; set; }

    /// <summary>Swap size, e.g. "2GB".</summary>
    public string? Swap { get; set; }

    /// <summary>Whether localhost forwarding is enabled.</summary>
    public bool? LocalhostForwarding { get; set; }

    /// <summary>Network mode, e.g. "NAT" or "mirrored".</summary>
    public string? NetworkingMode { get; set; }
}
