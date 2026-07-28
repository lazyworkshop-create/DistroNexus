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
    private const string AddInstanceTagCommand = "Add-DistroNexusInstanceTag";
    private const string SetInstanceTagsCommand = "Set-DistroNexusInstanceTag";
    private const string RemoveInstanceTagCommand = "Remove-DistroNexusInstanceTag";
    private const string RenameInstanceTagsCommand = "Rename-DistroNexusInstanceTags";
    private const string GetInstancesCommand = "Get-DistroNexusInstance";
    private const string StartInstanceCommand = "Start-DistroNexusInstance";
    private const string StopInstanceCommand = "Stop-DistroNexusInstance";
    private const string GetSettingsCommand = "Get-DistroNexusSettings";
    private const string SetSettingsCommand = "Set-DistroNexusSettings";
    private const string ResetSettingsCommand = "Reset-DistroNexusSettings";
    private readonly IPowerShellService _powerShellService;

    public PowerShellModuleClient(IPowerShellService powerShellService)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            GetInstancesCommand,
            options: new ModuleCallOptions { ParseAsJson = true },
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        }

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return Array.Empty<WslInstance>();
        }

        return DeserializeInstances(result.Output);
    }

    /// <inheritdoc />
    public Task<bool> StartInstanceAsync(string name, CancellationToken cancellationToken = default) =>
        ExecuteInstanceMutationAsync(StartInstanceCommand, name, cancellationToken);

    /// <inheritdoc />
    public Task<bool> StopInstanceAsync(string name, CancellationToken cancellationToken = default) =>
        ExecuteInstanceMutationAsync(StopInstanceCommand, name, cancellationToken);

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

    /// <inheritdoc />
    public Task AddInstanceTagAsync(string name, string tag, CancellationToken cancellationToken = default) =>
        ExecuteTagMutationAsync(AddInstanceTagCommand, new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Tag"] = tag
        }, cancellationToken);

    /// <inheritdoc />
    public Task SetInstanceTagsAsync(string name, IReadOnlyList<string> tags, CancellationToken cancellationToken = default) =>
        ExecuteTagMutationAsync(SetInstanceTagsCommand, new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Tags"] = tags.ToArray()
        }, cancellationToken);

    /// <inheritdoc />
    public Task RemoveInstanceTagAsync(string name, string tag, CancellationToken cancellationToken = default) =>
        ExecuteTagMutationAsync(RemoveInstanceTagCommand, new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Tag"] = tag
        }, cancellationToken);

    /// <inheritdoc />
    public Task RenameInstanceTagsAsync(string oldName, string newName, CancellationToken cancellationToken = default) =>
        ExecuteTagMutationAsync(RenameInstanceTagsCommand, new Dictionary<string, object>
        {
            ["OldName"] = oldName,
            ["NewName"] = newName
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<GlobalSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            GetSettingsCommand,
            options: new ModuleCallOptions { ParseAsJson = true },
            cancellationToken: cancellationToken);
        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        }

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return new GlobalSettings();
        }

        return JsonSerializer.Deserialize<GlobalSettings>(result.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new GlobalSettings();
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(DistroNexusSettingsUpdate settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var parameters = SettingsParameters(settings);
        if (parameters.Count == 0)
        {
            throw new ArgumentException("Specify at least one modeled settings field.", nameof(settings));
        }

        await ExecuteSettingsMutationAsync(SetSettingsCommand, parameters, cancellationToken);
    }

    /// <inheritdoc />
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) =>
        ExecuteSettingsMutationAsync(ResetSettingsCommand, null, cancellationToken);

    private async Task ExecuteTagMutationAsync(
        string command,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(command, parameters, cancellationToken: cancellationToken);
        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        }
    }

    private async Task ExecuteSettingsMutationAsync(
        string command,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(command, parameters, cancellationToken: cancellationToken);
        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        }
    }

    private static Dictionary<string, object> SettingsParameters(DistroNexusSettingsUpdate settings)
    {
        var parameters = new Dictionary<string, object>();
        Add("DefaultInstallPath", settings.DefaultInstallPath);
        Add("PackageCachePath", settings.PackageCachePath);
        Add("TerminalStartPath", settings.TerminalStartPath);
        Add("DefaultWslVersion", settings.DefaultWslVersion);
        Add("DefaultUsername", settings.DefaultUsername);
        Add("DefaultDistributionId", settings.DefaultDistributionId);
        Add("EnableLogging", settings.EnableLogging);
        Add("LogPath", settings.LogPath);
        Add("CheckUpdatesOnStartup", settings.CheckUpdatesOnStartup);
        Add("CatalogUrl", settings.CatalogUrl);
        Add("Theme", settings.Theme);
        Add("Language", settings.Language);
        Add("ShowConfirmationDialogs", settings.ShowConfirmationDialogs);
        Add("MaxConcurrentDownloads", settings.MaxConcurrentDownloads);
        Add("AutoRetryDownloads", settings.AutoRetryDownloads);
        Add("MaxRetryAttempts", settings.MaxRetryAttempts);
        Add("AutoSaveEnabled", settings.AutoSaveEnabled);
        Add("AutoSaveInterval", settings.AutoSaveInterval);
        if (settings.UpdatePowerShellModulePath) parameters["PowerShellModulePath"] = settings.PowerShellModulePath!;
        return parameters;

        void Add(string name, object? value)
        {
            if (value is not null) parameters[name] = value;
        }
    }

    private async Task<bool> ExecuteInstanceMutationAsync(string command, string name, CancellationToken cancellationToken)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            command,
            new Dictionary<string, object> { ["Name"] = name },
            cancellationToken: cancellationToken);
        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        }

        if (bool.TryParse(result.Output.Trim(), out var success))
        {
            return success;
        }

        throw new InvalidOperationException("The DistroNexus module operation returned an invalid instance lifecycle result.");
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

    private static IReadOnlyList<WslInstance> DeserializeInstances(string output)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var document = JsonDocument.Parse(output);
        IReadOnlyList<ModuleInstanceResult?> results = document.RootElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<ModuleInstanceResult?>>(output, options) ?? []
            : [JsonSerializer.Deserialize<ModuleInstanceResult>(output, options)];

        return results.OfType<ModuleInstanceResult>().Select(result => new WslInstance
        {
            Name = result.Name ?? string.Empty,
            State = result.State ?? string.Empty,
            Version = result.Version,
            InstallPath = result.BasePath ?? string.Empty,
            Size = result.DiskSize,
            Distribution = result.Distribution ?? string.Empty,
            LastAccessed = result.InstallTime
        }).ToArray();
    }

    private sealed record ModuleInstanceResult(
        string? Name,
        string? State,
        int Version,
        string? BasePath,
        long DiskSize,
        DateTime? InstallTime,
        string? Distribution);
}
