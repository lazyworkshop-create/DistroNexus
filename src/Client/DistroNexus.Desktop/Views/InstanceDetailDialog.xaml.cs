using DistroNexus.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Code-behind for InstanceDetailDialog.
/// </summary>
public partial class InstanceDetailDialog : FluentWindow
{
    public InstanceDetailDialog(InstanceDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
