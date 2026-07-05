using DistroNexus.Desktop.ViewModels.Tabs;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DistroNexus.Desktop.Controls.Tabs;

public partial class IntegrationsTabView : System.Windows.Controls.UserControl
{
    public IntegrationsTabView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is IntegrationsTabViewModel vm)
            _ = vm.InitializeAsync();
    }

    private void OnOpenDockerLink(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* ignore */ }
        e.Handled = true;
    }
}
