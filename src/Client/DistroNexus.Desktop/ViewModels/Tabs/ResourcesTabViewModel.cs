using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Resources tab of InstanceDetailDialog.
/// Handles global WSL resource settings and sparse mode configuration.
/// </summary>
public partial class ResourcesTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IWslManagerService _wslManager;
    private readonly IWslConfigService _wslConfigService;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public ResourcesTabViewModel(
        WslInstanceViewModel instance,
        IWslManagerService wslManager,
        IWslConfigService wslConfigService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _wslConfigService = wslConfigService ?? throw new ArgumentNullException(nameof(wslConfigService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;
        // Resources tab initialization will be implemented in Phase 2
        return Task.CompletedTask;
    }
}
