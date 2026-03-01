namespace DistroNexus.Core.Services;

/// <summary>
/// Represents the Docker Desktop WSL 2 integration state of a WSL instance.
/// </summary>
public enum DockerIntegrationStatus
{
    /// <summary>Docker integration is active for this instance.</summary>
    Enabled,

    /// <summary>Docker integration is not active for this instance.</summary>
    Disabled,

    /// <summary>
    /// Docker Desktop is not installed, the instance is a reserved Docker distro,
    /// or the settings file cannot be accessed.
    /// </summary>
    Unavailable
}
