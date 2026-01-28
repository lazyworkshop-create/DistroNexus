using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly INavigationService _navigationService;
    private readonly ITerminalService _terminalService;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<WslInstanceViewModel> _instances = new();

    [ObservableProperty]
    private WslInstanceViewModel? _selectedInstance;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isOnDashboard = true;

    [ObservableProperty]
    private string _currentTheme = "Dark";

    [ObservableProperty]
    private string _currentLanguage = "en-US";

    [ObservableProperty]
    private bool _isDownloadPanelVisible;

    [ObservableProperty]
    private int _activeDownloadsCount;

    /// <summary>
    /// Gets the collection of download tasks for data binding.
    /// </summary>
    public ObservableCollection<DownloadTask> DownloadTasks => _downloadTaskManager.Tasks;

    public MainViewModel(
        IWslManagerService wslManager,
        ISettingsService settingsService,
        INavigationService navigationService,
        ITerminalService terminalService,
        IDownloadTaskManager downloadTaskManager,
        IServiceProvider serviceProvider,
        ILogger<MainViewModel> logger)
    {
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
        _downloadTaskManager = downloadTaskManager ?? throw new ArgumentNullException(nameof(downloadTaskManager));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to download task status changes
        _downloadTaskManager.TaskStatusChanged += OnDownloadTaskStatusChanged;

        // Load saved theme and language settings
        _ = LoadUserPreferencesAsync();

        // Start auto-refresh timer
        StartAutoRefresh();

        // Update active downloads count initially
        UpdateActiveDownloadsCount();
    }

    private System.Timers.Timer? _refreshTimer;

    /// <summary>
    /// Loads user preferences from settings.
    /// </summary>
    private async Task LoadUserPreferencesAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            CurrentTheme = settings.Theme ?? "Dark";
            CurrentLanguage = settings.Language ?? "en-US";

            _logger.LogInformation("Loaded user preferences: Theme={Theme}, Language={Language}", 
                CurrentTheme, CurrentLanguage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load user preferences, using defaults");
        }
    }

    private void StartAutoRefresh()
    {
        _refreshTimer = new System.Timers.Timer(10000); // Refresh every 10 seconds
        _refreshTimer.Elapsed += async (s, e) =>
        {
            if (IsOnDashboard && !IsLoading)
            {
                await RefreshInstanceStatesAsync();
            }
        };
        _refreshTimer.Start();
    }

    /// <summary>
    /// Refreshes only the instance states without full reload.
    /// </summary>
    private async Task RefreshInstanceStatesAsync()
    {
        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var currentInstance in Instances)
                {
                    var updated = instances.FirstOrDefault(i => i.Name == currentInstance.Name);
                    if (updated != null && updated.State != currentInstance.State)
                    {
                        currentInstance.UpdateState(updated.State);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh instance states");
        }
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
                Instances.Add(new WslInstanceViewModel(instance, _wslManager, _terminalService, _logger));
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
    private void ShowDashboard()
    {
        StatusMessage = "Dashboard";
        CurrentPage = null;
        IsOnDashboard = true;
        _logger.LogInformation("Navigated to dashboard");
    }

    [RelayCommand]
    private void ShowSettings()
    {
        StatusMessage = "Settings";
        var settingsPage = _serviceProvider.GetRequiredService<SettingsPage>();
        CurrentPage = settingsPage;
        IsOnDashboard = false;
        _logger.LogInformation("Navigated to settings");
    }

    /// <summary>
    /// Toggles the download panel visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleDownloadPanel()
    {
        IsDownloadPanelVisible = !IsDownloadPanelVisible;
        _logger.LogInformation("Download panel visibility: {IsVisible}", IsDownloadPanelVisible);
    }

    /// <summary>
    /// Clears all completed download tasks.
    /// </summary>
    [RelayCommand]
    private void ClearCompletedDownloads()
    {
        _downloadTaskManager.ClearCompletedTasks();
        UpdateActiveDownloadsCount();
        _logger.LogInformation("Cleared completed downloads");
    }

    /// <summary>
    /// Cancels a specific download task.
    /// </summary>
    [RelayCommand]
    private async Task CancelDownloadAsync(Guid? taskId)
    {
        if (taskId.HasValue)
        {
            await _downloadTaskManager.CancelTaskAsync(taskId.Value);
            UpdateActiveDownloadsCount();
        }
    }

    /// <summary>
    /// Retries a failed download task.
    /// </summary>
    [RelayCommand]
    private async Task RetryDownloadAsync(Guid? taskId)
    {
        if (taskId.HasValue)
        {
            await _downloadTaskManager.RetryTaskAsync(taskId.Value);
            UpdateActiveDownloadsCount();
        }
    }

    /// <summary>
    /// Handles download task status changes.
    /// </summary>
    private void OnDownloadTaskStatusChanged(object? sender, DownloadTask task)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            UpdateActiveDownloadsCount();
            
            // Optionally show notification for completed downloads
            if (task.Status == DownloadStatus.Completed)
            {
                _logger.LogInformation("Download completed: {PackageName}", task.PackageName);
            }
        });
    }

    /// <summary>
    /// Updates the active downloads count.
    /// </summary>
    private void UpdateActiveDownloadsCount()
    {
        ActiveDownloadsCount = _downloadTaskManager.GetActiveTasksCount();
    }

    [RelayCommand]
    private void ShowInstallWizard()
    {
        StatusMessage = "Opening installation wizard...";
        _logger.LogInformation("Opening install wizard");

        // Use the new workflow-based wizard dialog
        var dialog = _serviceProvider.GetRequiredService<InstallWizardDialogNew>();
        dialog.Owner = Application.Current.MainWindow;
        
        var result = dialog.ShowDialog();
        
        if (result == true)
        {
            StatusMessage = "Installation completed - refreshing instances...";
            _ = LoadInstancesAsync();
        }
        else
        {
            StatusMessage = "Ready";
        }
    }

    [RelayCommand]
    private void ShowPackageManager()
    {
        StatusMessage = "Package Manager";
        var packagePage = _serviceProvider.GetRequiredService<PackageManagerPage>();
        CurrentPage = packagePage;
        IsOnDashboard = false;
        _logger.LogInformation("Navigated to package manager");
    }

    [RelayCommand]
    private void GoBack()
    {
        ShowDashboard();
    }

    /// <summary>
    /// Toggles between light and dark theme.
    /// </summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        try
        {
            _logger.LogInformation("Toggling theme from {CurrentTheme}", CurrentTheme);

            // Toggle between Dark and Light
            CurrentTheme = CurrentTheme == "Dark" ? "Light" : "Dark";

            // Use App's ApplyThemeFromSettings for consistent theme switching
            var app = (App)Application.Current;
            app.ApplyThemeFromSettings(CurrentTheme);

            // Save theme preference
            var settings = _settingsService.LoadSettingsAsync().Result;
            settings.Theme = CurrentTheme;
            _ = _settingsService.SaveSettingsAsync(settings);

            StatusMessage = $"Theme changed to {CurrentTheme}";
            _logger.LogInformation("Theme changed to {Theme}", CurrentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle theme");
            StatusMessage = "Failed to change theme";
        }
    }

    /// <summary>
    /// Toggles between English and Chinese language.
    /// </summary>
    [RelayCommand]
    private void ToggleLanguage()
    {
        try
        {
            _logger.LogInformation("Toggling language from {CurrentLanguage}", CurrentLanguage);

            // Toggle between en-US and zh-CN
            CurrentLanguage = CurrentLanguage == "en-US" ? "zh-CN" : "en-US";

            // Save language preference
            var settings = _settingsService.LoadSettingsAsync().Result;
            settings.Language = CurrentLanguage;
            _ = _settingsService.SaveSettingsAsync(settings);

            StatusMessage = CurrentLanguage == "en-US" 
                ? "Language changed to English" 
                : "语言已切换为中文";

            _logger.LogInformation("Language changed to {Language}", CurrentLanguage);

            MessageBox.Show(
                CurrentLanguage == "en-US"
                    ? "Language changed to English. Please restart the application for the change to take full effect."
                    : "语言已切换为中文。请重启应用程序以使更改完全生效。",
                CurrentLanguage == "en-US" ? "Language Changed" : "语言已更改",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle language");
            StatusMessage = "Failed to change language";
        }
    }
}

