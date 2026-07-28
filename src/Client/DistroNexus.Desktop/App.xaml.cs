using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Providers;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace DistroNexus.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        try 
        {
            // Force culture initialization to prevent NRE in WPFLocalizeExtension
            if (LocalizeDictionary.Instance.Culture == null)
            {
                LocalizeDictionary.Instance.Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Default localization initialization failed: {ex.Message}");
        }
    }

    private IHost? _host;
    private ILogger<App>? _logger;

    protected void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== Application OnStartup Begin ===");

            // Configure NLog before anything else
            ConfigureNLog();
            System.Diagnostics.Debug.WriteLine("NLog configured successfully");

            // Set up global exception handlers after NLog
            SetupExceptionHandling();
            System.Diagnostics.Debug.WriteLine("Exception handling setup complete");

            // Build the DI container (this is fast, keep it synchronous)
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register HttpClient
                    services.AddHttpClient();

                    services.AddSingleton<IPowerShellService>(sp => new PowerShellService(sp.GetRequiredService<ILogger<PowerShellService>>()));
                    services.AddSingleton<IPowerShellModuleClient, PowerShellModuleClient>();

                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<WorkspaceStartupRequest>();
                    // Override the Core null navigation sink with the concrete shell bridge.
                    services.AddSingleton<DesktopHealthNavigationBroker>();
                    services.AddSingleton<IHealthNavigationBroker>(sp => sp.GetRequiredService<DesktopHealthNavigationBroker>());
                    services.AddSingleton<IDialogService, DialogService>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<PackageManagerViewModel>();
                    services.AddTransient<TemplatesViewModel>();
                    services.AddTransient<InstallWizardViewModel>();
                    services.AddTransient<SourceManagerViewModel>();
                    services.AddTransient<HealthCenterViewModel>();
                    services.AddTransient<UsbDevicesViewModel>();
                    services.AddTransient<WorkspacesViewModel>();
                    services.AddTransient<ApplicationsViewModel>();
                    services.AddTransient<Wizard.InstallWizardWorkflowViewModel>();

                    // Register Views/Pages
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<SettingsPage>();
                    services.AddTransient<PackageManagerPage>();
                    services.AddTransient<TemplatesPage>();
                    services.AddTransient<HealthCenterPage>();
                    services.AddTransient<UsbDevicesPage>();
                    services.AddTransient<WorkspacesPage>();
                    services.AddTransient<ApplicationsPage>();
                    services.AddTransient<InstallWizardDialog>();
                    services.AddTransient<InstallWizardDialogNew>();

                    // Configure logging with NLog
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                        builder.AddNLog();
                    });
                })
                .Build();

            System.Diagnostics.Debug.WriteLine("DI container built successfully");

            // Get logger after DI is configured
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("DistroNexus application starting");
            if (WorkspaceStartupRoute.TryParse(e.Args, out var workspaceId))
                _host.Services.GetRequiredService<WorkspaceStartupRequest>().WorkspaceId = workspaceId;
            System.Diagnostics.Debug.WriteLine("Logger initialized");

            // PRIORITY: Show main window IMMEDIATELY
            // All other initialization will happen in background after window is shown
            System.Diagnostics.Debug.WriteLine("Creating main window...");
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            System.Diagnostics.Debug.WriteLine("Main window created, showing...");
            mainWindow.Show();

            // A shortcut only routes to the Workspace page and its preview. It never authorizes execution.
            if (_host.Services.GetRequiredService<WorkspaceStartupRequest>().WorkspaceId is not null && mainWindow.DataContext is MainViewModel shell)
                shell.ShowWorkspacesCommand.Execute(null);

            _logger.LogInformation("Main window displayed successfully");
            System.Diagnostics.Debug.WriteLine("=== Main Window Shown - UI is now visible ===");

            // Perform all background initialization asynchronously (non-blocking)
            _ = InitializeApplicationAsync();

            System.Diagnostics.Debug.WriteLine("=== Application OnStartup Complete ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== CRITICAL ERROR IN OnStartup ===");
            System.Diagnostics.Debug.WriteLine($"Exception: {ex}");

            // Critical startup error - show to user
            var errorMessage = string.Format(DistroNexus.Desktop.Properties.Resources.ErrorStartupMessage, ex.Message, ex.StackTrace);
            MessageBox.Show(errorMessage, DistroNexus.Desktop.Properties.Resources.ErrorStartupTitle, MessageBoxButton.OK, MessageBoxImage.Error);

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
    /// Performs background initialization tasks after the main window is shown.
    /// This ensures the UI is visible to the user quickly while other operations complete.
    /// </summary>
    private async Task InitializeApplicationAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== Background Initialization Starting ===");

            // Small delay to ensure window is fully rendered
            await Task.Delay(100);

            var moduleClient = _host?.Services.GetRequiredService<IPowerShellModuleClient>();
            if (moduleClient is null) return;
            // The typed bootstrap operation is the sole product-settings input at startup.
            var bootstrap = await moduleClient.GetBootstrapSettingsAsync();
            ApplyBootstrapSettings(bootstrap.Settings);
            await CheckForUpdatesOnStartupAsync(bootstrap.Settings);

            System.Diagnostics.Debug.WriteLine("=== Background Initialization Complete ===");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Background initialization encountered an error");
            // Don't crash the app for background initialization errors
        }
    }

    /// <summary>
    /// Checks for application updates on startup if enabled in settings.
    /// </summary>
    private async Task CheckForUpdatesOnStartupAsync(GlobalSettings settings)
    {
        try
        {
            if (_host == null)
                return;

            var moduleClient = _host.Services.GetRequiredService<IPowerShellModuleClient>();
            if ((await moduleClient.GetStoreComplianceStatusAsync()).IsStoreManaged)
            {
                _logger?.LogInformation("Skipping update check on startup because Store compliance mode is enabled");
                return;
            }

            if (!settings.CheckUpdatesOnStartup)
            {
                _logger?.LogInformation("Update check on startup is disabled");
                return;
            }

            _logger?.LogInformation("Checking for updates on startup");

            var updateInfo = await moduleClient.GetUpdateStatusAsync();

            if (updateInfo?.IsUpdateAvailable == true)
            {
                _logger?.LogInformation("Update available: {CurrentVersion} -> {LatestVersion}", 
                    updateInfo.CurrentVersion, updateInfo.LatestVersion);

                await Dispatcher.InvokeAsync(async () =>
                {
                    var uiMsgBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = DistroNexus.Desktop.Properties.Resources.UpdateAvailableTitle,
                        Content = string.Format(DistroNexus.Desktop.Properties.Resources.UpdateAvailableMessage, 
                            updateInfo.CurrentVersion, updateInfo.LatestVersion),
                        PrimaryButtonText = "Download",
                        CloseButtonText = "Cancel"
                    };

                    var result = await uiMsgBox.ShowDialogAsync();

                    if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                    {
                        if (updateInfo.ReleaseUri is not null)
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = updateInfo.ReleaseUri.AbsoluteUri, UseShellExecute = true });
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

        // Flush and shutdown NLog
        LogManager.Shutdown();

        _host?.Dispose();
    }

    private void ApplyBootstrapSettings(GlobalSettings settings)
    {
        try
        {
            var language = string.IsNullOrWhiteSpace(settings.Language) ? "en-US" : settings.Language;
            var culture = new System.Globalization.CultureInfo(language);
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            LocalizeDictionary.Instance.Culture = culture;
            _logger?.LogInformation("Applied bootstrap language {Language}", language);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply language: {ex.Message}");
        }
    }

    private void ConfigureNLog()
    {
        try
        {
            // Product-independent defaults deliberately precede the typed bootstrap call.
            LogManager.Configuration ??= new NLog.Config.LoggingConfiguration();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== ConfigureNLog FAILED ===");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Exception Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
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

        var message = string.Format(DistroNexus.Desktop.Properties.Resources.ErrorUnexpectedException, e.Exception.Message);
        
        MessageBox.Show(message, DistroNexus.Desktop.Properties.Resources.ErrorApplicationTitle, MessageBoxButton.OK, MessageBoxImage.Error);

        // Mark as handled to prevent application crash
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _logger?.LogCritical(exception, "Fatal unhandled exception. IsTerminating: {IsTerminating}", e.IsTerminating);

        if (e.IsTerminating)
        {
            var message = string.Format(DistroNexus.Desktop.Properties.Resources.ErrorFatalException, exception?.Message ?? "Unknown error");
            MessageBox.Show(message, DistroNexus.Desktop.Properties.Resources.ErrorFatalTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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

}

