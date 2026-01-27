using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// Main view model for the application shell.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IWslManagerService _wslManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<WslInstanceViewModel> _instances = new();

    [ObservableProperty]
    private WslInstanceViewModel? _selectedInstance;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainViewModel(
        IWslManagerService wslManager,
        ISettingsService settingsService,
        ILogger<MainViewModel> logger)
    {
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task LoadInstancesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading WSL instances...";

            _logger.LogInformation("Loading WSL instances");

            var instances = await _wslManager.GetInstancesAsync();
            
            Instances.Clear();
            foreach (var instance in instances)
            {
                Instances.Add(new WslInstanceViewModel(instance, _wslManager, _logger));
            }

            StatusMessage = $"Loaded {Instances.Count} instance(s)";
            _logger.LogInformation("Loaded {Count} WSL instances", Instances.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load WSL instances");
            StatusMessage = $"Error loading instances: {ex.Message}";
            MessageBox.Show($"Failed to load WSL instances: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInstancesAsync();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        StatusMessage = "Opening settings...";
        // TODO: Navigate to settings page
    }

    [RelayCommand]
    private void ShowInstallWizard()
    {
        StatusMessage = "Opening installation wizard...";
        // TODO: Show install wizard dialog
    }

    [RelayCommand]
    private void ShowPackageManager()
    {
        StatusMessage = "Opening package manager...";
        // TODO: Navigate to package manager page
    }
}

/// <summary>
/// View model for a single WSL instance.
/// </summary>
public partial class WslInstanceViewModel : ObservableObject
{
    private readonly IWslManagerService _wslManager;
    private readonly ILogger _logger;

    [ObservableProperty]
    private WslInstance _instance;

    public string Name => Instance.Name;
    public string State => Instance.State;
    public bool IsRunning => Instance.IsRunning;
    public string InstallPath => Instance.InstallPath;
    public string Distribution => Instance.Distribution;

    public WslInstanceViewModel(
        WslInstance instance, 
        IWslManagerService wslManager, 
        ILogger logger)
    {
        _instance = instance;
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        try
        {
            _logger.LogInformation("Starting instance {Name}", Name);
            
            var success = await _wslManager.StartInstanceAsync(Name);
            
            if (success)
            {
                Instance.State = "Running";
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsRunning));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start instance {Name}", Name);
            MessageBox.Show($"Failed to start instance: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try
        {
            _logger.LogInformation("Stopping instance {Name}", Name);
            
            var success = await _wslManager.StopInstanceAsync(Name);
            
            if (success)
            {
                Instance.State = "Stopped";
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsRunning));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop instance {Name}", Name);
            MessageBox.Show($"Failed to stop instance: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        var result = MessageBox.Show(
            $"Are you sure you want to remove instance '{Name}'? This action cannot be undone.",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Removing instance {Name}", Name);
            
            await _wslManager.RemoveInstanceAsync(Name);
            
            MessageBox.Show($"Instance '{Name}' removed successfully", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove instance {Name}", Name);
            MessageBox.Show($"Failed to remove instance: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
