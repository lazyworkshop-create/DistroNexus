namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a listening port inside a WSL instance.
/// </summary>
public class PortMapping
{
    /// <summary>Protocol: TCP or UDP.</summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>Local address the port is bound to (e.g., "0.0.0.0" or "::").</summary>
    public string LocalAddress { get; set; } = string.Empty;

    /// <summary>Port number.</summary>
    public int Port { get; set; }

    /// <summary>Process name owning the port inside WSL.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>PID of the owning process inside WSL.</summary>
    public int Pid { get; set; }

    /// <summary>Whether a matching Windows <c>netsh portproxy</c> rule exists for this port.</summary>
    public bool HasWindowsProxy { get; set; }

    /// <summary>The WSL instance's IP address at the time of the query.</summary>
    public string? InstanceIpAddress { get; set; }

    /// <summary>Address family as reported by the Linux listener (IPv4 or IPv6).</summary>
    public string AddressFamily { get; set; } = string.Empty;

    /// <summary>Whether Windows currently owns a colliding listener on the same protocol and port.</summary>
    public bool HasWindowsCollision { get; set; }

    /// <summary>Human-readable, copyable remediation for a collision when known.</summary>
    public string? ConflictGuidance { get; set; }
}
