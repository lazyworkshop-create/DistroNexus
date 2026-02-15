using DistroNexus.Desktop.Wizard;
using System.Windows;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Interaction logic for InstallWizardDialogNew.xaml
/// </summary>
public partial class InstallWizardDialogNew : FluentWindow
{
    private readonly InstallWizardWorkflowViewModel _viewModel;

    public InstallWizardDialogNew(InstallWizardWorkflowViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Subscribe to wizard completion
        _viewModel.WizardCompleted += OnWizardCompleted;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize the wizard
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private void OnWizardCompleted(object? sender, bool success)
    {
        DialogResult = success;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Unsubscribe from events
        _viewModel.WizardCompleted -= OnWizardCompleted;
    }
}
