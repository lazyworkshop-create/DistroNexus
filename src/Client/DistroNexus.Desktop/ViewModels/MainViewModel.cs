using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Views;
using DistroNexus.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
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
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<WslInstanceViewModel> _instances = new();

    /// <summary>Filterable/groupable view over <see cref="Instances"/>.</summary>
    public ICollectionView InstancesView { get; private set; }

    /// <summary>All tags across all instances, used by the filter bar.</summary>
    public ObservableCollection<TagFilterViewModel> AvailableTags { get; } = [];

    [ObservableProperty]
    private bool _isGroupByTag;

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

    // ── Multi-select mode (P1-8) ──────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCount))]
    private bool _isMultiSelectMode;

    public int SelectedCount => Instances.Count(i => i.IsSelected);

    // ── Auto-refresh indicator (P1-9) ────────────────────────────────────

    [ObservableProperty]
    private bool _isAutoRefreshing;

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
        IDockerIntegrationService dockerIntegrationService,
        IDialogService dialogService)
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
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        // ICollectionView for filtering/grouping (Design Review #4)
        InstancesView = CollectionViewSource.GetDefaultView(_instances);

        // Subscribe to download task status changes
        _downloadTaskManager.TaskStatusChanged += OnDownloadTaskStatusChanged;

        // Subscribe to cache invalidation for auto-refresh (E-07-3)
        _wslEventWatcher.CacheInvalidationRequested += OnCacheInvalidated;

        // NOTE: LoadUserPreferencesAsync is now called explicitly from MainWindow.OnLoaded
        // to avoid async operations in constructor which can block DI resolution

        // Update active downloads count initially
        UpdateActiveDownloadsCount();

        // The Core Health Center requests navigation through an abstraction.  Attach it to the
        // actual shell while it is alive rather than leaving repairs at a no-op sink.
        if (_serviceProvider.GetService<DesktopHealthNavigationBroker>() is { } healthNavigation)
            healthNavigation.RequestHandler = (_, _) => ShowSettings();
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
                await ShowAlert(Properties.Resources.TitleBackupFailure, string.Format(Properties.Resources.ErrorBackupFailedForInstance, inst, msg));
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

    private static readonly System.Text.RegularExpressions.Regex _errorCodePattern =
        new(@"\[DN-(\d+)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private async Task ShowAlert(string title, string message)
    {
        var match = _errorCodePattern.Match(message);

        object content;
        if (match.Success)
        {
            var code = match.Value;
            var panel = new System.Windows.Controls.StackPanel();
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = System.Windows.TextWrapping.Wrap
            });
            var link = new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run(
                    string.Format(Properties.Resources.ErrorCopyCode ?? "Copy error code {0}", code)));
            link.Click += (_, _) =>
                System.Windows.Clipboard.SetText(code);
            var linkBlock = new System.Windows.Controls.TextBlock
            {
                Margin = new System.Windows.Thickness(0, 8, 0, 0)
            };
            linkBlock.Inlines.Add(link);
            panel.Children.Add(linkBlock);
            content = panel;
        }
        else
        {
            content = message;
        }

        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = content,
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
                    var vm = new WslInstanceViewModel(instance, _wslManager, _terminalService, _settingsService, _logger, _tagService, _backupService, _serviceProvider);
                    vm.RefreshRequested += (s, e) => _ = RefreshAsync();
                    vm.TagsChanged += (s, e) => _ = RefreshAvailableTagsAsync();
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

            // Load tags per instance in background (P6-1)
            _ = LoadTagsForInstancesAsync(Instances.ToList(), cancellationToken);

            // Load Docker integration status in the background (C-01-8)
            _ = LoadDockerStatusAsync(Instances.ToList(), cancellationToken);
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

    /// <summary>
    /// Loads tags for each instance and populates <see cref="WslInstanceViewModel.Tags"/>,
    /// then refreshes <see cref="AvailableTags"/>.
    /// </summary>
    private async Task LoadTagsForInstancesAsync(List<WslInstanceViewModel> snapshot, CancellationToken ct)
    {
        try
        {
            foreach (var vm in snapshot)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var tags = await _tagService.GetTagsAsync(vm.Name);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        vm.Tags.Clear();
                        foreach (var t in tags) vm.Tags.Add(t);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load tags for instance {Name}", vm.Name);
                }
            }
            await RefreshAvailableTagsAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tag background load failed");
        }
    }

    /// <summary>
    /// Rebuilds <see cref="AvailableTags"/> from all loaded instance tags,
    /// preserving existing selection state.
    /// </summary>
    internal async Task RefreshAvailableTagsAsync()
    {
        try
        {
            var allTags = await _tagService.GetAllTagsAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Preserve selection of currently selected tags
                var selected = new HashSet<string>(
                    AvailableTags.Where(t => t.IsSelected).Select(t => t.Name),
                    StringComparer.OrdinalIgnoreCase);

                AvailableTags.Clear();
                foreach (var tag in allTags)
                {
                    var tfvm = new TagFilterViewModel { Name = tag, IsSelected = selected.Contains(tag) };
                    tfvm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TagFilterViewModel.IsSelected))
                            ApplyTagFilter();
                    };
                    AvailableTags.Add(tfvm);
                }
                ApplyTagFilter();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh available tags");
        }
    }

    /// <summary>
    /// Applies (or removes) the tag filter predicate on <see cref="InstancesView"/>.
    /// </summary>
    private void ApplyTagFilter()
    {
        var activeFilters = AvailableTags.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        InstancesView.Filter = activeFilters.Count > 0
            ? o => o is WslInstanceViewModel vm && activeFilters.All(f => vm.Tags.Contains(f, StringComparer.OrdinalIgnoreCase))
            : null;
        InstancesView.Refresh();
    }

    /// <summary>Clears all selected tag filters.</summary>
    [RelayCommand]
    private void ClearTagFilters()
    {
        foreach (var tag in AvailableTags)
            tag.IsSelected = false;
        ApplyTagFilter();
    }

    /// <summary>Toggles a single tag filter pill.</summary>
    [RelayCommand]
    private void ToggleTagFilter(TagFilterViewModel? tag)
    {
        if (tag == null) return;
        tag.IsSelected = !tag.IsSelected;
        ApplyTagFilter();
    }

    /// <summary>Deselects a specific tag filter pill (called from × button).</summary>
    [RelayCommand]
    private void ClearSingleTagFilter(TagFilterViewModel? tag)
    {
        if (tag == null) return;
        tag.IsSelected = false;
        ApplyTagFilter();
    }

    /// <summary>Toggles Group by Tag grouping on <see cref="InstancesView"/>.</summary>
    [RelayCommand]
    private void ToggleGroupByTag()
    {
        IsGroupByTag = !IsGroupByTag;
        InstancesView.GroupDescriptions.Clear();
        if (IsGroupByTag)
        {
            // Group by primary tag (first tag); ungrouped instances go to empty group
            InstancesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(WslInstanceViewModel.PrimaryTag)));
        }
        InstancesView.Refresh();
    }

    /// <summary>
    /// Loads Docker integration status for each eligible instance and updates
    /// <see cref="WslInstanceViewModel.DockerIntegrationEnabled"/> asynchronously.
    /// Skips docker-desktop/docker-desktop-data and WSL v1 instances (C-01-8).
    /// </summary>
    private async Task LoadDockerStatusAsync(List<WslInstanceViewModel> snapshot, CancellationToken ct)
    {
        try
        {
            bool isInstalled = await _dockerIntegrationService.IsDockerDesktopInstalledAsync(ct);
            if (!isInstalled) return;

            foreach (var vm in snapshot)
            {
                if (ct.IsCancellationRequested) return;
                var name = vm.Name;
                if (!vm.IsWslV2) continue;
                if (name.Equals("docker-desktop", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("docker-desktop-data", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var status = await _dockerIntegrationService.GetIntegrationStatusAsync(name, ct);
                    vm.DockerIntegrationEnabled = status == Core.Services.DockerIntegrationStatus.Enabled;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get Docker status for instance {Name}", name);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Docker status background load failed");
        }
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

    [RelayCommand]
    private void ShowHealth()
    {
        CurrentPage = _serviceProvider.GetRequiredService<HealthCenterPage>();
        IsOnDashboard = false;
    }

    [RelayCommand]
    private void ShowDevices()
    {
        CurrentPage = _serviceProvider.GetRequiredService<UsbDevicesPage>();
        IsOnDashboard = false;
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
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            IsAutoRefreshing = true;
            try
            {
                await LoadInstancesAsync(CancellationToken.None);
            }
            finally
            {
                IsAutoRefreshing = false;
            }
        });
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
        StatusMessage = Properties.Resources.TemplatesTitle;
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

            StatusMessage = Properties.Resources.StatusReady;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate diagnostics");
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorGenerateDiagnostics, MainViewModel.FormatAlertMessage(ex)));
            StatusMessage = string.Format(Properties.Resources.ErrorGenerateDiagnostics, MainViewModel.FormatAlertMessage(ex));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _wslEventWatcher.CacheInvalidationRequested -= OnCacheInvalidated;
        _wslEventWatcher.Stop();
    }

    /// <summary>
    /// Starts the WSL event watcher after the initial instance load completes.
    /// Called from MainWindow.LoadDataInBackgroundAsync to avoid race conditions (Design Review #1).
    /// </summary>
    public void StartEventWatcherAfterLoad()
    {
        try
        {
            _wslEventWatcher.Start();
            _logger.LogInformation("WSL event watcher started after initial load");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start WSL event watcher; proactive cache invalidation unavailable");
        }
    }

    // ── Multi-select commands (P1-8) ─────────────────────────────────────

    [RelayCommand]
    private void ToggleMultiSelect()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
        {
            foreach (var vm in Instances)
                vm.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    [ObservableProperty]
    private string _bulkCompactProgressText = string.Empty;

    [ObservableProperty]
    private bool _isBulkCompacting;

    private CancellationTokenSource? _bulkCompactCts;

    [RelayCommand]
    private async Task CompactSelectedAsync(CancellationToken ct)
    {
        var selected = Instances.Where(i => i.IsSelected && i.IsWslV2).ToList();
        if (selected.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync(
            Properties.Resources.BulkCompact_ConfirmTitle,
            string.Format(Properties.Resources.BulkCompact_ConfirmMessage, selected.Count));
        if (!confirmed) return;

        IsBulkCompacting = true;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _bulkCompactCts = cts;

        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (cts.IsCancellationRequested) break;

                var inst = selected[i];
                BulkCompactProgressText = string.Format(
                    Properties.Resources.BulkCompact_Counter, i + 1, selected.Count, inst.Name);

                var diskVm = new ViewModels.Tabs.DiskTabViewModel(inst, _wslManager, _dialogService);
                await diskVm.RunCompactionAsync(cts.Token);
            }
        }
        finally
        {
            _bulkCompactCts = null;
            IsBulkCompacting = false;
            BulkCompactProgressText = string.Empty;
            IsMultiSelectMode = false;
            foreach (var vm in Instances) vm.IsSelected = false;
        }
    }

    [RelayCommand]
    private void CancelBulkCompact()
    {
        _bulkCompactCts?.Cancel();
    }

    [RelayCommand]
    private async Task ImportInstanceAsync()
    {
        var existingNames = Instances.Select(i => i.Name).ToList();
        var vm = new ImportInstanceViewModel(existingNames);
        var dialog = new ImportInstanceDialog(vm) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();

        if (!vm.Confirmed) return;

        IsLoading = true;
        try
        {
            await _wslManager.ImportInstanceAsync(
                vm.InstanceName.Trim(),
                vm.SourcePath.Trim(),
                vm.InstallPath.Trim());

            await LoadInstancesAsync();

            var newVm = Instances.FirstOrDefault(i =>
                string.Equals(i.Name, vm.InstanceName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (newVm is not null)
                SelectedInstance = newVm;

            await _dialogService.ShowAlertAsync(
                Properties.Resources.Import_CompleteTitle,
                string.Format(Properties.Resources.Import_Complete, vm.InstanceName.Trim()));
        }
        catch (WslInstanceAlreadyExistsException ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.Import_NameExists, ex.InstanceName ?? vm.InstanceName));
        }
        catch (WslOperationException ex)
        {
            _logger.LogError(ex, "Import failed. ErrorCode={ErrorCode}", (int)ex.Code);
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
        }
    }
}
