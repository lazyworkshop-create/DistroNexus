using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Disk tab of InstanceDetailDialog.
/// Handles VHDX compaction, disk size display and related operations.
/// </summary>
public partial class DiskTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IWslManagerService _wslManager;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public DiskTabViewModel(
        WslInstanceViewModel instance,
        IWslManagerService wslManager,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;
        // Disk tab initialization will be implemented in Phase 2
        return Task.CompletedTask;
    }
}
