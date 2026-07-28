using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;
using System.Windows;
using WPFLocalizeExtension.Engine;
using System.Globalization;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IPowerShellModuleClient _moduleClient;
    private readonly ICatalogService _catalogService;
    private readonly ITerminalService _terminalService;
    private readonly IStoreComplianceModeService _storeComplianceModeService;
    private readonly ILogger<SettingsViewModel> _logger;
    private System.Timers.Timer? _autoSaveTimer;

    [ObservableProperty]
    private string _defaultInstallPath = @"C:\WSL";

    [ObservableProperty]
    private string _packageCachePath = string.Empty;

    [ObservableProperty]
    private string _terminalStartPath = "~";

    [ObservableProperty]
    private int _defaultWslVersion = 2;

    /// <summary>
    /// Gets the WSL version index for ComboBox binding (0 = WSL1, 1 = WSL2).
    /// </summary>
    public int WslVersionIndex
    {
        get => DefaultWslVersion - 1;
        set => DefaultWslVersion = value + 1;
    }

    [ObservableProperty]
    private string _defaultUsername = "root";

    [ObservableProperty]
    private bool _enableLogging = true;

    [ObservableProperty]
    private string _logPath = string.Empty;

    [ObservableProperty]
    private bool _checkUpdatesOnStartup = true;

    [ObservableProperty]
    private bool _isUpdateCheckOnStartupAvailable = true;

    [ObservableProperty]
    private string _catalogUrl = string.Empty;

    [ObservableProperty]
    private string _theme = "Auto";

    [ObservableProperty]
    private string _language = "en-US";

    [ObservableProperty]
    private bool _showConfirmationDialogs = true;

    [ObservableProperty]
    private int _maxConcurrentDownloads = 3;

    [ObservableProperty]
    private bool _autoRetryDownloads = true;

    [ObservableProperty]
    private int _maxRetryAttempts = 3;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _autoSaveEnabled = true;

    [ObservableProperty]
    private int _autoSaveInterval = 30;

    [ObservableProperty]
    private string _autoSaveStatus = "Auto-save enabled";

    [ObservableProperty]
    private ObservableCollection<DistroPackage> _availableDistributions = new();

    [ObservableProperty]
    private DistroPackage? _defaultDistribution;

    // Cache management properties
    [ObservableProperty]
    private string _cachePath = string.Empty;

    [ObservableProperty]
    private int _cachedPackageCount;

    [ObservableProperty]
    private string _cacheTotalSize = "0 B";

    [ObservableProperty]
    private ObservableCollection<CachedPackageInfo> _cachedPackages = [];

    [ObservableProperty]
    private string? _powerShellModulePath;

    /// <summary>WSL Global Configuration editor section (E-01).</summary>
    public WslConfigSectionViewModel WslConfigSection { get; }

    /// <summary>Manage Tags section (E-02-9).</summary>
    public ManageTagsViewModel ManageTags { get; }

    public SettingsViewModel(
        ICatalogService catalogService,
        ITerminalService terminalService,
        IStoreComplianceModeService storeComplianceModeService,
        ILogger<SettingsViewModel> logger,
        IWslConfigService wslConfigService,
        IWslManagerService wslManagerService,
        IPowerShellModuleClient moduleClient,
        IDialogService dialogService,
        IWslConfigurationService configurationService,
        IPlatformCapabilityService capabilityService)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
        _storeComplianceModeService = storeComplianceModeService ?? throw new ArgumentNullException(nameof(storeComplianceModeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        WslConfigSection = new WslConfigSectionViewModel(wslConfigService, moduleClient, dialogService, configurationService, capabilityService);
        ManageTags = new ManageTagsViewModel(moduleClient, dialogService);
        
        // Initialize auto-save timer
        SetupAutoSaveTimer();
    }

    private async Task ShowAlert(string title, string message)
    {
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            MaxWidth = 400
        };

        await uiMessageBox.ShowDialogAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            _logger.LogInformation("Loading settings");

            var settings = await _moduleClient.GetSettingsAsync();
            var isStoreComplianceMode = _storeComplianceModeService.IsStoreComplianceModeEnabled();

            DefaultInstallPath = settings.DefaultInstallPath;
            PackageCachePath = settings.PackageCachePath;
            TerminalStartPath = settings.TerminalStartPath;
            DefaultWslVersion = settings.DefaultWslVersion;
            DefaultUsername = settings.DefaultUsername;
            EnableLogging = settings.EnableLogging;
            LogPath = settings.LogPath;
            CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
            IsUpdateCheckOnStartupAvailable = !isStoreComplianceMode;

            if (isStoreComplianceMode)
            {
                CheckUpdatesOnStartup = false;
            }
            CatalogUrl = settings.CatalogUrl;
            Theme = settings.Theme;
            Language = settings.Language;
            ShowConfirmationDialogs = settings.ShowConfirmationDialogs;
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads;
            AutoRetryDownloads = settings.AutoRetryDownloads;
            MaxRetryAttempts = settings.MaxRetryAttempts;
            AutoSaveEnabled = settings.AutoSaveEnabled;
            AutoSaveInterval = settings.AutoSaveInterval;
            PowerShellModulePath = settings.PowerShellModulePath;

            // Load available distributions for default selection
            await LoadDistributionsAsync();

            // Load cache info
            await RefreshCacheInfoAsync();

            IsDirty = false;
            _logger.LogInformation("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorLoadSettings, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    private async Task LoadDistributionsAsync()
    {
        try
        {
            var distributions = await _moduleClient.GetPackagesAsync();
            AvailableDistributions.Clear();
            foreach (var distro in distributions)
            {
                AvailableDistributions.Add(distro);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load distributions for settings");
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            _logger.LogInformation("Saving settings");

            await _moduleClient.SaveSettingsAsync(CreateSettingsUpdate(includePowerShellModulePath: true));

            // Apply theme immediately
            await ApplyThemeAsync(Theme);

            IsDirty = false;

            // Apply language immediately
            if (!string.IsNullOrEmpty(Language))
            {
                try 
                {
                    System.Diagnostics.Debug.WriteLine($"Changing language to: {Language}");
                    var culture = new CultureInfo(Language);
                    LocalizeDictionary.Instance.Culture = culture;
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = culture;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to switch language to {Language}", Language);
                }
            }

            await ShowAlert(Properties.Resources.Success, Properties.Resources.SettingsSaved);

            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.SaveSettingsError, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            "Confirm Reset",
            "Are you sure you want to reset all settings to defaults?",
            "Reset");

        if (!confirmed)
            return;

        try
        {
            _logger.LogInformation("Resetting settings to defaults");

            await _moduleClient.ResetSettingsAsync();
            await LoadSettingsAsync();

            await ShowAlert(Properties.Resources.Success, Properties.Resources.StatusSettingsReset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorResetSettings, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private void BrowseInstallPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select default installation path",
            InitialDirectory = DefaultInstallPath
        };

        if (dialog.ShowDialog() == true)
        {
            DefaultInstallPath = dialog.FolderName;
            IsDirty = true;
        }
    }

    [RelayCommand]
    private void BrowseCachePath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select package cache path",
            InitialDirectory = PackageCachePath
        };

        if (dialog.ShowDialog() == true)
        {
            PackageCachePath = dialog.FolderName;
            IsDirty = true;
        }
    }

    /// <summary>
    /// Applies the selected theme to the application.
    /// </summary>
    /// <param name="themeName">The name of the theme to apply ("Light", "Dark", or "Auto").</param>
    private async Task ApplyThemeAsync(string themeName)
    {
        try
        {
            _logger.LogInformation("Applying theme: {Theme}", themeName);

            var app = (App)Application.Current;
            app.ApplyThemeFromSettings(themeName);

            _logger.LogInformation("Theme applied successfully: {Theme}", themeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply theme");
            await ShowAlert(Properties.Resources.TitleThemeError, string.Format(Properties.Resources.ErrorApplyTheme, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private void BrowseLogPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select log file path",
            InitialDirectory = LogPath
        };

        if (dialog.ShowDialog() == true)
        {
            LogPath = dialog.FolderName;
            IsDirty = true;
        }
    }

    [RelayCommand]
    private void BrowseTerminalPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select terminal start path",
            InitialDirectory = string.IsNullOrEmpty(TerminalStartPath) || TerminalStartPath == "~" 
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) 
                : TerminalStartPath
        };

        if (dialog.ShowDialog() == true)
        {
            TerminalStartPath = dialog.FolderName;
            IsDirty = true;
        }
    }

    [RelayCommand]
    private async Task BrowsePowerShellModulePath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select PowerShell Module Directory (containing DistroNexus.psd1)",
            InitialDirectory = !string.IsNullOrEmpty(PowerShellModulePath) && Directory.Exists(PowerShellModulePath)
                ? PowerShellModulePath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedPath = dialog.FolderName;

            // Validate that the directory contains DistroNexus.psd1
            var manifestPath = Path.Combine(selectedPath, "DistroNexus.psd1");
            if (File.Exists(manifestPath))
            {
                PowerShellModulePath = selectedPath;
                IsDirty = true;
                _logger.LogInformation("PowerShell module path set to: {Path}", selectedPath);
            }
            else
            {
                await ShowAlert(Properties.Resources.TitleInvalidModulePath, Properties.Resources.ErrorInvalidModulePath);
                _logger.LogWarning("Invalid PowerShell module path selected: {Path}", selectedPath);
            }
        }
    }

    [RelayCommand]
    private void ClearPowerShellModulePath()
    {
        PowerShellModulePath = null;
        IsDirty = true;
        _logger.LogInformation("PowerShell module path cleared. Will use auto-detection.");
    }

    partial void OnDefaultInstallPathChanged(string value) => IsDirty = true;
    partial void OnPackageCachePathChanged(string value) => IsDirty = true;
    partial void OnTerminalStartPathChanged(string value) => IsDirty = true;
    partial void OnDefaultWslVersionChanged(int value) => IsDirty = true;
    partial void OnDefaultUsernameChanged(string value) => IsDirty = true;
    partial void OnEnableLoggingChanged(bool value) => IsDirty = true;
    partial void OnCheckUpdatesOnStartupChanged(bool value) => IsDirty = true;
    partial void OnCatalogUrlChanged(string value) => IsDirty = true;
    partial void OnThemeChanged(string value) => IsDirty = true;
    partial void OnLanguageChanged(string value) => IsDirty = true;
    partial void OnShowConfirmationDialogsChanged(bool value) => IsDirty = true;
    partial void OnMaxConcurrentDownloadsChanged(int value) => IsDirty = true;
    partial void OnAutoRetryDownloadsChanged(bool value) => IsDirty = true;
    partial void OnMaxRetryAttemptsChanged(int value) => IsDirty = true;
    partial void OnDefaultDistributionChanged(DistroPackage? value) => IsDirty = true;
    partial void OnAutoSaveEnabledChanged(bool value) => SetupAutoSaveTimer();
    partial void OnAutoSaveIntervalChanged(int value) => SetupAutoSaveTimer();

    /// <summary>
    /// Refreshes cache usage information.
    /// </summary>
    [RelayCommand]
    private async Task RefreshCacheInfoAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing cache info");

            var cacheInfo = await _catalogService.GetCacheUsageAsync();

            CachePath = cacheInfo.CachePath;
            CachedPackageCount = cacheInfo.PackageCount;
            CacheTotalSize = cacheInfo.TotalSizeDisplay;

            CachedPackages.Clear();
            foreach (var package in cacheInfo.CachedPackages)
            {
                CachedPackages.Add(package);
            }

            _logger.LogInformation("Cache info refreshed: {Count} packages, {Size}", 
                CachedPackageCount, CacheTotalSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh cache info");
        }
    }

    /// <summary>
    /// Clears all cached packages.
    /// </summary>
    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.ConfirmTitle,
            string.Format(Properties.Resources.ConfirmClearCache, CachedPackageCount),
            "Clear Cache");

        if (!confirmed)
            return;

        try
        {
            _logger.LogInformation("Clearing cache");

            var deletedCount = await _catalogService.ClearAllCacheAsync();

            await RefreshCacheInfoAsync();

            await ShowAlert(Properties.Resources.Success, string.Format(Properties.Resources.StatusCacheCleared, deletedCount));

            _logger.LogInformation("Cache cleared: {Count} files deleted", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorClearCache, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    /// <summary>
    /// Deletes a single cached package file.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCachedPackageAsync(CachedPackageInfo package)
    {
        if (package == null)
            return;

        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            "Confirm Delete",
            $"Are you sure you want to delete the cached file '{package.FileName}'?",
            "Delete");

        if (!confirmed)
            return;

        try
        {
            _logger.LogInformation("Deleting cached package: {PackageId}", package.PackageId);

            // Use catalog service to delete the package
            await _catalogService.DeleteCachedPackageAsync(package.PackageId);

            await RefreshCacheInfoAsync();

            _logger.LogInformation("Deleted cached package: {FileName}", package.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cached file");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.DeleteFileError, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    /// <summary>
    /// Opens the cache folder in File Explorer.
    /// </summary>
    [RelayCommand]
    private async Task OpenCacheFolderAsync()
    {
        try
        {
            var cachePath = _catalogService.GetPackageCachePath();

            if (!string.IsNullOrEmpty(cachePath))
            {
                var success = await _terminalService.OpenFileExplorerAsync(cachePath);
                
                if (!success)
                {
                    await ShowAlert(Properties.Resources.ErrorTitle, Properties.Resources.OpenCacheFolderError);
                }
            }
            else
            {
                await ShowAlert(Properties.Resources.InformationTitle, Properties.Resources.CachePathNotConfigured);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open cache folder");
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorOpenCacheFolder, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    /// <summary>
    /// Navigates back to the dashboard.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        _logger.LogInformation("Navigating back from settings");
        
        // Get the MainViewModel from the application's main window
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.ShowDashboardCommand.Execute(null);
        }
    }

    /// <summary>
    /// Sets up the auto-save timer based on current settings.
    /// </summary>
    private void SetupAutoSaveTimer()
    {
        // Dispose existing timer
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;

        if (AutoSaveEnabled && AutoSaveInterval > 0)
        {
            _autoSaveTimer = new System.Timers.Timer(AutoSaveInterval * 1000); // Convert seconds to milliseconds
            _autoSaveTimer.Elapsed += OnAutoSaveTimerElapsed;
            _autoSaveTimer.AutoReset = true;
            _autoSaveTimer.Start();

            AutoSaveStatus = $"Auto-save enabled (every {AutoSaveInterval}s)";
            _logger.LogInformation("Auto-save timer started: {Interval}s", AutoSaveInterval);
        }
        else
        {
            AutoSaveStatus = AutoSaveEnabled ? "Auto-save enabled" : "Auto-save disabled";
            _logger.LogInformation("Auto-save timer stopped");
        }
    }

    /// <summary>
    /// Handles the auto-save timer elapsed event.
    /// </summary>
    private async void OnAutoSaveTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (IsDirty)
        {
            try
            {
                _logger.LogInformation("Auto-saving settings");
                
                await _moduleClient.SaveSettingsAsync(CreateSettingsUpdate(includePowerShellModulePath: false));

                IsDirty = false;
                AutoSaveStatus = $"Last auto-saved: {DateTime.Now:HH:mm:ss}";

                _logger.LogInformation("Auto-save completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-save failed");
                AutoSaveStatus = "Auto-save failed";
            }
        }
    }

    /// <summary>
    /// Called when the ViewModel is disposed to clean up resources.
    /// </summary>
    public void Dispose()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
    }

    private DistroNexusSettingsUpdate CreateSettingsUpdate(bool includePowerShellModulePath) => new(
        DefaultInstallPath,
        PackageCachePath,
        TerminalStartPath,
        DefaultWslVersion,
        DefaultUsername,
        DefaultDistribution?.Id ?? string.Empty,
        EnableLogging,
        LogPath,
        IsUpdateCheckOnStartupAvailable && CheckUpdatesOnStartup,
        CatalogUrl,
        Theme,
        Language,
        ShowConfirmationDialogs,
        MaxConcurrentDownloads,
        AutoRetryDownloads,
        MaxRetryAttempts,
        AutoSaveEnabled,
        AutoSaveInterval,
        PowerShellModulePath,
        includePowerShellModulePath);
}
