namespace DistroNexus.Core.Models;

/// <summary>
/// Represents options for installing a WSL distribution.
/// </summary>
public class InstallOptions
{
    /// <summary>
    /// Gets or sets the name for the new WSL instance.
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distribution package to install.
    /// </summary>
    public DistroPackage? Package { get; set; }

    /// <summary>
    /// Gets or sets the installation path.
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username to create in the instance.
    /// </summary>
    public string Username { get; set; } = "root";

    /// <summary>
    /// Gets or sets the password for the user.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the WSL version to use (1 or 2).
    /// </summary>
    public int WslVersion { get; set; } = 2;

    /// <summary>
    /// Gets or sets whether to set this instance as the default.
    /// </summary>
    public bool SetAsDefault { get; set; }

    /// <summary>
    /// Gets or sets whether to launch the instance after installation.
    /// </summary>
    public bool LaunchAfterInstall { get; set; }

    /// <summary>
    /// Gets or sets custom initialization commands to run after installation.
    /// </summary>
    public List<string> InitCommands { get; set; } = new();
}
