using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the package manager page.
/// </summary>
public partial class PackageManagerViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IDownloadService _downloadService;
    private readonly ILogger<PackageManagerViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<DistroPackage> _packages = new();

    [ObservableProperty]
    private ObservableCollection<DistroPackage> _filteredPackages = new();

    [ObservableProperty]
    private DistroPackage? _selectedPackage;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isOfflineMode;

    public PackageManagerViewModel(
        ICatalogService catalogService,
        IDownloadService downloadService,
        ILogger<PackageManagerViewModel> logger)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading distribution catalog...";

            _logger.LogInformation("Loading distribution catalog");

            var packages = await _catalogService.LoadCatalogAsync();
            
            Packages.Clear();
            FilteredPackages.Clear();
            
            foreach (var package in packages)
            {
                Packages.Add(package);
                FilteredPackages.Add(package);
            }

            UpdateGroupedPackages();

            StatusMessage = $"Loaded {Packages.Count} distribution(s)";
            _logger.LogInformation("Loaded {Count} distributions", Packages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog");
            StatusMessage = $"Error loading catalog: {ex.Message}";
            MessageBox.Show($"Failed to load catalog: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshCatalogAsync()
    {
        try
        {
            IsLoading = true;
            IsOfflineMode = false;
            StatusMessage = "Refreshing catalog from remote source...";

            _logger.LogInformation("Refreshing catalog");

            await _catalogService.RefreshCatalogAsync();
            await LoadCatalogAsync();

            StatusMessage = "Catalog refreshed successfully";
            MessageBox.Show("Catalog refreshed successfully", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (HttpRequestException ex)
        {
            // Network error - switch to offline mode
            _logger.LogWarning(ex, "Network error - switching to offline mode");
            IsOfflineMode = true;
            StatusMessage = "Offline Mode - Using cached catalog";
            
            // Try to load from cache
            await LoadCatalogAsync();
            
            MessageBox.Show("Unable to connect to remote catalog. Using cached data.\n\nWorking in Offline Mode.", 
                "Offline Mode", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh catalog");
            IsOfflineMode = true;
            StatusMessage = "Offline Mode - " + ex.Message;
            
            // Try to load from cache anyway
            await LoadCatalogAsync();
            
            MessageBox.Show($"Failed to refresh catalog: {ex.Message}\n\nUsing cached data.", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            _logger.LogInformation("Searching for '{Query}'", SearchQuery);

            var results = await _catalogService.SearchDistributionsAsync(SearchQuery);
            
            FilteredPackages.Clear();
            foreach (var package in results)
            {
                FilteredPackages.Add(package);
            }

            UpdateGroupedPackages();

            StatusMessage = $"Found {FilteredPackages.Count} matching distribution(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            MessageBox.Show($"Search failed: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DownloadPackageAsync(DistroPackage package)
    {
        if (package == null)
            return;

        try
        {
            _logger.LogInformation("Downloading package {PackageName}", package.Name);

            var downloadsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "Downloads", 
                "DistroNexus");

            if (!System.IO.Directory.Exists(downloadsPath))
                System.IO.Directory.CreateDirectory(downloadsPath);

            var fileName = System.IO.Path.GetFileName(new Uri(package.DownloadUrl).LocalPath);
            var destination = System.IO.Path.Combine(downloadsPath, fileName);

            var progress = new Progress<double>(percent =>
            {
                StatusMessage = $"Downloading {package.Name}: {percent:F1}%";
            });

            var success = await _downloadService.DownloadFileAsync(
                package.DownloadUrl, 
                destination, 
                progress);

            if (success)
            {
                StatusMessage = $"Downloaded {package.Name} successfully";
                MessageBox.Show($"Package downloaded to:\n{destination}", 
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Download failed";
                MessageBox.Show("Download failed. Please try again.", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download package");
            MessageBox.Show($"Failed to download package: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredPackages.Clear();
            foreach (var package in Packages)
            {
                FilteredPackages.Add(package);
            }
            UpdateGroupedPackages();
        }
        else
        {
            // Trigger search when user stops typing
            _ = SearchAsync();
        }
    }

    [ObservableProperty]
    private ObservableCollection<PackageGroup> _groupedPackages = new();

    [ObservableProperty]
    private string _customSourceUrl = string.Empty;

    [ObservableProperty]
    private bool _isAddSourcePanelVisible;

    /// <summary>
    /// Toggles the visibility of the Add Source panel.
    /// </summary>
    [RelayCommand]
    private void ToggleAddSourcePanel()
    {
        IsAddSourcePanelVisible = !IsAddSourcePanelVisible;
        if (!IsAddSourcePanelVisible)
        {
            CustomSourceUrl = string.Empty;
        }
    }

    /// <summary>
    /// Updates the grouped packages collection based on category.
    /// </summary>
    private void UpdateGroupedPackages()
    {
        GroupedPackages.Clear();
        
        var groups = FilteredPackages
            .GroupBy(p => string.IsNullOrEmpty(p.Category) ? "Other" : p.Category)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            GroupedPackages.Add(new PackageGroup
            {
                Category = group.Key,
                Packages = new ObservableCollection<DistroPackage>(group.ToList())
            });
        }
    }

    [RelayCommand]
    private async Task DeletePackageAsync(DistroPackage package)
    {
        if (package == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the cached package '{package.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Deleting cached package {PackageName}", package.Name);
            
            await _catalogService.DeleteCachedPackageAsync(package.Id);
            
            package.IsCached = false;
            StatusMessage = $"Deleted cached package: {package.Name}";
            
            // Refresh the grouped packages to update UI
            UpdateGroupedPackages();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cached package");
            MessageBox.Show($"Failed to delete package: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Re-downloads a package, replacing any existing cached version.
    /// </summary>
    [RelayCommand]
    private async Task RedownloadPackageAsync(DistroPackage package)
    {
        if (package == null)
            return;

        try
        {
            _logger.LogInformation("Re-downloading package {PackageName}", package.Name);

            // Delete existing cache first if present
            if (package.IsCached)
            {
                await _catalogService.DeleteCachedPackageAsync(package.Id);
                package.IsCached = false;
            }

            // Download the package
            await DownloadPackageAsync(package);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to redownload package");
            MessageBox.Show($"Failed to redownload package: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task AddCustomSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomSourceUrl))
        {
            MessageBox.Show("Please enter a valid URL", "Invalid URL", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _logger.LogInformation("Adding custom source: {Url}", CustomSourceUrl);
            
            // Validate URL format
            if (!Uri.TryCreate(CustomSourceUrl, UriKind.Absolute, out var uri))
            {
                MessageBox.Show("Invalid URL format", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await _catalogService.AddCustomSourceAsync(CustomSourceUrl);
            
            CustomSourceUrl = string.Empty;
            IsAddSourcePanelVisible = false;
            await LoadCatalogAsync();
            
            MessageBox.Show("Custom source added successfully", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add custom source");
            MessageBox.Show($"Failed to add custom source: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Navigates back to the dashboard.
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        _logger.LogInformation("Navigating back from package manager");
        
        // Get the MainViewModel from the application's main window
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.ShowDashboardCommand.Execute(null);
        }
    }
}

/// <summary>
/// Represents a group of packages by category.
/// </summary>
public class PackageGroup
{
    public string Category { get; set; } = string.Empty;
    public ObservableCollection<DistroPackage> Packages { get; set; } = new();
}
