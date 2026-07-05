using System.Windows.Controls;

namespace DistroNexus.Desktop.Controls;

/// <summary>
/// WSL Global Configuration editor section for the Settings page.
/// </summary>
public partial class WslConfigSectionView : UserControl
{
    public WslConfigSectionView()
    {
        InitializeComponent();
    }

    private async void OnExpanded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.WslConfigSectionViewModel vm)
            await vm.LoadAsync();
    }
}
