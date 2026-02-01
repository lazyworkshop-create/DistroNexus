using DistroNexus.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
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

            // Show loading state IMMEDIATELY before any async operations
            _viewModel.StatusMessage = "Initializing application...";
            _viewModel.IsLoading = true;

            // Small delay to ensure loading overlay is rendered
            await Task.Delay(50);

            // Initialize ViewModel (loads user preferences including theme)
            System.Diagnostics.Debug.WriteLine("Initializing ViewModel...");
            await _viewModel.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("ViewModel initialized");

            // Now load and apply theme from loaded settings
            await LoadAndApplyThemeAsync();

            System.Diagnostics.Debug.WriteLine("MainWindow OnLoaded completed - UI is now visible and responsive");

            // Start background data loading (fire and forget, with error handling)
            _ = LoadDataInBackgroundAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow OnLoaded error: {ex}");
            _viewModel.IsLoading = false;
            _viewModel.StatusMessage = "Initialization failed";
            MessageBox.Show($"Failed to initialize application:\n\n{ex.Message}", "Initialization Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles clicks on the download overlay to close the panel if clicking outside the panel.
    /// </summary>
    private void DownloadOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only close if clicking on the overlay (not the panel itself)
        if (e.Source is Grid && sender is Grid)
        {
            _viewModel.ToggleDownloadPanelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async Task LoadDataInBackgroundAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Background data loading started");

            // Small delay to ensure UI is fully rendered
            await Task.Delay(100);

            // Update status on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                _viewModel.StatusMessage = "Loading WSL instances...";
            });

            // Load WSL instances with timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                // Use Refresh instead of LoadInstances to automatically load disk sizes
                await _viewModel.RefreshCommand.ExecuteAsync(null);

                // Success - hide loading overlay on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    _viewModel.IsLoading = false;
                    var instanceCount = _viewModel.Instances?.Count ?? 0;
                    _viewModel.StatusMessage = instanceCount > 0 
                        ? $"Loaded {instanceCount} WSL instance(s)" 
                        : "Ready";
                });

                System.Diagnostics.Debug.WriteLine("Background data loading completed successfully");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Background loading canceled due to timeout");
                await Dispatcher.InvokeAsync(() =>
                {
                    _viewModel.IsLoading = false;
                    _viewModel.StatusMessage = "Loading timed out - some data may be unavailable";
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Background loading error: {ex}");

            // Always clear loading state on error
            await Dispatcher.InvokeAsync(() =>
            {
                _viewModel.IsLoading = false;
                _viewModel.StatusMessage = "Error loading data";

                // Show error to user (non-blocking)
                MessageBox.Show($"Failed to load WSL instances:\n\n{ex.Message}", "Load Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            });
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


