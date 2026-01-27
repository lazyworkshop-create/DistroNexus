using System.IO;
using System.Windows;
using System.Windows.Threading;
using DistroNexus.Core.Interfaces;
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
        // Set up global exception handlers before anything else
        SetupExceptionHandling();

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

                // Register ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<PackageManagerViewModel>();
                services.AddTransient<InstallWizardViewModel>();

                // Register Views/Pages
                services.AddSingleton<MainWindow>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<PackageManagerPage>();
                services.AddTransient<InstallWizardDialog>();

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        // Get logger after DI is configured
        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("DistroNexus application starting");

        // Initialize PowerShell module
        InitializePowerShellModule();

        // Show main window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
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

    private async void InitializePowerShellModule()
    {
        try
        {
            var powerShellService = _host?.Services.GetRequiredService<IPowerShellService>();
            if (powerShellService == null)
                return;

            // Get the PowerShell module path
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var modulePath = Path.Combine(baseDir, @"..\..\..\..\..\PowerShell\DistroNexus.psm1");
            modulePath = Path.GetFullPath(modulePath);

            if (File.Exists(modulePath))
            {
                await powerShellService.ImportModuleAsync(modulePath);
                _logger?.LogInformation("PowerShell module loaded from {ModulePath}", modulePath);
            }
            else
            {
                _logger?.LogWarning("PowerShell module not found at {ModulePath}", modulePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize PowerShell module");
            MessageBox.Show($"Failed to initialize PowerShell module: {ex.Message}", 
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

