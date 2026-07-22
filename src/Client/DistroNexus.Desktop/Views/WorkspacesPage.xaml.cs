using System.Windows.Controls;
using DistroNexus.Desktop.ViewModels;
namespace DistroNexus.Desktop.Views;
public partial class WorkspacesPage : Page { public WorkspacesPage(WorkspacesViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Loaded += async (_, _) => await viewModel.InitializeAsync(); } }
