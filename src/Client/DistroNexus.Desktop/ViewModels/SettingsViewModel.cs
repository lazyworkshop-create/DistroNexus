using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<SettingsViewModel> _logger;

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

    public SettingsViewModel(
        ISettingsService settingsService, 
        ICatalogService catalogService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            _logger.LogInformation("Loading settings");

            var settings = await _settingsService.LoadSettingsAsync();

            DefaultInstallPath = settings.DefaultInstallPath;
            PackageCachePath = settings.PackageCachePath;
            TerminalStartPath = settings.TerminalStartPath;
            DefaultWslVersion = settings.DefaultWslVersion;
            DefaultUsername = settings.DefaultUsername;
            EnableLogging = settings.EnableLogging;
            LogPath = settings.LogPath;
            CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
            CatalogUrl = settings.CatalogUrl;
            Theme = settings.Theme;
            Language = settings.Language;
            ShowConfirmationDialogs = settings.ShowConfirmationDialogs;
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads;
            AutoRetryDownloads = settings.AutoRetryDownloads;
            MaxRetryAttempts = settings.MaxRetryAttempts;

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
            MessageBox.Show($"Failed to load settings: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadDistributionsAsync()
    {
        try
        {
            var distributions = await _catalogService.LoadCatalogAsync();
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

            var settings = new GlobalSettings
            {
                DefaultInstallPath = DefaultInstallPath,
                PackageCachePath = PackageCachePath,
                TerminalStartPath = TerminalStartPath,
                DefaultWslVersion = DefaultWslVersion,
                DefaultUsername = DefaultUsername,
                DefaultDistributionId = DefaultDistribution?.Id ?? string.Empty,
                EnableLogging = EnableLogging,
                LogPath = LogPath,
                CheckUpdatesOnStartup = CheckUpdatesOnStartup,
                CatalogUrl = CatalogUrl,
                Theme = Theme,
                Language = Language,
                ShowConfirmationDialogs = ShowConfirmationDialogs,
                MaxConcurrentDownloads = MaxConcurrentDownloads,
                AutoRetryDownloads = AutoRetryDownloads,
                MaxRetryAttempts = MaxRetryAttempts
            };

            await _settingsService.SaveSettingsAsync(settings);

            // Apply theme immediately
            ApplyTheme(Theme);

            IsDirty = false;
            MessageBox.Show("Settings saved successfully", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show($"Failed to save settings: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to defaults?",
            "Confirm Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Resetting settings to defaults");

            await _settingsService.ResetSettingsAsync();
            await LoadSettingsAsync();

            MessageBox.Show("Settings reset to defaults", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            MessageBox.Show($"Failed to reset settings: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
    private void ApplyTheme(string themeName)
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
            MessageBox.Show($"Failed to apply theme: {ex.Message}",
                "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        var result = MessageBox.Show(
            "Are you sure you want to clear all cached packages? This will free up disk space but downloaded packages will need to be re-downloaded.",
            "Confirm Clear Cache",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Clearing cache");

            var deletedCount = await _catalogService.ClearAllCacheAsync();

            await RefreshCacheInfoAsync();

            MessageBox.Show($"Successfully cleared {deletedCount} cached files.", 
                "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);

            _logger.LogInformation("Cache cleared: {Count} files deleted", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            MessageBox.Show($"Failed to clear cache: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        var result = MessageBox.Show(
            $"Are you sure you want to delete the cached file '{package.FileName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Deleting cached file: {FilePath}", package.FilePath);

            if (System.IO.File.Exists(package.FilePath))
            {
                System.IO.File.Delete(package.FilePath);
            }

            await RefreshCacheInfoAsync();

            _logger.LogInformation("Deleted cached file: {FileName}", package.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cached file");
            MessageBox.Show($"Failed to delete file: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens the cache folder in File Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenCacheFolder()
    {
        try
        {
            var cachePath = _catalogService.GetPackageCachePath();

            if (!string.IsNullOrEmpty(cachePath) && System.IO.Directory.Exists(cachePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cachePath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Cache folder does not exist yet.", 
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open cache folder");
            MessageBox.Show($"Failed to open cache folder: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
