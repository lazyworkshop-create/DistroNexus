using DistroNexus.Desktop.ViewModels;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Interaction logic for PackageManagerPage.xaml
/// </summary>
public partial class PackageManagerPage : Page
{
    public PackageManagerPage(PackageManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadCatalogCommand.ExecuteAsync(null);
    }
}
