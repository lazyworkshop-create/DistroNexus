using System.IO;
using System.Windows;
using System.Windows.Threading;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ILogger<App>? _logger;

    protected void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== Application OnStartup Begin ===");
            
            // Set up global exception handlers before anything else
            SetupExceptionHandling();
            System.Diagnostics.Debug.WriteLine("Exception handling setup complete");

            // Build the DI container
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register HttpClient
                    services.AddHttpClient();

                    // Register Core services
                    services.AddSingleton<IPowerShellService, PowerShellService>();
                    services.AddSingleton<IWslManagerService, WslManagerService>();
                    services.AddSingleton<IDownloadService, DownloadService>();
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<ICatalogService, CatalogService>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IUpdateService, UpdateService>();
                    services.AddSingleton<ITerminalService, TerminalService>();
                    services.AddSingleton<IDownloadTaskManager, DownloadTaskManager>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<PackageManagerViewModel>();
                    services.AddTransient<InstallWizardViewModel>();
                    services.AddTransient<Wizard.InstallWizardWorkflowViewModel>();

                    // Register Views/Pages
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<SettingsPage>();
                    services.AddTransient<PackageManagerPage>();
                    services.AddTransient<InstallWizardDialog>();
                    services.AddTransient<InstallWizardDialogNew>();

                    // Configure logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                })
                .Build();

            System.Diagnostics.Debug.WriteLine("DI container built successfully");

            // Get logger after DI is configured
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("DistroNexus application starting");
            System.Diagnostics.Debug.WriteLine("Logger initialized");

            // Note: Theme will be loaded and applied in MainWindow.OnLoaded
            // to avoid async/sync context issues during startup
            System.Diagnostics.Debug.WriteLine("Skipping theme load in OnStartup - will load in window");

            // Initialize PowerShell module
            System.Diagnostics.Debug.WriteLine("Initializing PowerShell module...");
            InitializePowerShellModule();
            System.Diagnostics.Debug.WriteLine("PowerShell module initialized");

            // Show main window
            System.Diagnostics.Debug.WriteLine("Creating main window...");
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            System.Diagnostics.Debug.WriteLine("Main window created, showing...");
            mainWindow.Show();
            
            _logger.LogInformation("Main window displayed successfully");
            
            // Check for updates in background (non-blocking)
            _ = CheckForUpdatesOnStartupAsync();
            
            System.Diagnostics.Debug.WriteLine("=== Application OnStartup Complete ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== CRITICAL ERROR IN OnStartup ===");
            System.Diagnostics.Debug.WriteLine($"Exception: {ex}");
            
            // Critical startup error - show to user
            var errorMessage = $"Failed to start DistroNexus:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            MessageBox.Show(errorMessage, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Log the error if logger is available
            _logger?.LogCritical(ex, "Critical error during application startup");
            
            // Shutdown the application
            Shutdown(1);
        }
    }

    /// <summary>
    /// Applies theme. This is now called from MainWindow after it loads and when settings are changed.
    /// </summary>
    public void ApplyThemeFromSettings(string themeName)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"ApplyThemeFromSettings: Applying {themeName}");
            
            Wpf.Ui.Appearance.ApplicationTheme theme;
            
            if (themeName == "Dark")
            {
                theme = Wpf.Ui.Appearance.ApplicationTheme.Dark;
            }
            else if (themeName == "Light")
            {
                theme = Wpf.Ui.Appearance.ApplicationTheme.Light;
            }
            else // Auto or any other value
            {
                // Default to system theme or Light if unable to detect
                theme = Wpf.Ui.Appearance.ApplicationTheme.Light;
                System.Diagnostics.Debug.WriteLine($"ApplyThemeFromSettings: Auto mode, defaulting to Light");
            }

            // Apply theme to the application with backdrop and force update
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica, true);
            
            // Update the resource dictionary
            Dispatcher.Invoke(() =>
            {
                var dictionaries = Resources.MergedDictionaries;
                var themeDict = dictionaries.OfType<Wpf.Ui.Markup.ThemesDictionary>().FirstOrDefault();
                
                if (themeDict != null)
                {
                    themeDict.Theme = theme;
                }
            });
            
            System.Diagnostics.Debug.WriteLine($"ApplyThemeFromSettings: Successfully applied {theme}");
            _logger?.LogInformation("Theme applied: {Theme} (requested: {RequestedTheme})", theme, themeName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyThemeFromSettings: ERROR: {ex}");
            _logger?.LogError(ex, "Failed to apply theme");
        }
    }

    /// <summary>
    /// Checks for application updates on startup if enabled in settings.
    /// </summary>
    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            if (_host == null)
                return;

            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            var settings = await settingsService.LoadSettingsAsync();

            if (!settings.CheckUpdatesOnStartup)
            {
                _logger?.LogInformation("Update check on startup is disabled");
                return;
            }

            _logger?.LogInformation("Checking for updates on startup");

            var updateService = _host.Services.GetRequiredService<IUpdateService>();
            var updateInfo = await updateService.CheckForUpdatesAsync();

            if (updateInfo?.IsUpdateAvailable == true)
            {
                _logger?.LogInformation("Update available: {CurrentVersion} -> {LatestVersion}", 
                    updateInfo.CurrentVersion, updateInfo.LatestVersion);

                await Dispatcher.InvokeAsync(() =>
                {
                    var result = MessageBox.Show(
                        $"A new version of DistroNexus is available!\n\n" +
                        $"Current version: {updateInfo.CurrentVersion}\n" +
                        $"Latest version: {updateInfo.LatestVersion}\n\n" +
                        $"Would you like to open the download page?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        updateService.OpenDownloadPage(updateInfo.ReleaseUrl);
                    }
                });
            }
            else
            {
                _logger?.LogInformation("Application is up to date");
            }
        }
        catch (Exception ex)
        {
            // Don't show error to user - update check is non-critical
            _logger?.LogWarning(ex, "Failed to check for updates on startup");
        }
    }

    protected void OnExit(object sender, ExitEventArgs e)
    {
        _logger?.LogInformation("DistroNexus application exiting");
        _host?.Dispose();
    }

    private void SetupExceptionHandling()
    {
        // Handle UI thread exceptions
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Handle non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Handle Task exceptions that were not observed
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI thread exception");

        var message = $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will attempt to continue.";
        
        MessageBox.Show(message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);

        // Mark as handled to prevent application crash
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _logger?.LogCritical(exception, "Fatal unhandled exception. IsTerminating: {IsTerminating}", e.IsTerminating);

        if (e.IsTerminating)
        {
            var message = $"A fatal error occurred:\n\n{exception?.Message ?? "Unknown error"}\n\nThe application will now close.";
            MessageBox.Show(message, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");

        // Prevent the exception from terminating the application
        e.SetObserved();

        // Log individual inner exceptions
        foreach (var innerException in e.Exception.InnerExceptions)
        {
            _logger?.LogError(innerException, "Inner task exception");
        }
    }

    private void InitializePowerShellModule()
    {
        // Note: The DistroNexus PowerShell module is optional.
        // WslManagerService now uses inline PowerShell scripts directly,
        // so the external module is not required for core functionality.
        // This method is kept for future extensibility if a module is added.
        
        try
        {
            var powerShellService = _host?.Services.GetRequiredService<IPowerShellService>();
            if (powerShellService == null)
                return;

            // Check for optional PowerShell module in multiple locations
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possiblePaths =
            [
                Path.Combine(baseDir, "PowerShell", "DistroNexus.psm1"),
                Path.Combine(baseDir, @"..\..\..\..\..\PowerShell\DistroNexus.psm1"),
                Path.Combine(baseDir, @"..\..\..\..\src\PowerShell\DistroNexus.psm1")
            ];

            foreach (var path in possiblePaths)
            {
                var modulePath = Path.GetFullPath(path);
                if (File.Exists(modulePath))
                {
                    // Module found - try to load it (fire and forget, non-blocking)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await powerShellService.ImportModuleAsync(modulePath);
                            _logger?.LogInformation("Optional PowerShell module loaded from {ModulePath}", modulePath);
                        }
                        catch (Exception ex)
                        {
                            // Module loading is optional, just log the warning
                            _logger?.LogWarning(ex, "Could not load optional PowerShell module from {ModulePath}", modulePath);
                        }
                    });
                    return;
                }
            }

            // No module found - this is fine, core functionality uses inline scripts
            _logger?.LogDebug("No DistroNexus PowerShell module found. Using inline scripts for WSL operations.");
        }
        catch (Exception ex)
        {
            // Don't show error dialogs for optional module loading
            _logger?.LogWarning(ex, "Error during optional PowerShell module initialization");
        }
    }
}

