using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Integrations tab of InstanceDetailDialog.
/// Handles Docker Desktop integration status and toggle (C-01).
/// </summary>
public partial class IntegrationsTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IDialogService _dialogService;
    private readonly IPowerShellModuleClient? _powerShellModuleClient;

    private bool _initialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContainerActionsEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPodmanServiceControlsEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPodmanConnectionEnabled))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private DockerIntegrationStatus _dockerStatus = DockerIntegrationStatus.Unavailable;

    [ObservableProperty]
    private bool _isDockerInstalled;

    [ObservableProperty]
    private bool _isDockerEnabled;

    [ObservableProperty]
    private bool _showRestartBanner;

    [ObservableProperty]
    private string _containerRuntimeSummary = string.Empty;
    [ObservableProperty]
    private string _containerRuntimeInventory = string.Empty;
    [ObservableProperty] private string _podmanConnectionName = "local";
    [ObservableProperty] private string _podmanConnectionEndpoint = "unix:///run/user/1000/podman/podman.sock";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPodmanServiceControlsEnabled))]
    private bool _isInstanceSystemdSupported;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContainerActionsEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPodmanServiceControlsEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPodmanConnectionEnabled))]
    private bool _isPodmanWslAvailable;
    [ObservableProperty]
    private string _podmanPrerequisiteMessage = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    /// <summary>Tab should be hidden for docker-desktop / docker-desktop-data instances.</summary>
    public bool IsTabVisible => !IsDockerSystemInstance(_instance.Name);

    public string StatusText => DockerStatus switch
    {
        DockerIntegrationStatus.Enabled  => Properties.Resources.IntegrationsTab_DockerEnabled,
        DockerIntegrationStatus.Disabled => Properties.Resources.IntegrationsTab_DockerDisabled,
        _                                => Properties.Resources.IntegrationsTab_DockerUnavailable
    };

    // True when toggle should be interactive.
    public bool IsToggleEnabled => IsDockerInstalled && Instance.IsWslV2 && !IsLoading;
    public bool IsContainerActionsEnabled => IsPodmanServiceControlsEnabled || IsPodmanConnectionEnabled;
    public bool IsPodmanServiceControlsEnabled => _powerShellModuleClient is not null && Instance.IsWslV2 && IsInstanceSystemdSupported && IsPodmanWslAvailable && !IsLoading;
    public bool IsPodmanConnectionEnabled => _powerShellModuleClient is not null && Instance.IsWslV2 && IsPodmanWslAvailable && !IsLoading;

    // WSL v1 guard message
    public bool ShowWslV1Message => !Instance.IsWslV2;

    public IntegrationsTabViewModel(
        WslInstanceViewModel instance,
        IDialogService dialogService,
        IPowerShellModuleClient powerShellModuleClient)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _powerShellModuleClient = powerShellModuleClient ?? throw new ArgumentNullException(nameof(powerShellModuleClient));
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (!IsTabVisible) return;

        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            var capabilities = await _powerShellModuleClient.GetInstanceCapabilitiesAsync(_instance.Name);
            IsInstanceSystemdSupported = capabilities.Capabilities.TryGetValue(CapabilityId.InstanceSystemd, out var systemd) && systemd.IsSupported;
            try
            {
                var docker = await _powerShellModuleClient.GetDockerIntegrationAsync(_instance.Name);
                IsDockerInstalled = docker.IsAvailable;

                if (IsDockerInstalled && Instance.IsWslV2)
                {
                    DockerStatus = docker.Status == "Enabled" ? DockerIntegrationStatus.Enabled : docker.Status == "Disabled" ? DockerIntegrationStatus.Disabled : DockerIntegrationStatus.Unavailable;
                    IsDockerEnabled = DockerStatus == DockerIntegrationStatus.Enabled;
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync(
                    Properties.Resources.ErrorTitle,
                    string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
            }
            if (_powerShellModuleClient is not null)
            {
                var snapshot = await _powerShellModuleClient.GetContainerRuntimeStatusAsync(_instance.Name);
                IsPodmanWslAvailable = snapshot.Runtimes.Any(x => x.Kind == ContainerRuntimeKind.PodmanWsl && x.Availability == ContainerRuntimeAvailability.Available);
                PodmanPrerequisiteMessage = PodmanPrerequisiteExplanation();
                ContainerRuntimeSummary = string.Join("; ", snapshot.Runtimes.Select(x => string.Format(R("IntegrationsTab_RuntimeSummary"), RuntimeLabel(x.Kind), AvailabilityLabel(x.Availability), HealthLabel(x.Health))));
                ContainerRuntimeInventory = string.Join(Environment.NewLine, snapshot.Runtimes.Select(x =>
                {
                    var containers = snapshot.Containers.GetValueOrDefault(x.Kind)?.Count ?? 0;
                    var images = snapshot.Images.GetValueOrDefault(x.Kind)?.Count ?? 0;
                    var projects = snapshot.Projects.GetValueOrDefault(x.Kind)?.Count ?? 0;
                    if (snapshot.Failures.TryGetValue(x.Kind, out var failure)) return string.Format(R("IntegrationsTab_RuntimeFailure"), RuntimeLabel(x.Kind), failure);
                    var states = PodmanStates(x.ServiceState);
                    var rows = new List<string> { string.Format(R("IntegrationsTab_RuntimeInventorySummary"), RuntimeLabel(x.Kind), containers, images, projects), string.Format(R("IntegrationsTab_RuntimeDetails"), SafeVersion(x.Version) ?? R("IntegrationsTab_ValueUnavailable"), StateLabel(states.Socket), StateLabel(states.Service), x.Endpoint ?? R("IntegrationsTab_ValueUnavailable")) };
                    rows.AddRange(snapshot.Containers.GetValueOrDefault(x.Kind)?.Take(10).Select(c => string.Format(R("IntegrationsTab_ContainerRow"), c.Name, c.Image, c.State)) ?? []);
                    rows.AddRange(snapshot.Images.GetValueOrDefault(x.Kind)?.Take(10).Select(i => string.Format(R("IntegrationsTab_ImageRow"), i.Repository, i.Tag)) ?? []);
                    rows.AddRange(snapshot.Projects.GetValueOrDefault(x.Kind)?.Take(10).Select(p => string.Format(R("IntegrationsTab_ComposeRow"), p.Name, p.Status, p.ServiceCount)) ?? []);
                    return string.Join(Environment.NewLine, rows);
                }));
            }
            else
            {
                PodmanPrerequisiteMessage = R("IntegrationsTab_PodmanUnavailableService");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
            _instance.IsBusy = false;
            OnPropertyChanged(nameof(IsToggleEnabled));
        }
    }

    [RelayCommand]
    private async Task ToggleDockerAsync()
    {
        if (!IsToggleEnabled) return;

        IsLoading = true;
        _instance.IsBusy = true;
        ShowRestartBanner = false;
        OnPropertyChanged(nameof(IsToggleEnabled));
        try
        {
            bool target = !IsDockerEnabled;
            var preview = await _powerShellModuleClient.GetDockerIntegrationPreviewAsync(_instance.Name, target);
            if (!await _dialogService.ShowConfirmAsync(R("IntegrationsTab_DockerIntegration"), FormatPreview(preview.Effects))) return;
            var result = await _powerShellModuleClient.SetDockerIntegrationAsync(_instance.Name, target, preview.Token);
            if (!result.Succeeded) { await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle, result.Guidance ?? result.OutcomeCode); return; }
            IsDockerEnabled = target; DockerStatus = target ? DockerIntegrationStatus.Enabled : DockerIntegrationStatus.Disabled; ShowRestartBanner = result.RestartRequired;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
            _instance.IsBusy = false;
            OnPropertyChanged(nameof(IsToggleEnabled));
        }
    }

    [RelayCommand]
    private void DismissRestartBanner() => ShowRestartBanner = false;

    [RelayCommand]
    private Task StartPodmanSocketAsync() => RunPodmanUnitAsync(PodmanUserUnit.Socket, SystemdAction.Start);
    [RelayCommand]
    private Task StopPodmanSocketAsync() => RunPodmanUnitAsync(PodmanUserUnit.Socket, SystemdAction.Stop);
    [RelayCommand]
    private Task StartPodmanServiceAsync() => RunPodmanUnitAsync(PodmanUserUnit.Service, SystemdAction.Start);
    [RelayCommand]
    private Task StopPodmanServiceAsync() => RunPodmanUnitAsync(PodmanUserUnit.Service, SystemdAction.Stop);

    private async Task RunPodmanUnitAsync(PodmanUserUnit unit, SystemdAction action)
    {
        var powerShellModuleClient = _powerShellModuleClient;
        if (!IsPodmanServiceControlsEnabled || powerShellModuleClient is null) return;
        IsLoading = true;
        try
        {
            var preview = await powerShellModuleClient.GetPodmanUserUnitPreviewAsync(_instance.Name, unit, action);
            if (!await _dialogService.ShowConfirmAsync(R("IntegrationsTab_ConfirmPodmanAction"), FormatPreview(preview.Effects))) return;
            var result = await powerShellModuleClient.InvokePodmanUserUnitAsync(preview);
            if (!result.Succeeded) await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle, result.Guidance ?? result.OutcomeCode);
            _initialized = false;
            await InitializeAsync();
        }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsToggleEnabled)); }
    }
    [RelayCommand]
    private async Task ConfigurePodmanConnectionAsync()
    {
        var powerShellModuleClient = _powerShellModuleClient;
        if (!IsPodmanConnectionEnabled || powerShellModuleClient is null) return;
        IsLoading = true;
        try
        {
            var preview = await powerShellModuleClient.GetPodmanConnectionPreviewAsync(_instance.Name, new PodmanConnectionRequest(PodmanConnectionName, new Uri(PodmanConnectionEndpoint, UriKind.Absolute)));
            if (!await _dialogService.ShowConfirmAsync(R("IntegrationsTab_ConfirmConnection"), FormatPreview(preview.Effects))) return;
            var result = await powerShellModuleClient.InvokePodmanConnectionAsync(preview);
            if (!result.Succeeded) await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle, result.Guidance ?? result.OutcomeCode);
        }
        catch (Exception ex) { await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex))); }
        finally { IsLoading = false; }
    }

    private static bool IsDockerSystemInstance(string name) =>
        name.Equals("docker-desktop", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("docker-desktop-data", StringComparison.OrdinalIgnoreCase);
    private static string R(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
    private static string RuntimeLabel(ContainerRuntimeKind kind) => R($"IntegrationsTab_Runtime_{kind}");
    private static string AvailabilityLabel(ContainerRuntimeAvailability availability) => R($"IntegrationsTab_Availability_{availability}");
    private static string HealthLabel(string health) => Properties.Resources.ResourceManager.GetString($"IntegrationsTab_Health_{health}") ?? health;
    private static string FormatPreview(IReadOnlyList<string>? effects) => string.Join(Environment.NewLine, effects ?? []);
    private static string? SafeVersion(string? value) => VersionSafety.Normalize(value);
    private static (string Socket, string Service) PodmanStates(string value)
    {
        const string unavailable = "unavailable";
        var parts = value.Split(';', StringSplitOptions.None);
        if (parts.Length != 2 || !parts[0].StartsWith("socket=", StringComparison.Ordinal) || !parts[1].StartsWith("service=", StringComparison.Ordinal)) return (unavailable, unavailable);
        var socket = parts[0]["socket=".Length..];
        var service = parts[1]["service=".Length..];
        return (SafeState(socket), SafeState(service));
    }

    private static string SafeState(string value) => value is "active" or "inactive" or "unknown" or "unavailable" ? value : "unavailable";
    private static string StateLabel(string state) => R($"IntegrationsTab_State_{state}");
    private string PodmanPrerequisiteExplanation()
    {
        if (!Instance.IsWslV2) return R("IntegrationsTab_PodmanUnavailableWsl2");
        if (!IsPodmanWslAvailable) return R("IntegrationsTab_PodmanUnavailableRuntime");
        if (!IsInstanceSystemdSupported) return R("IntegrationsTab_PodmanUnavailableSystemd");
        return string.Empty;
    }
}
