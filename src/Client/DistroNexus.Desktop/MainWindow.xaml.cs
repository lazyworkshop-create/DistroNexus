using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(
        IWslManagerService wslManager,
        ISettingsService settingsService,
        ILogger<MainViewModel> logger)
    {
        InitializeComponent();

        _viewModel = new MainViewModel(wslManager, settingsService, logger);
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        await _viewModel.LoadInstancesCommand.ExecuteAsync(null);
    }
}