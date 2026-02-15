using DistroNexus.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Interaction logic for PackageManagerPage.xaml
/// </summary>
public partial class PackageManagerPage : Page
{
    private readonly PackageManagerViewModel _viewModel;

    public PackageManagerPage(PackageManagerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadCatalogCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Handles clicks on the overlay to close the Add Source panel.
    /// </summary>
    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Close the Add Source panel when clicking on the overlay
        _viewModel.ToggleAddSourcePanelCommand.Execute(null);
    }
}
