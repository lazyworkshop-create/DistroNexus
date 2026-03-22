using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
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

    /// <summary>Raised when the dialog should be closed.</summary>
    public event EventHandler? CloseRequested;

    public InstanceDetailViewModel(
        WslInstanceViewModel instance,
        IWslManagerService wslManager,
        IDockerIntegrationService dockerIntegrationService,
        INetworkService networkService,
        IBackupService backupService,
        IWslConfigService wslConfigService,
        ITagService tagService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        DiskTab = new DiskTabViewModel(instance, wslManager, dialogService);
        ResourcesTab = new ResourcesTabViewModel(instance, wslManager, wslConfigService, dialogService);
        IntegrationsTab = new IntegrationsTabViewModel(instance, dockerIntegrationService, dialogService);
        NetworkTab = new NetworkTabViewModel(instance, networkService, dialogService);
        BackupTab = new BackupTabViewModel(instance, backupService, dialogService);
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
