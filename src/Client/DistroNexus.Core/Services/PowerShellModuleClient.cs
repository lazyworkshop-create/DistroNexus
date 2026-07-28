using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const string GetCatalogSourcesCommand = "Get-DistroNexusCatalogSource";
    private const string AddCatalogSourceCommand = "Add-DistroNexusCatalogSource";
    private const string UpdateCatalogSourceCommand = "Set-DistroNexusCatalogSource";
    private const string RemoveCatalogSourceCommand = "Remove-DistroNexusCatalogSource";
    private const string TestCatalogSourceCommand = "Test-DistroNexusCatalogSource";
    private const string SetCatalogSourceActiveCommand = "Set-DistroNexusCatalogSourceActive";
    private const string SetCatalogSourceOrderCommand = "Set-DistroNexusCatalogSourceOrder";
    private const string ResetCatalogSourcesCommand = "Reset-DistroNexusCatalogSource";
    private const string GetPackagesCommand = "Get-DistroNexusPackage";
    private const string RefreshCatalogCommand = "Update-DistroNexusCatalog";
    private const string GetPackageCacheLocationCommand = "Get-DistroNexusPackageCacheLocation";
    private const string GetPackageCacheUsageCommand = "Get-DistroNexusPackageCacheUsage";
    private const string RemovePackageCacheCommand = "Remove-DistroNexusPackage";
    private const string ClearPackageCacheCommand = "Clear-DistroNexusPackageCache";
    private const string GetContainerRuntimeStatusCommand = "Get-DistroNexusContainerRuntimeStatus";
    private const string GetCapabilityCommand = "Get-DistroNexusCapability";
    private const string GetPodmanUserUnitPreviewCommand = "Get-DistroNexusPodmanUserUnitPreview";
    private const string InvokePodmanUserUnitCommand = "Invoke-DistroNexusPodmanUserUnit";
    private const string GetPodmanConnectionPreviewCommand = "Get-DistroNexusPodmanConnectionPreview";
    private const string InvokePodmanConnectionCommand = "Invoke-DistroNexusPodmanConnection";
    private const string GetWslgStatusCommand = "Get-DistroNexusWslgStatus";
    private const string GetWslgApplicationsCommand = "Get-DistroNexusWslgApplication";
    private const string StartWslgApplicationCommand = "Start-DistroNexusWslgApplication";
    private const string RevealWslgApplicationCommand = "Show-DistroNexusWslgApplicationEntry";
    private const string SetWslgApplicationPinCommand = "Set-DistroNexusWslgApplicationPin";
    private const string GetDockerIntegrationCommand = "Get-DistroNexusDockerIntegration";
    private const string GetDockerIntegrationPreviewCommand = "Get-DistroNexusDockerIntegrationPreview";
    private const string SetDockerIntegrationCommand = "Set-DistroNexusDockerIntegration";
    private const string GetMonitoringSnapshotCommand = "Get-DistroNexusMonitoringSnapshot";
    private const string GetMonitoringProcessActionPreviewCommand = "Get-DistroNexusMonitoringProcessActionPreview";
    private const string InvokeMonitoringProcessActionCommand = "Invoke-DistroNexusMonitoringProcessAction";
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogSource>> GetCatalogSourcesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            GetCatalogSourcesCommand,
            options: new ModuleCallOptions { ParseAsJson = true },
            cancellationToken: cancellationToken);
        ThrowIfFailed(result);

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return Array.Empty<CatalogSource>();
        }

        return DeserializeCatalogSources(result.Output);
    }

    /// <inheritdoc />
    public async Task<CatalogSource> AddCatalogSourceAsync(
        DistroNexusCatalogSourceCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            AddCatalogSourceCommand,
            new Dictionary<string, object>
            {
                ["Name"] = request.Name,
                ["Url"] = request.Url,
                ["Description"] = request.Description ?? string.Empty,
                ["IsActive"] = request.IsActive
            },
            new ModuleCallOptions { ParseAsJson = true },
            cancellationToken);
        ThrowIfFailed(result);
        return DeserializeCatalogSource(result.Output);
    }

    /// <inheritdoc />
    public async Task<CatalogSource> UpdateCatalogSourceAsync(
        DistroNexusCatalogSourceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            UpdateCatalogSourceCommand,
            new Dictionary<string, object>
            {
                ["SourceId"] = request.SourceId,
                ["Name"] = request.Name,
                ["Url"] = request.Url,
                ["Description"] = request.Description ?? string.Empty,
                ["IsActive"] = request.IsActive
            },
            new ModuleCallOptions { ParseAsJson = true },
            cancellationToken);
        ThrowIfFailed(result);
        return DeserializeCatalogSource(result.Output);
    }

    /// <inheritdoc />
    public Task<bool> RemoveCatalogSourceAsync(string sourceId, CancellationToken cancellationToken = default) =>
        ExecuteCatalogSourceBooleanMutationAsync(RemoveCatalogSourceCommand, new Dictionary<string, object> { ["SourceId"] = sourceId }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TestCatalogSourceAsync(string url, CancellationToken cancellationToken = default) =>
        ExecuteCatalogSourceBooleanMutationAsync(TestCatalogSourceCommand, new Dictionary<string, object> { ["Url"] = url }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetCatalogSourceActiveAsync(string sourceId, bool isActive, CancellationToken cancellationToken = default) =>
        ExecuteCatalogSourceBooleanMutationAsync(SetCatalogSourceActiveCommand, new Dictionary<string, object> { ["SourceId"] = sourceId, ["IsActive"] = isActive }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ReorderCatalogSourcesAsync(IReadOnlyList<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceIds);
        return ExecuteCatalogSourceBooleanMutationAsync(SetCatalogSourceOrderCommand, new Dictionary<string, object> { ["SourceId"] = sourceIds.ToArray() }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ResetCatalogSourcesAsync(CancellationToken cancellationToken = default) =>
        ExecuteCatalogSourceBooleanMutationAsync(ResetCatalogSourcesCommand, null, cancellationToken);

    public async Task<IReadOnlyList<DistroPackage>> GetPackagesAsync(string? family = null, bool forceReload = false, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(family)) parameters["Family"] = family;
        if (forceReload) parameters["ForceReload"] = true;
        return await ExecutePackagesAsync(parameters.Count == 0 ? null : parameters, cancellationToken);
    }

    public Task<IReadOnlyList<DistroPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default) =>
        ExecutePackagesAsync(new Dictionary<string, object> { ["Query"] = query }, cancellationToken);

    public async Task<DistroPackage?> GetPackageAsync(string id, CancellationToken cancellationToken = default)
    {
        var packages = await ExecutePackagesAsync(new Dictionary<string, object> { ["Id"] = id }, cancellationToken);
        return packages.FirstOrDefault();
    }

    public async Task<DistroNexusCatalogRefreshResult> RefreshCatalogAsync(string? sourceUrl = null, CancellationToken cancellationToken = default)
    {
        Dictionary<string, object>? parameters = string.IsNullOrWhiteSpace(sourceUrl) ? null : new() { ["SourceUrl"] = sourceUrl };
        var result = await _powerShellService.ExecuteModuleCmdletAsync(RefreshCatalogCommand, parameters, cancellationToken: cancellationToken);
        ThrowIfFailed(result);
        return JsonSerializer.Deserialize<DistroNexusCatalogRefreshResult>(result.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The DistroNexus module returned an invalid catalog refresh result.");
    }

    public async Task<PackageCacheLocationResult> GetPackageCacheLocationAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(GetPackageCacheLocationCommand, options: new ModuleCallOptions { ParseAsJson = true }, cancellationToken: cancellationToken);
        ThrowIfFailed(result);
        return JsonSerializer.Deserialize<PackageCacheLocationResult>(result.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("The module returned an invalid package cache location.");
    }

    public async Task<CacheUsageInfo> GetPackageCacheUsageAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(GetPackageCacheUsageCommand, options: new ModuleCallOptions { ParseAsJson = true }, cancellationToken: cancellationToken);
        ThrowIfFailed(result);
        return JsonSerializer.Deserialize<CacheUsageInfo>(result.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("The module returned an invalid package cache usage result.");
    }

    public async Task<PackageCacheDeleteResult> DeletePackageCacheEntryAsync(string cacheEntryId, CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(RemovePackageCacheCommand, new Dictionary<string, object> { ["CacheEntryId"] = cacheEntryId }, new ModuleCallOptions { ParseAsJson = true }, cancellationToken);
        ThrowIfFailed(result);
        return JsonSerializer.Deserialize<PackageCacheDeleteResult>(result.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("The module returned an invalid package cache delete result.");
    }

    public async Task<PackageCacheClearResult> ClearPackageCacheAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(ClearPackageCacheCommand, options: new ModuleCallOptions { ParseAsJson = true }, cancellationToken: cancellationToken);
        ThrowIfFailed(result);
        return JsonSerializer.Deserialize<PackageCacheClearResult>(result.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("The module returned an invalid package cache clear result.");
    }

    public async Task<ContainerRuntimeSnapshot> GetContainerRuntimeStatusAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name, nameof(name));
        return await ExecuteJsonAsync<ContainerRuntimeSnapshot>(GetContainerRuntimeStatusCommand, new() { ["Name"] = name }, cancellationToken);
    }

    public async Task<InstanceCapabilitySnapshot> GetInstanceCapabilitiesAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name, nameof(name));
        return await ExecuteJsonAsync<InstanceCapabilitySnapshot>(GetCapabilityCommand, new() { ["Name"] = name }, cancellationToken);
    }

    public async Task<PlatformCapabilitySnapshot> GetHostCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        await ExecuteJsonAsync<PlatformCapabilitySnapshot>(GetCapabilityCommand, new() { ["Host"] = true }, cancellationToken);

    public async Task<DistroNexusPodmanUserUnitPreview> GetPodmanUserUnitPreviewAsync(string name, PodmanUserUnit unit, SystemdAction action, CancellationToken cancellationToken = default)
    {
        ValidateName(name, nameof(name));
        ValidatePodmanUnitAction(unit, action);
        return await ExecuteJsonAsync<DistroNexusPodmanUserUnitPreview>(GetPodmanUserUnitPreviewCommand, new() { ["Name"] = name, ["Unit"] = unit.ToString(), ["Action"] = action.ToString() }, cancellationToken);
    }

    public async Task<DistroNexusPodmanUserUnitResult> InvokePodmanUserUnitAsync(DistroNexusPodmanUserUnitPreview preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ValidateName(preview.InstanceName, nameof(preview));
        ValidatePodmanUnitAction(preview.Unit, preview.Action);
        if (string.IsNullOrWhiteSpace(preview.Token)) throw new ArgumentException("A Core-issued Podman preview token is required.", nameof(preview));
        return await ExecuteJsonAsync<DistroNexusPodmanUserUnitResult>(InvokePodmanUserUnitCommand, new() { ["PreviewToken"] = preview.Token, ["InstanceName"] = preview.InstanceName, ["Unit"] = preview.Unit.ToString(), ["Action"] = preview.Action.ToString() }, cancellationToken);
    }

    public async Task<DistroNexusPodmanConnectionPreview> GetPodmanConnectionPreviewAsync(string name, PodmanConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateName(name, nameof(name));
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        return await ExecuteJsonAsync<DistroNexusPodmanConnectionPreview>(GetPodmanConnectionPreviewCommand, new() { ["Name"] = name, ["ConnectionName"] = request.Name, ["Endpoint"] = request.SafeEndpoint }, cancellationToken);
    }

    public async Task<PodmanConnectionResult> InvokePodmanConnectionAsync(DistroNexusPodmanConnectionPreview preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ValidateName(preview.InstanceName, nameof(preview));
        if (string.IsNullOrWhiteSpace(preview.Token)) throw new ArgumentException("A Core-issued Podman preview token is required.", nameof(preview));
        var request = new PodmanConnectionRequest(preview.Name, new Uri(preview.Endpoint, UriKind.Absolute));
        request.Validate();
        return await ExecuteJsonAsync<PodmanConnectionResult>(InvokePodmanConnectionCommand, new() { ["PreviewToken"] = preview.Token, ["InstanceName"] = preview.InstanceName, ["ConnectionName"] = request.Name, ["Endpoint"] = request.SafeEndpoint }, cancellationToken);
    }

    public Task<WslgApplicationStatus> GetWslgStatusAsync(string name, CancellationToken cancellationToken = default)
    { ValidateName(name, nameof(name)); return ExecuteJsonAsync<WslgApplicationStatus>(GetWslgStatusCommand, new() { ["Name"] = name }, cancellationToken); }
    public Task<WslgDiscoveryResult> DiscoverWslgApplicationsAsync(string name, CancellationToken cancellationToken = default)
    { ValidateName(name, nameof(name)); return ExecuteJsonAsync<WslgDiscoveryResult>(GetWslgApplicationsCommand, new() { ["Name"] = name }, cancellationToken); }
    public Task<WslgActionResult> LaunchWslgApplicationAsync(string token, string applicationId, CancellationToken cancellationToken = default) => ExecuteWslgActionAsync(StartWslgApplicationCommand, token, applicationId, null, cancellationToken);
    public Task<WslgActionResult> RevealWslgApplicationAsync(string token, string applicationId, CancellationToken cancellationToken = default) => ExecuteWslgActionAsync(RevealWslgApplicationCommand, token, applicationId, null, cancellationToken);
    public Task<WslgActionResult> SetWslgApplicationPinAsync(string token, string applicationId, bool pinned, CancellationToken cancellationToken = default) => ExecuteWslgActionAsync(SetWslgApplicationPinCommand, token, applicationId, pinned, cancellationToken);
    public Task<DockerIntegrationSnapshot> GetDockerIntegrationAsync(string name, CancellationToken cancellationToken = default)
    { ValidateName(name, nameof(name)); return ExecuteJsonAsync<DockerIntegrationSnapshot>(GetDockerIntegrationCommand, new() { ["Name"] = name }, cancellationToken); }
    public Task<DockerIntegrationPreview> GetDockerIntegrationPreviewAsync(string name, bool enabled, CancellationToken cancellationToken = default)
    { ValidateName(name, nameof(name)); return ExecuteJsonAsync<DockerIntegrationPreview>(GetDockerIntegrationPreviewCommand, new() { ["Name"] = name, ["Enabled"] = enabled }, cancellationToken); }
    public Task<DockerIntegrationResult> SetDockerIntegrationAsync(string name, bool enabled, string previewToken, CancellationToken cancellationToken = default)
    { ValidateName(name, nameof(name)); if (string.IsNullOrWhiteSpace(previewToken) || previewToken.Length != 64 || previewToken.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("A Core-issued Docker integration preview token is required.", nameof(previewToken)); return ExecuteJsonAsync<DockerIntegrationResult>(SetDockerIntegrationCommand, new() { ["Name"] = name, ["Enabled"] = enabled, ["Preview"] = previewToken }, cancellationToken); }
    public Task<MonitoringSnapshotResult> GetMonitoringSnapshotAsync(string name, int intervalSeconds, CancellationToken cancellationToken = default)
    { ValidateName(name, nameof(name)); if (intervalSeconds is not (1 or 2 or 5 or 10)) throw new ArgumentOutOfRangeException(nameof(intervalSeconds)); return ExecuteJsonAsync<MonitoringSnapshotResult>(GetMonitoringSnapshotCommand, new() { ["Name"] = name, ["IntervalSeconds"] = intervalSeconds }, cancellationToken); }
    public Task<MonitoringProcessActionPreview> GetMonitoringProcessActionPreviewAsync(string snapshotToken, int processId, MonitoringProcessAction action, CancellationToken cancellationToken = default)
    { ValidateToken(snapshotToken, nameof(snapshotToken)); if (processId <= 1 || action is not (MonitoringProcessAction.Terminate or MonitoringProcessAction.Kill or MonitoringProcessAction.Renice)) throw new ArgumentOutOfRangeException(nameof(processId)); return ExecuteJsonAsync<MonitoringProcessActionPreview>(GetMonitoringProcessActionPreviewCommand, new() { ["SnapshotToken"] = snapshotToken, ["ProcessId"] = processId, ["Action"] = action.ToString() }, cancellationToken); }
    public Task<ProcessActionResult> InvokeMonitoringProcessActionAsync(string previewToken, CancellationToken cancellationToken = default)
    { ValidateToken(previewToken, nameof(previewToken)); return ExecuteJsonAsync<ProcessActionResult>(InvokeMonitoringProcessActionCommand, new() { ["PreviewToken"] = previewToken }, cancellationToken); }
    private Task<WslgActionResult> ExecuteWslgActionAsync(string command, string token, string applicationId, bool? pinned, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || token.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("A WSLg discovery token is invalid.", nameof(token)); ValidateName(applicationId, nameof(applicationId)); var parameters = new Dictionary<string, object> { ["DiscoveryToken"] = token, ["ApplicationId"] = applicationId }; if (pinned is not null) parameters["Pinned"] = pinned.Value; return ExecuteJsonAsync<WslgActionResult>(command, parameters, ct); }

    private async Task<IReadOnlyList<DistroPackage>> ExecutePackagesAsync(Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(GetPackagesCommand, parameters, new ModuleCallOptions { ParseAsJson = true }, cancellationToken);
        if (!result.Success) throw result.Exception ?? new InvalidOperationException(result.Error ?? "The DistroNexus module operation failed.");
        if (string.IsNullOrWhiteSpace(result.Output)) return Array.Empty<DistroPackage>();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return result.Output.TrimStart().StartsWith('[')
            ? JsonSerializer.Deserialize<List<DistroPackage>>(result.Output, options) ?? []
            : [JsonSerializer.Deserialize<DistroPackage>(result.Output, options) ?? throw new InvalidOperationException("The module returned an invalid package result.")];
    }

    private async Task<T> ExecuteJsonAsync<T>(string command, Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(command, parameters, new ModuleCallOptions { ParseAsJson = true }, cancellationToken);
        ThrowIfFailed(result);
        return JsonSerializer.Deserialize<T>(result.Output, JsonOptions)
            ?? throw new InvalidOperationException("The DistroNexus module returned an invalid result.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    private static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("The instance name is invalid.", parameterName);
    }
    private static void ValidateToken(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("A Core-issued monitoring token is required.", parameterName);
    }

    private static void ValidatePodmanUnitAction(PodmanUserUnit unit, SystemdAction action)
    {
        if (unit is not (PodmanUserUnit.Service or PodmanUserUnit.Socket) || action is not (SystemdAction.Start or SystemdAction.Stop)) throw new ArgumentOutOfRangeException(nameof(action));
    }

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

    private async Task<bool> ExecuteCatalogSourceBooleanMutationAsync(
        string command,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        var result = await _powerShellService.ExecuteModuleCmdletAsync(command, parameters, cancellationToken: cancellationToken);
        ThrowIfFailed(result);
        if (bool.TryParse(result.Output?.Trim(), out var success))
        {
            return success;
        }

        throw new InvalidOperationException("The DistroNexus module operation returned an invalid catalog source result.");
    }

    private static void ThrowIfFailed(PowerShellScriptResult result)
    {
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

    private static IReadOnlyList<CatalogSource> DeserializeCatalogSources(string output)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<CatalogSource>>(output, options) ?? new List<CatalogSource>();
        }

        return [DeserializeCatalogSource(output)];
    }

    private static CatalogSource DeserializeCatalogSource(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("The DistroNexus module operation returned no catalog source result.");
        }

        return JsonSerializer.Deserialize<CatalogSource>(output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The DistroNexus module operation returned an invalid catalog source result.");
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
