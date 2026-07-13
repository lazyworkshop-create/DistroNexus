using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels.Tabs;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// ViewModel for the InstanceDetailDialog. Owns 5 tab ViewModels and coordinates
/// lazy initialization when the user switches tabs.
/// </summary>
public partial class InstanceDetailViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private WslInstanceViewModel _instance;

    public DiskTabViewModel DiskTab { get; }
    public ResourcesTabViewModel ResourcesTab { get; }
    public IntegrationsTabViewModel IntegrationsTab { get; }
    public NetworkTabViewModel NetworkTab { get; }
    public BackupTabViewModel BackupTab { get; }
    public ConfigurationTabViewModel ConfigurationTab { get; }
    public ServicesTabViewModel ServicesTab { get; }

    /// <summary>Raised when the dialog should be closed.</summary>
    public event EventHandler? CloseRequested;

    public InstanceDetailViewModel(
        WslInstanceViewModel instance,
        IWslManagerService wslManager,
        IDockerIntegrationService dockerIntegrationService,
        INetworkService networkService,
        IBackupService backupService,
        IRecoveryPointService recoveryPointService,
        IWslConfigService wslConfigService,
        ITagService tagService,
        IDialogService dialogService,
        IDistributionConfigurationService distributionConfigurationService,
        IPlatformCapabilityService platformCapabilityService,
        ISystemdService systemdService,
        INetworkDiagnosticsService networkDiagnostics,
        IFirewallOperationBroker firewallOperationBroker,
        INetworkConfigurationService networkConfigurationService,
        INetworkStatusAdapter networkStatusAdapter,
        IBrowserLauncher browserLauncher)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        DiskTab = new DiskTabViewModel(instance, wslManager, dialogService);
        ResourcesTab = new ResourcesTabViewModel(instance, wslManager, wslConfigService, dialogService);
        IntegrationsTab = new IntegrationsTabViewModel(instance, dockerIntegrationService, dialogService);
        NetworkTab = new NetworkTabViewModel(instance, networkService, dialogService, networkDiagnostics, firewallOperationBroker, networkConfigurationService, networkStatusAdapter, browserLauncher);
        BackupTab = new BackupTabViewModel(instance, backupService, dialogService, recoveryPointService);
        ConfigurationTab = new ConfigurationTabViewModel(instance, distributionConfigurationService, platformCapabilityService, dialogService);
        ServicesTab = new ServicesTabViewModel(instance, systemdService, dialogService);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        _ = InitializeActiveTabAsync(value);
    }

    private async Task InitializeActiveTabAsync(int tabIndex)
    {
        try
        {
            switch (tabIndex)
            {
                case 0: await DiskTab.InitializeAsync(); break;
                case 1: await ResourcesTab.InitializeAsync(); break;
                case 2: await IntegrationsTab.InitializeAsync(); break;
                case 3: await NetworkTab.InitializeAsync(); break;
                case 4: await BackupTab.InitializeAsync(); break;
                case 5: await ConfigurationTab.InitializeAsync(); break;
                case 6: await ServicesTab.InitializeAsync(); break;
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
