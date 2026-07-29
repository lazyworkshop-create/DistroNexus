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
    private readonly IPowerShellModuleClient _moduleClient;
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
        IPowerShellModuleClient moduleClient,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
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
            var snapshot = await _moduleClient.GetInstanceResourcesAsync(_instance.Name);
            SparseMode = snapshot.SparseMode;
            SparseModeIndeterminate = false;
            SparseModeStatus = snapshot.SparseMode ? Properties.Resources.ResourcesTab_SparseModeEnabled : Properties.Resources.ResourcesTab_SparseModeDisabled;
            MemoryDisplay = CpuDisplay = SwapDisplay = "—";
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
            var preview = await _moduleClient.GetInstanceSparsePreviewAsync(_instance.Name, newValue);
            if (!await _dialogService.ShowConfirmAsync(Properties.Resources.ResourcesTab_SparseMode, string.Join(Environment.NewLine, preview.Effects))) return;
            var result = await _moduleClient.SetInstanceSparseModeAsync(preview.PreviewToken);
            if (!result.Succeeded) throw new InvalidOperationException(result.OutcomeCode);
            var snapshot = await _moduleClient.GetInstanceResourcesAsync(_instance.Name);
            SparseMode = snapshot.SparseMode;
            SparseModeIndeterminate = false;
            SparseModeStatus = snapshot.SparseMode
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
