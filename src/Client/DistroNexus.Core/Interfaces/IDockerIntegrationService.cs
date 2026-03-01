using System.Threading;
using System.Threading.Tasks;
using DistroNexus.Core.Services;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Service for querying and modifying Docker Desktop WSL integration settings.
/// </summary>
public interface IDockerIntegrationService
{
    /// <summary>
    /// Checks whether Docker Desktop is installed on the system.
    /// </summary>
    Task<bool> IsDockerDesktopInstalledAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the Docker integration status for a given WSL instance name.
    /// Returns <see cref="DockerIntegrationStatus.Unavailable"/> when Docker Desktop is not installed
    /// or the instance name is a reserved Docker distro (docker-desktop, docker-desktop-data).
    /// </summary>
    Task<DockerIntegrationStatus> GetIntegrationStatusAsync(string instanceName, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables Docker Desktop integration for a WSL instance by writing
    /// the <c>integratedWslDistros</c> array in Docker's settings JSON.
    /// </summary>
    Task SetIntegrationAsync(string instanceName, bool enabled, CancellationToken ct = default);
}
