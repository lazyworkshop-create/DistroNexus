using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using System.Windows;

namespace DistroNexus.Desktop.Services;

/// <summary>
/// Bridges Core health repair navigation requests to the already-created WPF shell.
/// It deliberately performs no configuration change; it only brings the user to Settings.
/// </summary>
public sealed class DesktopHealthNavigationBroker : IHealthNavigationBroker
{
    internal Action<string, HealthFinding>? RequestHandler { get; set; }

    public void Request(string target, HealthFinding finding)
    {
        if (!string.Equals(target, "settings", StringComparison.OrdinalIgnoreCase)) return;
        if (RequestHandler is not null)
        {
            RequestHandler(target, finding);
            return;
        }

        var application = Application.Current;
        if (application?.Dispatcher is null) return;
        application.Dispatcher.BeginInvoke(() =>
        {
            if (application.MainWindow?.DataContext is MainViewModel main)
                main.ShowSettingsCommand.Execute(null);
        });
    }
}
