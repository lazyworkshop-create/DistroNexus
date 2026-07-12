using System.Windows.Controls;
using DistroNexus.Desktop.ViewModels;
namespace DistroNexus.Desktop.Views;
public partial class HealthCenterPage : Page
{
    public HealthCenterPage(HealthCenterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            await viewModel.InitializeAsync();
            if (viewModel.IsHealthAvailable) await viewModel.RescanCommand.ExecuteAsync(null);
        };
    }
}
