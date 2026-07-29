using System.Windows.Controls;
using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.Desktop.Views;
public partial class UsbDevicesPage : Page
{
    public UsbDevicesPage(UsbDevicesViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Loaded += async (_, _) => await viewModel.InitializeAsync(); Unloaded += (_, _) => viewModel.Dispose(); }
}
