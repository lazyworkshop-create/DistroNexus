using DistroNexus.Desktop.ViewModels;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadSettingsCommand.ExecuteAsync(null);
    }
}
