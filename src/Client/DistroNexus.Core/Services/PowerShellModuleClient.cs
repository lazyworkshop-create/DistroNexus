using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>
/// Executes the registered DistroNexus module operations available to the desktop client.
/// </summary>
public sealed class PowerShellModuleClient : IPowerShellModuleClient
{
    private const string GetInstanceTagsCommand = "Get-DistroNexusInstanceTag";
    private readonly IPowerShellService _powerShellService;

    public PowerShellModuleClient(IPowerShellService powerShellService)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DistroNexusInstanceTagResult>> GetInstanceTagsAsync(
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = string.IsNullOrWhiteSpace(name)
            ? null
            : new Dictionary<string, object> { ["Name"] = name };

        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            GetInstanceTagsCommand,
            parameters,
            new ModuleCallOptions { ParseAsJson = true },
            cancellationToken);

        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        }

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return Array.Empty<DistroNexusInstanceTagResult>();
        }

        return DeserializeTagResults(result.Output);
    }

    private static IReadOnlyList<DistroNexusInstanceTagResult> DeserializeTagResults(string output)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var results = JsonSerializer.Deserialize<List<DistroNexusInstanceTagResult>>(output, options);
            return results is null ? Array.Empty<DistroNexusInstanceTagResult>() : results;
        }

        var result = JsonSerializer.Deserialize<DistroNexusInstanceTagResult>(output, options);
        return result is null ? Array.Empty<DistroNexusInstanceTagResult>() : [result];
    }
}
