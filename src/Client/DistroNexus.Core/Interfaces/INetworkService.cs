using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Queries port mapping information for WSL instances.
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Returns the listening ports inside a running WSL instance.
    /// </summary>
    /// <param name="instanceName">The WSL instance name.</param>
    /// <param name="protocol">Optional filter: "TCP", "UDP", or null for all.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<List<PortMapping>> GetPortMappingsAsync(
        string instanceName,
        string? protocol = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current WSL IP address for the given instance.
    /// </summary>
    /// <param name="instanceName">The WSL instance name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<string?> GetInstanceIpAddressAsync(
        string instanceName,
        CancellationToken cancellationToken = default);
}
