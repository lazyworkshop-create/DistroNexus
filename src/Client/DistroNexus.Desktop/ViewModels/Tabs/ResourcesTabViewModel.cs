using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

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
    private bool? _sparseMode;  // null = indeterminate/unknown

    [ObservableProperty]
    private bool _sparseModeIndeterminate;

    [ObservableProperty]
    private string _sparseModeStatus = string.Empty;

    [ObservableProperty]
    private string _memoryDisplay = string.Empty;

    [ObservableProperty]
    private string _cpuDisplay = string.Empty;

    [ObservableProperty]
    private string _swapDisplay = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public bool IsWslV1 => !_instance.IsWslV2;

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

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (!_instance.IsWslV2) return;

        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            // Read sparse mode from instance config
            var configObj = await _wslManager.GetInstanceConfigAsync(_instance.Name);
            if (configObj is bool boolVal)
            {
                SparseMode = boolVal;
                SparseModeIndeterminate = false;
                SparseModeStatus = boolVal
                    ? Properties.Resources.ResourcesTab_SparseModeEnabled
                    : Properties.Resources.ResourcesTab_SparseModeDisabled;
            }
            else if (configObj is IDictionary<string, object?> dict
                     && dict.TryGetValue("sparse", out var sparseVal)
                     && sparseVal is bool b)
            {
                SparseMode = b;
                SparseModeIndeterminate = false;
                SparseModeStatus = b
                    ? Properties.Resources.ResourcesTab_SparseModeEnabled
                    : Properties.Resources.ResourcesTab_SparseModeDisabled;
            }
            else
            {
                SparseMode = null;
                SparseModeIndeterminate = true;
                SparseModeStatus = Properties.Resources.ResourcesTab_SparseModeUnknown;
            }

            // Read global WSL config
            var wslConfig = await _wslConfigService.GetWslConfigAsync();
            MemoryDisplay = wslConfig.Memory ?? "—";
            CpuDisplay = wslConfig.Processors.HasValue ? wslConfig.Processors.Value.ToString() : "—";
            SwapDisplay = wslConfig.Swap ?? "—";
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
        }
    }

    [RelayCommand]
    private async Task ToggleSparseModeAsync()
    {
        if (SparseMode is null) return;

        bool newValue = !SparseMode.Value;
        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            await _wslManager.SetSparseModeAsync(_instance.Name, newValue);
            SparseMode = newValue;
            SparseModeIndeterminate = false;
            SparseModeStatus = newValue
                ? Properties.Resources.ResourcesTab_SparseModeEnabled
                : Properties.Resources.ResourcesTab_SparseModeDisabled;
        }
        catch (WslOperationException ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
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
        }
    }
}
