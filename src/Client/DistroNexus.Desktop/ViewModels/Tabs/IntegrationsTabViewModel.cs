using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Integrations tab of InstanceDetailDialog.
/// Handles Docker Desktop integration status and toggle (C-01).
/// </summary>
public partial class IntegrationsTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IDockerIntegrationService _dockerIntegrationService;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    [ObservableProperty]
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

    // WSL v1 guard message
    public bool ShowWslV1Message => !Instance.IsWslV2;

    public IntegrationsTabViewModel(
        WslInstanceViewModel instance,
        IDockerIntegrationService dockerIntegrationService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _dockerIntegrationService = dockerIntegrationService ?? throw new ArgumentNullException(nameof(dockerIntegrationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (!IsTabVisible) return;

        IsLoading = true;
        try
        {
            IsDockerInstalled = await _dockerIntegrationService.IsDockerDesktopInstalledAsync();

            if (IsDockerInstalled && Instance.IsWslV2)
            {
                DockerStatus = await _dockerIntegrationService.GetIntegrationStatusAsync(_instance.Name);
                IsDockerEnabled = DockerStatus == DockerIntegrationStatus.Enabled;
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsToggleEnabled));
        }
    }

    [RelayCommand]
    private async Task ToggleDockerAsync()
    {
        IsLoading = true;
        ShowRestartBanner = false;
        OnPropertyChanged(nameof(IsToggleEnabled));
        try
        {
            bool target = !IsDockerEnabled;
            await _dockerIntegrationService.SetIntegrationAsync(_instance.Name, target);
            IsDockerEnabled = target;
            DockerStatus = target ? DockerIntegrationStatus.Enabled : DockerIntegrationStatus.Disabled;
            ShowRestartBanner = true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsToggleEnabled));
        }
    }

    [RelayCommand]
    private void DismissRestartBanner() => ShowRestartBanner = false;

    private static bool IsDockerSystemInstance(string name) =>
        name.Equals("docker-desktop", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("docker-desktop-data", StringComparison.OrdinalIgnoreCase);
}
