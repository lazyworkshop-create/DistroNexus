using System.Windows;
using DistroNexus.Desktop.ViewModels;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Interaction logic for InstallWizardDialog.xaml
/// </summary>
public partial class InstallWizardDialog : FluentWindow
{
    private readonly InstallWizardViewModel _viewModel;

    public InstallWizardDialog(InstallWizardViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        InitializeComponent();

        // Subscribe to wizard completion
        _viewModel.WizardCompleted += OnWizardCompleted;

        // Initialize the wizard
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private void OnWizardCompleted(object? sender, bool success)
    {
        DialogResult = success;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.WizardCompleted -= OnWizardCompleted;
        base.OnClosed(e);
    }
}
