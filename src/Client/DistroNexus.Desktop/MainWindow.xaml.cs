using DistroNexus.Desktop.ViewModels;
using System.Windows;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace DistroNexus.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        try
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += OnLoaded;
            
            System.Diagnostics.Debug.WriteLine("MainWindow initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow constructor error: {ex}");
            MessageBox.Show($"Failed to initialize MainWindow:\n\n{ex.Message}", "Initialization Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("MainWindow OnLoaded started");
            
            // Load and apply theme from settings
            await LoadAndApplyThemeAsync();
            
            // Load WSL instances
            await _viewModel.LoadInstancesCommand.ExecuteAsync(null);
            
            System.Diagnostics.Debug.WriteLine("MainWindow OnLoaded completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow OnLoaded error: {ex}");
            MessageBox.Show($"Failed to load application data:\n\n{ex.Message}", "Load Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Loads saved theme and applies it.
    /// </summary>
    private async Task LoadAndApplyThemeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("LoadAndApplyThemeAsync: Starting");
            
            // The app provides access to services through the application instance
            var app = (App)Application.Current;
            
            // Load theme from ViewModel which has already loaded preferences
            var themeName = _viewModel.CurrentTheme;
            System.Diagnostics.Debug.WriteLine($"LoadAndApplyThemeAsync: Theme from ViewModel = {themeName}");
            
            // Apply theme
            app.ApplyThemeFromSettings(themeName);
            
            System.Diagnostics.Debug.WriteLine("LoadAndApplyThemeAsync: Completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadAndApplyThemeAsync: ERROR: {ex}");
            // Don't show error dialog for theme loading failure
        }
        
        await Task.CompletedTask;
    }
}


