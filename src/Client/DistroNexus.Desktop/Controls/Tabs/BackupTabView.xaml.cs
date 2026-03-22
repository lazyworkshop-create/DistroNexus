using DistroNexus.Desktop.ViewModels.Tabs;
using System.Windows;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Controls.Tabs;

public partial class BackupTabView : UserControl
{
    public BackupTabView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupTabViewModel vm)
            _ = vm.InitializeAsync();
    }
}
