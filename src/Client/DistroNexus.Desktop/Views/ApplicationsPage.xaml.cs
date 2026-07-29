using System.Windows.Controls;
using DistroNexus.Desktop.ViewModels;
namespace DistroNexus.Desktop.Views;
public partial class ApplicationsPage : Page { public ApplicationsPage(ApplicationsViewModel viewModel) { InitializeComponent(); DataContext=viewModel; } }
