using System.IO;
using System.Windows;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected void OnStartup(object sender, StartupEventArgs e)
    {
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

                // Register MainWindow
                services.AddSingleton<MainWindow>();

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        // Initialize PowerShell module
        InitializePowerShellModule();

        // Show main window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected void OnExit(object sender, ExitEventArgs e)
    {
        _host?.Dispose();
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
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize PowerShell module: {ex.Message}", 
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

