using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for a single port mapping row in the Network tab grid.
/// </summary>
public class PortMappingViewModel
{
    public string Protocol       { get; init; } = string.Empty;
    public string LocalAddress   { get; init; } = string.Empty;
    public int    Port           { get; init; }
    public string ProcessName    { get; init; } = string.Empty;
    public bool   HasWindowsProxy { get; init; }
    public string CopyText => $"{LocalAddress}:{Port}";
}

/// <summary>
/// ViewModel for the Network tab of InstanceDetailDialog.
/// Displays WSL IP address and port mappings (C-02).
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
    private string _instanceIp = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PortMappingViewModel> _portMappings = [];

    [ObservableProperty]
    private bool _showStoppedPlaceholder;

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

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await RefreshNetworkAsync();
    }

    [RelayCommand]
    private async Task RefreshNetworkAsync()
    {
        if (!Instance.IsRunning)
        {
            ShowStoppedPlaceholder = true;
            return;
        }

        ShowStoppedPlaceholder = false;
        IsLoading = true;
        try
        {
            var ip = await _networkService.GetInstanceIpAddressAsync(_instance.Name);
            InstanceIp = ip ?? string.Empty;

            var mappings = await _networkService.GetPortMappingsAsync(_instance.Name);
            PortMappings = new ObservableCollection<PortMappingViewModel>(
                mappings.Select(m => new PortMappingViewModel
                {
                    Protocol       = m.Protocol,
                    LocalAddress   = m.LocalAddress,
                    Port           = m.Port,
                    ProcessName    = m.ProcessName,
                    HasWindowsProxy = m.HasWindowsProxy
                }));
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
        }
    }

    [RelayCommand]
    private void CopyAddress(PortMappingViewModel? row)
    {
        if (row is null) return;
        try { Clipboard.SetText(row.CopyText); }
        catch { /* ignore clipboard failures */ }
    }
}
