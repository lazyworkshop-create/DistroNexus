using DistroNexus.Desktop.ViewModels.Tabs;
using System.Windows;

namespace DistroNexus.Desktop.Controls.Tabs;

public partial class NetworkTabView : System.Windows.Controls.UserControl
{
    public NetworkTabView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkTabViewModel vm)
            _ = vm.InitializeAsync();
    }
}
