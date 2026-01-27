using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for managing WSL instances using PowerShell cmdlets.
/// </summary>
public class WslManagerService : IWslManagerService
{
    private readonly IPowerShellService _powerShellService;
    private readonly ILogger<WslManagerService> _logger;

    public WslManagerService(IPowerShellService powerShellService, ILogger<WslManagerService> logger)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<List<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving WSL instances");
            
            var instances = await _powerShellService.ExecuteAsync<List<WslInstance>>(
                "Get-WslInstance",
                cancellationToken: cancellationToken
            );

            return instances ?? new List<WslInstance>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve WSL instances");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task InstallInstanceAsync(InstallOptions options, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.InstanceName))
            throw new ArgumentException("Instance name is required", nameof(options));

        if (string.IsNullOrWhiteSpace(options.InstallPath))
            throw new ArgumentException("Install path is required", nameof(options));

        try
        {
            _logger.LogInformation("Installing WSL instance '{InstanceName}' to '{InstallPath}'", 
                options.InstanceName, options.InstallPath);

            var parameters = new Dictionary<string, object>
            {
                { "DistroName", options.InstanceName },
                { "InstallPath", options.InstallPath },
                { "Username", options.Username }
            };

            if (options.Password != null)
            {
                parameters.Add("Password", options.Password);
            }

            await _powerShellService.ExecuteAsync<object>(
                "Install-DistroNexusInstance",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("WSL instance '{InstanceName}' installed successfully", options.InstanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install WSL instance '{InstanceName}'", options.InstanceName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StartInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentNullException(nameof(instanceName));

        try
        {
            _logger.LogInformation("Starting WSL instance '{InstanceName}'", instanceName);

            var parameters = new Dictionary<string, object>
            {
                { "DistroName", instanceName }
            };

            await _powerShellService.ExecuteAsync<object>(
                "Start-WslInstance",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("WSL instance '{InstanceName}' started successfully", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WSL instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StopInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentNullException(nameof(instanceName));

        try
        {
            _logger.LogInformation("Stopping WSL instance '{InstanceName}'", instanceName);

            var parameters = new Dictionary<string, object>
            {
                { "DistroName", instanceName }
            };

            await _powerShellService.ExecuteAsync<object>(
                "Stop-WslInstance",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("WSL instance '{InstanceName}' stopped successfully", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop WSL instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentNullException(nameof(instanceName));

        try
        {
            _logger.LogInformation("Removing WSL instance '{InstanceName}'", instanceName);

            var parameters = new Dictionary<string, object>
            {
                { "DistroName", instanceName }
            };

            await _powerShellService.ExecuteAsync<object>(
                "Remove-WslInstance",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("WSL instance '{InstanceName}' removed successfully", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove WSL instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task MoveInstanceAsync(string instanceName, string newPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentNullException(nameof(instanceName));

        if (string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentNullException(nameof(newPath));

        try
        {
            _logger.LogInformation("Moving WSL instance '{InstanceName}' to '{NewPath}'", instanceName, newPath);

            var parameters = new Dictionary<string, object>
            {
                { "DistroName", instanceName },
                { "NewPath", newPath }
            };

            await _powerShellService.ExecuteAsync<object>(
                "Move-WslInstance",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("WSL instance '{InstanceName}' moved successfully", instanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move WSL instance '{InstanceName}'", instanceName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RenameInstanceAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldName))
            throw new ArgumentNullException(nameof(oldName));

        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentNullException(nameof(newName));

        try
        {
            _logger.LogInformation("Renaming WSL instance '{OldName}' to '{NewName}'", oldName, newName);

            var parameters = new Dictionary<string, object>
            {
                { "OldName", oldName },
                { "NewName", newName }
            };

            await _powerShellService.ExecuteAsync<object>(
                "Rename-WslInstance",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("WSL instance renamed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename WSL instance");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetCredentialsAsync(string instanceName, string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentNullException(nameof(instanceName));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentNullException(nameof(username));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentNullException(nameof(password));

        try
        {
            _logger.LogInformation("Setting credentials for WSL instance '{InstanceName}'", instanceName);

            var parameters = new Dictionary<string, object>
            {
                { "DistroName", instanceName },
                { "Username", username },
                { "Password", password }
            };

            await _powerShellService.ExecuteAsync<object>(
                "Set-WslCredentials",
                parameters,
                cancellationToken
            );

            _logger.LogInformation("Credentials set successfully for WSL instance '{InstanceName}'", instanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set credentials for WSL instance '{InstanceName}'", instanceName);
            throw;
        }
    }
}
