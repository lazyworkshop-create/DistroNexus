using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private string _defaultInstallPath = @"C:\WSL";

    [ObservableProperty]
    private int _defaultWslVersion = 2;

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

    public SettingsViewModel(ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
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

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            _logger.LogInformation("Saving settings");

            var settings = new GlobalSettings
            {
                DefaultInstallPath = DefaultInstallPath,
                DefaultWslVersion = DefaultWslVersion,
                DefaultUsername = DefaultUsername,
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
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select default installation path",
            SelectedPath = DefaultInstallPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            DefaultInstallPath = dialog.SelectedPath;
            IsDirty = true;
        }
    }

    partial void OnDefaultInstallPathChanged(string value) => IsDirty = true;
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
}
