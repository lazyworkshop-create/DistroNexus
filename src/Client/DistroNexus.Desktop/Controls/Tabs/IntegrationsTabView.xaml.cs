using DistroNexus.Desktop.ViewModels.Tabs;
using System.Windows;

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
}
