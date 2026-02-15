using DistroNexus.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Views;

public partial class TemplatesPage : Page
{
    private readonly TemplatesViewModel _viewModel;

    public TemplatesPage(TemplatesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += Page_Loaded;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
