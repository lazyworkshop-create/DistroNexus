using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using WPFLocalizeExtension.Engine;
using System.Globalization;
using System.Threading;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// Main view model for the application shell.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IWslManagerService _wslManager;
    private readonly ISettingsService _settingsService;
    private readonly INavigationService _navigationService;
    private readonly ITerminalService _terminalService;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IWslEventWatcher _wslEventWatcher;
    private readonly ITagService _tagService;
    private readonly IBackupService _backupService;
    private readonly IDockerIntegrationService _dockerIntegrationService;

    [ObservableProperty]
    private ObservableCollection<WslInstanceViewModel> _instances = new();

    [ObservableProperty]
    private WslInstanceViewModel? _selectedInstance;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = Properties.Resources.StatusInitializing;

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
    [NotifyPropertyChangedFor(nameof(ActiveDownloadsDisplayText))]
    private int _activeDownloadsCount;

    public string ActiveDownloadsDisplayText => 
        string.Format(Properties.Resources.ActiveDownloadsFormat, ActiveDownloadsCount);

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
        ILogger<MainViewModel> logger,
        IWslEventWatcher wslEventWatcher,
        ITagService tagService,
        IBackupService backupService,
        IDockerIntegrationService dockerIntegrationService)
    {
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
        _downloadTaskManager = downloadTaskManager ?? throw new ArgumentNullException(nameof(downloadTaskManager));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wslEventWatcher = wslEventWatcher ?? throw new ArgumentNullException(nameof(wslEventWatcher));
        _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _dockerIntegrationService = dockerIntegrationService ?? throw new ArgumentNullException(nameof(dockerIntegrationService));

        // Subscribe to download task status changes
        _downloadTaskManager.TaskStatusChanged += OnDownloadTaskStatusChanged;

        // Subscribe to cache invalidation for auto-refresh (E-07-3)
        _wslEventWatcher.CacheInvalidationRequested += OnCacheInvalidated;

        // NOTE: LoadUserPreferencesAsync is now called explicitly from MainWindow.OnLoaded
        // to avoid async operations in constructor which can block DI resolution

        // Update active downloads count initially
        UpdateActiveDownloadsCount();
    }

    /// <summary>
    /// Initializes the ViewModel asynchronously. Must be called after construction.
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadUserPreferencesAsync();
        await ShowPendingBackupNotificationsAsync();
    }

    /// <summary>
    /// Reads and displays any backup failure notifications persisted by backup-runner.ps1 (E-04-1).
    /// Deletes the notification file after displaying to prevent repeat display.
    /// </summary>
    private async Task ShowPendingBackupNotificationsAsync()
    {
        var notifPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DistroNexus", "pending-notifications.json");
        if (!File.Exists(notifPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(notifPath);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("notifications", out var notifs))
            {
                // corrupt or unexpected file format — still deleted by finally
                return;
            }
            foreach (var n in notifs.EnumerateArray())
            {
                var msg = n.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "Unknown error";
                var inst = n.TryGetProperty("instance", out var instEl) ? instEl.GetString() : "Unknown instance";
                await ShowAlert("Backup Failure", $"Backup failed for '{inst}': {msg}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read or display pending backup notifications");
        }
        finally
        {
            try { File.Delete(notifPath); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete pending-notifications.json after display");
            }
        }
    }

    /// <summary>
    /// Loads user preferences from settings.
    /// </summary>
    private Task LoadUserPreferencesAsync()
    {
        try
        {
            var settings = _settingsService.LoadSettings();
            CurrentTheme = settings.Theme ?? "Dark";
            CurrentLanguage = settings.Language ?? "en-US";

            _logger.LogInformation("Loaded user preferences: Theme={Theme}, Language={Language}", 
                CurrentTheme, CurrentLanguage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load user preferences, using defaults");
        }

        return Task.CompletedTask;
    }

    private async Task ShowAlert(string title, string message)
    {
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = Properties.Resources.ButtonClose ?? "Close"
        };
        await uiMessageBox.ShowDialogAsync();
    }

    public static string FormatAlertMessage(Exception ex)
    {
        var code = ex is DistroNexus.Core.Exceptions.WslException wslEx
            ? $"[DN-{(int)wslEx.Code:D4}] "
            : ex is DistroNexus.Core.Exceptions.WslOperationException opEx
                ? $"[DN-{(int)opEx.Code:D4}] "
                : string.Empty;
        return $"{code}{ex.Message}";
    }

    [RelayCommand]
    private async Task LoadInstancesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading WSL instances");

            // Add timeout to prevent hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var instances = await _wslManager.GetInstancesAsync(combinedCts.Token);
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Instances.Clear();
                foreach (var instance in instances)
                {
                    var vm = new WslInstanceViewModel(instance, _wslManager, _terminalService, _settingsService, _logger, _tagService, _backupService);
                    vm.RefreshRequested += (s, e) => _ = RefreshAsync();
                    Instances.Add(vm);
                }

                // Check for default distro setting and select it if no selection exists
                if (SelectedInstance == null)
                {
                    var settings = _settingsService.LoadSettings();
                    if (!string.IsNullOrEmpty(settings.DefaultDistributionId))
                    {
                        var defaultInstance = Instances.FirstOrDefault(i => i.Name == settings.DefaultDistributionId);
                        if (defaultInstance != null)
                        {
                            SelectedInstance = defaultInstance;
                        }
                    }
                }
            });

            _logger.LogInformation("Loaded {Count} WSL instances", Instances.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Loading WSL instances canceled or timed out");
            // Don't show error dialog for timeout to avoid annoying the user
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load WSL instances");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.LoadInstancesError, MainViewModel.FormatAlertMessage(ex)));
        }
        // Note: Don't set IsLoading = false here as it's controlled by MainWindow
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInstancesAsync();
        
        // Load disk size only for running instances after refresh
        try
        {
            var runningInstances = Instances.Where(i => i.IsRunning).ToList();
            if (runningInstances.Any())
            {
                _logger.LogInformation("Loading disk size for {Count} running instance(s)", runningInstances.Count);
                
                // Use ForceRefreshInstanceAsync for each running instance
                // This will calculate disk size and update configuration
                foreach (var instance in runningInstances)
                {
                    try
                    {
                        var refreshedInstance = await _wslManager.ForceRefreshInstanceAsync(instance.Name);
                        if (refreshedInstance != null)
                        {
                            instance.UpdateDiskSize(refreshedInstance.Size);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to refresh instance {Name}", instance.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load disk sizes for running instances");
        }
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        StatusMessage = Properties.Resources.DashboardTitle;
        CurrentPage = null;
        IsOnDashboard = true;
        _logger.LogInformation("Navigated to dashboard");
    }

    [RelayCommand]
    private void ShowSettings()
    {
        StatusMessage = Properties.Resources.SettingsTitle;
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
    private void OnCacheInvalidated(object? sender, EventArgs e)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(
            async () => await LoadInstancesAsync(CancellationToken.None));
    }

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
    private void ShowInstallWizard(InstallWizardStartupRequest? startupRequest)
    {
        StatusMessage = Properties.Resources.StatusOpeningWizard;
        _logger.LogInformation("Opening install wizard");

        // Use the new workflow-based wizard dialog
        var dialog = _serviceProvider.GetRequiredService<InstallWizardDialogNew>();
        dialog.Owner = Application.Current.MainWindow;

        if (dialog.DataContext is InstallWizardWorkflowViewModel wizardVm)
        {
            wizardVm.SetStartupRequest(startupRequest);
        }
        
        var result = dialog.ShowDialog();
        
        if (result == true)
        {
            StatusMessage = Properties.Resources.StatusInstallCompleteRefresh;
            _ = LoadInstancesAsync();
        }
        else
        {
            StatusMessage = Properties.Resources.StatusReady;
        }
    }

    [RelayCommand]
    private void ShowTemplates()
    {
        StatusMessage = "Templates";
        var page = _serviceProvider.GetRequiredService<TemplatesPage>();
        CurrentPage = page;
        IsOnDashboard = false;
    }

    [RelayCommand]
    private void ShowPackageManager()
    {
        StatusMessage = Properties.Resources.PackageManagerTitle;
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
            var settings = _settingsService.LoadSettings();
            settings.Theme = CurrentTheme;
            _settingsService.SaveSettings(settings);

            StatusMessage = string.Format(Properties.Resources.StatusThemeChanged, CurrentTheme);
            _logger.LogInformation("Theme changed to {Theme}", CurrentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle theme");
            StatusMessage = Properties.Resources.StatusThemeChangeFailed;
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

            // Apply immediately
            var culture = new CultureInfo(CurrentLanguage);
            LocalizeDictionary.Instance.Culture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Save language preference
            var settings = _settingsService.LoadSettings();
            settings.Language = CurrentLanguage;
            _settingsService.SaveSettings(settings);

            StatusMessage = Properties.Resources.LanguageChangedTitle;

            _logger.LogInformation("Language changed to {Language}", CurrentLanguage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle language");
            StatusMessage = Properties.Resources.StatusLanguageChangeFailed;
        }
    }

    /// <summary>
    /// Shows PowerShell service diagnostic information.
    /// </summary>
    [RelayCommand]
    private async Task ShowDiagnosticsAsync()
    {
        try
        {
            _logger.LogInformation("Generating PowerShell diagnostics");
            StatusMessage = Properties.Resources.StatusGeneratingDiagnostics;

            var powerShellService = _serviceProvider.GetService(typeof(IPowerShellService)) as IPowerShellService;
            if (powerShellService == null)
            {
                await ShowAlert(Properties.Resources.DiagnosticsErrorTitle, Properties.Resources.DiagnosticsServiceUnavailable);
                return;
            }

            var diagnostics = await powerShellService.GetDiagnosticInfoAsync();

            _logger.LogInformation("Diagnostics generated successfully");
            _logger.LogInformation(diagnostics);

            // Show in a message box
            var window = new Window
            {
                Title = Properties.Resources.DiagnosticsWindowTitle,
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                Content = new System.Windows.Controls.ScrollViewer
                {
                    Content = new System.Windows.Controls.TextBox
                    {
                        Text = diagnostics,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Padding = new Thickness(10)
                    }
                }
            };
            window.ShowDialog();

            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate diagnostics");
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorGenerateDiagnostics, MainViewModel.FormatAlertMessage(ex)));
            StatusMessage = "Failed to generate diagnostics";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _wslEventWatcher.CacheInvalidationRequested -= OnCacheInvalidated;
    }
}
