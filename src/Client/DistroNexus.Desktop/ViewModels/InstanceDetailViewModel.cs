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
public partial class InstanceDetailViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IDialogService _dialogService;
    private readonly SemaphoreSlim _tabLifecycle = new(1, 1);

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
    public MonitorTabViewModel MonitorTab { get; }

    /// <summary>Raised when the dialog should be closed.</summary>
    public event EventHandler? CloseRequested;

    public InstanceDetailViewModel(
        WslInstanceViewModel instance,
        IWslManagerService wslManager,
        INetworkService networkService,
        IBackupService backupService,
        IRecoveryPointService recoveryPointService,
        IWslConfigService wslConfigService,
        IDialogService dialogService,
        IDistributionConfigurationService distributionConfigurationService,
        ISystemdService systemdService,
        INetworkDiagnosticsService networkDiagnostics,
        IFirewallOperationBroker firewallOperationBroker,
        INetworkConfigurationService networkConfigurationService,
        INetworkStatusAdapter networkStatusAdapter,
        IBrowserLauncher browserLauncher,
        IPowerShellModuleClient? powerShellModuleClient = null)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        DiskTab = new DiskTabViewModel(instance, wslManager, dialogService);
        ResourcesTab = new ResourcesTabViewModel(instance, wslManager, wslConfigService, dialogService);
        IntegrationsTab = new IntegrationsTabViewModel(instance, dialogService, powerShellModuleClient ?? throw new ArgumentNullException(nameof(powerShellModuleClient)));
        NetworkTab = new NetworkTabViewModel(instance, networkService, dialogService, networkDiagnostics, firewallOperationBroker, networkConfigurationService, networkStatusAdapter, browserLauncher);
        BackupTab = new BackupTabViewModel(instance, backupService, dialogService, recoveryPointService);
        ConfigurationTab = new ConfigurationTabViewModel(instance, distributionConfigurationService, powerShellModuleClient ?? throw new ArgumentNullException(nameof(powerShellModuleClient)), dialogService);
        ServicesTab = new ServicesTabViewModel(instance, systemdService, dialogService);
        MonitorTab = new MonitorTabViewModel(instance, powerShellModuleClient ?? throw new ArgumentNullException(nameof(powerShellModuleClient)), dialogService);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        _ = InitializeActiveTabAsync(value);
    }

    private async Task InitializeActiveTabAsync(int tabIndex)
    {
        try
        {
            await _tabLifecycle.WaitAsync();
            try
            {
            // Tab selection changes are asynchronous. Do not activate a monitor that the user
            // has already navigated away from while another tab was initializing.
            if (tabIndex != SelectedTabIndex) return;
            if (tabIndex != 7) await MonitorTab.StopAsync();
            switch (tabIndex)
            {
                case 0: await DiskTab.InitializeAsync(); break;
                case 1: await ResourcesTab.InitializeAsync(); break;
                case 2: await IntegrationsTab.InitializeAsync(); break;
                case 3: await NetworkTab.InitializeAsync(); break;
                case 4: await BackupTab.InitializeAsync(); break;
                case 5: await ConfigurationTab.InitializeAsync(); break;
                case 6: await ServicesTab.InitializeAsync(); break;
                case 7: await MonitorTab.ActivateAsync(); break;
            }
            if (tabIndex == 7 && tabIndex != SelectedTabIndex) await MonitorTab.StopAsync();
            }
            finally { _tabLifecycle.Release(); }
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
    public async ValueTask DisposeAsync() { await MonitorTab.DisposeAsync(); _tabLifecycle.Dispose(); }
}
