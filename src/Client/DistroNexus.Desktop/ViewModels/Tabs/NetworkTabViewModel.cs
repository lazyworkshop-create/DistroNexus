using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Network tab of InstanceDetailDialog.
/// Displays IP address, port mappings and proxy information.
/// </summary>
public partial class NetworkTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly INetworkService _networkService;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public NetworkTabViewModel(
        WslInstanceViewModel instance,
        INetworkService networkService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;
        // Network tab initialization will be implemented in Phase 4
        return Task.CompletedTask;
    }
}
