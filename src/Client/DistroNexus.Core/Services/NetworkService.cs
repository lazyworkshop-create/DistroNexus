using System.Text.Json;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Queries WSL instance port mappings via the Get-DistroNexusPortMapping PowerShell cmdlet.
/// </summary>
public class NetworkService : INetworkService
{
    private readonly IPowerShellService _powerShellService;
    private readonly ILogger<NetworkService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public NetworkService(IPowerShellService powerShellService, ILogger<NetworkService> logger)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _logger            = logger            ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<List<PortMapping>> GetPortMappingsAsync(
        string instanceName,
        string? protocol = null,
        CancellationToken cancellationToken = default)
    {
        if (instanceName is null) throw new ArgumentNullException(nameof(instanceName));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new WslOperationFailedException(
                "Instance name must not be empty.",
                DistroNexusErrorCode.InstanceNotFound,
                operation: "GetPortMappings");

        var parameters = new Dictionary<string, object> { ["Name"] = instanceName };
        if (!string.IsNullOrWhiteSpace(protocol))
            parameters["Protocol"] = protocol;

        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusPortMapping",
            parameters,
            new ModuleCallOptions { TimeoutSeconds = 30 },
            cancellationToken);

        if (result.ExitCode != 0)
            throw new WslOperationFailedException(
                $"Port mapping query failed for '{instanceName}': {result.Error}",
                DistroNexusErrorCode.PowerShellModuleUnavailable,
                operation: "GetPortMappings",
                instanceName: instanceName);

        if (string.IsNullOrWhiteSpace(result.Output))
            return [];

        try
        {
            var mappings = JsonSerializer.Deserialize<List<PortMapping>>(result.Output, _jsonOptions);
            return mappings ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse port mapping output for '{Name}'", instanceName);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetInstanceIpAddressAsync(
        string instanceName,
        CancellationToken cancellationToken = default)
    {
        if (instanceName is null) throw new ArgumentNullException(nameof(instanceName));

        // GetPortMappingsAsync returns InstanceIpAddress in the results;
        // call it with a quick protocol filter so we get the IP without full enumeration.
        var mappings = await GetPortMappingsAsync(instanceName, null, cancellationToken);
        return mappings.FirstOrDefault()?.InstanceIpAddress;
    }
}