/// <summary>
/// View model for a single WSL instance.
/// </summary>
public partial class WslInstanceViewModel : ObservableObject
{
    private readonly IWslManagerService _wslManager;
    private readonly ITerminalService _terminalService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private WslInstance _instance;

    public string Name => Instance.Name;
    public string State => Instance.State;
    public bool IsRunning => Instance.IsRunning;
    public string InstallPath => Instance.InstallPath;
    public string Distribution => Instance.Distribution;
    public long DiskSize => Instance.Size;
    
    /// <summary>
    /// Gets the disk size formatted for display.
    /// </summary>
    public string DiskSizeDisplay => FormatFileSize(DiskSize);

    public WslInstanceViewModel(
        WslInstance instance, 
        IWslManagerService wslManager,
        ITerminalService terminalService,
        ILogger logger)
    {
        _instance = instance;
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "Unknown";
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Updates the instance state and notifies property changes.
    /// </summary>
    /// <param name="newState">The new state value.</param>
    public void UpdateState(string newState)
    {
        Instance.State = newState;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRunning));
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

    [RelayCommand]
    private async Task OpenTerminalAsync()
    {
        try
        {
            _logger.LogInformation("Opening terminal for instance {Name}", Name);
            
            var success = await _terminalService.OpenTerminalAsync(Name);
            
            if (!success)
            {
                MessageBox.Show($"Failed to open terminal for instance '{Name}'", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open terminal for instance {Name}", Name);
            MessageBox.Show($"Failed to open terminal: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task MoveAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"Select new location for '{Name}'"
        };

        if (dialog.ShowDialog() != true)
            return;

        var newPath = dialog.FolderName;

        var confirm = MessageBox.Show(
            $"Move instance '{Name}' to:\n{newPath}\n\nThis may take a while depending on the instance size.",
            "Confirm Move",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Moving instance {Name} to {NewPath}", Name, newPath);
            
            await _wslManager.MoveInstanceAsync(Name, newPath);
            
            Instance.InstallPath = newPath;
            OnPropertyChanged(nameof(InstallPath));
            
            MessageBox.Show($"Instance '{Name}' moved successfully", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move instance {Name}", Name);
            MessageBox.Show($"Failed to move instance: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        var inputDialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Rename Instance",
            Content = new System.Windows.Controls.TextBox
            {
                Text = Name,
                MinWidth = 200
            },
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel"
        };

        var result = await inputDialog.ShowDialogAsync();
        
        if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        var textBox = inputDialog.Content as System.Windows.Controls.TextBox;
        var newName = textBox?.Text?.Trim();

        if (string.IsNullOrEmpty(newName) || newName == Name)
            return;

        try
        {
            _logger.LogInformation("Renaming instance {OldName} to {NewName}", Name, newName);
            
            await _wslManager.RenameInstanceAsync(Name, newName);
            
            Instance.Name = newName;
            OnPropertyChanged(nameof(Name));
            
            MessageBox.Show($"Instance renamed to '{newName}' successfully", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename instance {Name}", Name);
            MessageBox.Show($"Failed to rename instance: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SetCredentialsAsync()
    {
        // For now, show a simple dialog - in a full implementation, use a proper dialog
        var username = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter username:",
            "Set Credentials",
            "root");

        if (string.IsNullOrEmpty(username))
            return;

        var password = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter password:",
            "Set Credentials",
            "");

        try
        {
            _logger.LogInformation("Setting credentials for instance {Name}", Name);
            
            await _wslManager.SetCredentialsAsync(Name, username, password);
            
            MessageBox.Show($"Credentials set successfully for '{Name}'", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set credentials for instance {Name}", Name);
            MessageBox.Show($"Failed to set credentials: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
