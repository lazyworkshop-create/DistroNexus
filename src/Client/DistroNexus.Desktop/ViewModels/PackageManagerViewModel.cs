using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly ILogger<PackageManagerViewModel> _logger;
    
    // Track active downloads: PackageId -> DownloadTask
    private readonly Dictionary<string, DownloadTask> _activeDownloads = new();

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
        IDownloadTaskManager downloadTaskManager,
        ILogger<PackageManagerViewModel> logger)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _downloadTaskManager = downloadTaskManager ?? throw new ArgumentNullException(nameof(downloadTaskManager));
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
            _logger.LogInformation("Queuing download for package {PackageName}", package.Name);

            // Set downloading state
            package.IsDownloading = true;

            // Use the configured cache path from catalog service
            var cachePath = _catalogService.GetPackageCachePath();
            var fileName = !string.IsNullOrWhiteSpace(package.DownloadUrl)
                ? Path.GetFileName(new Uri(package.DownloadUrl).LocalPath)
                : $"{package.Id}.tar.gz";
            
            var destinationPath = Path.Combine(cachePath, fileName);
            
            _logger.LogInformation("Downloading {PackageName} to cache path: {Path}", package.Name, destinationPath);
            
            var downloadTask = _downloadTaskManager.AddTask(package, destinationPath);
            
            // Track the download
            lock (_activeDownloads)
            {
                _activeDownloads[package.Id] = downloadTask;
            }
            
            // Subscribe to status changes to update UI
            _ = MonitorDownloadTaskAsync(downloadTask, package);

            StatusMessage = $"Download queued: {package.Name}";
            _logger.LogInformation("Download queued successfully for {PackageName}", package.Name);
        }
        catch (Exception ex)
        {
            package.IsDownloading = false;
            _logger.LogError(ex, "Failed to queue download");
            StatusMessage = "Failed to queue download";
            MessageBox.Show($"Failed to queue download: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Cancels an active download for a package.
    /// </summary>
    [RelayCommand]
    private void CancelDownload(DistroPackage package)
    {
        if (package == null)
            return;

        try
        {
            lock (_activeDownloads)
            {
                if (_activeDownloads.TryGetValue(package.Id, out var downloadTask))
                {
                    _logger.LogInformation("Cancelling download for {PackageName}", package.Name);
                    downloadTask.CancellationTokenSource?.Cancel();
                    _activeDownloads.Remove(package.Id);
                    package.IsDownloading = false;
                    StatusMessage = $"Download cancelled: {package.Name}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel download for {PackageName}", package.Name);
            MessageBox.Show($"Failed to cancel download: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Monitors a download task and updates the package status.
    /// </summary>
    private async Task MonitorDownloadTaskAsync(DownloadTask downloadTask, DistroPackage package)
    {
        try
        {
            // Poll the task status
            while (downloadTask.Status == DownloadStatus.Pending || downloadTask.Status == DownloadStatus.Downloading)
            {
                await Task.Delay(500);
            }

            // Task completed or failed
            switch (downloadTask.Status)
            {
                case DownloadStatus.Completed:
                    package.IsDownloading = false;
                    package.IsCached = true;
                    package.LocalPath = downloadTask.DestinationPath;
                    _logger.LogInformation("Download completed for {PackageName} to {Path}", package.Name, downloadTask.DestinationPath);
                    
                    // Refresh catalog to update cache status for all packages
                    // This ensures other packages with the same URL also show as cached
                    await RefreshCatalogCacheStatusAsync();
                    break;
                    
                case DownloadStatus.Failed:
                    package.IsDownloading = false;
                    _logger.LogWarning("Download failed for {PackageName}: {Error}", package.Name, downloadTask.ErrorMessage);
                    break;
                    
                case DownloadStatus.Cancelled:
                    package.IsDownloading = false;
                    _logger.LogInformation("Download cancelled for {PackageName}", package.Name);
                    break;
            }

            // Remove from active downloads
            lock (_activeDownloads)
            {
                _activeDownloads.Remove(package.Id);
            }

            // Update UI grouping
            UpdateGroupedPackages();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring download task for {PackageName}", package.Name);
            package.IsDownloading = false;
        }
    }

    /// <summary>
    /// Refreshes cache status for all packages after a download completes.
    /// </summary>
    private async Task RefreshCatalogCacheStatusAsync()
    {
        try
        {
            // Reload catalog to get updated cache status and file sizes
            var refreshedPackages = await _catalogService.LoadCatalogAsync(forceReload: true);
            
            // Update existing package objects with new cache status
            foreach (var pkg in Packages)
            {
                var refreshed = refreshedPackages.FirstOrDefault(p => p.Id == pkg.Id);
                if (refreshed != null)
                {
                    pkg.IsCached = refreshed.IsCached;
                    pkg.LocalPath = refreshed.LocalPath;
                    pkg.FileSize = refreshed.FileSize;
                }
            }
            
            _logger.LogInformation("Refreshed cache status for {Count} packages", Packages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh cache status");
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
            
            // Extract user-friendly error message
            var errorMessage = ExtractUserFriendlyError(ex.Message);
            
            MessageBox.Show($"Failed to delete package: {errorMessage}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Extracts a user-friendly error message from technical error output.
    /// Duplicated from WslManagerService to keep ViewModel independent.
    /// </summary>
    private static string ExtractUserFriendlyError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return "Operation failed with an unknown error.";

        // Remove CLIXML tags
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            errorMessage, 
            @"#< CLIXML.*?<Objs.*?</Objs>", 
            "", 
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // Check for common error patterns
        if (errorMessage.Contains("recognized as a name of a cmdlet", StringComparison.OrdinalIgnoreCase))
        {
            return "The required functionality is missing from the PowerShell module. Please restart the application.";
        }
        
        if (errorMessage.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return "Access denied. Please ensure you have administrator privileges or the file is not in use.";
        }

        if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "The package file could not be found. It may have been already deleted.";
        }

        // Return first valid line of the cleaned error
        var firstLine = cleaned.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLine))
        {
            // Remove ANSI color codes if present
            firstLine = System.Text.RegularExpressions.Regex.Replace(firstLine, @"\x1B\[[^@-~]*[@-~]", "");
            
            if (firstLine.Length < 200)
                return firstLine.Trim();
        }

        return "An unexpected error occurred while deleting the package.";
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

    /// <summary>
    /// Updates the distribution catalog from remote sources.
    /// </summary>
    [RelayCommand]
    private async Task UpdateSourcesAsync()
    {
        _logger.LogInformation("Updating distribution sources");
        
        try
        {
            StatusMessage = "Updating sources...";
            await _catalogService.RefreshCatalogAsync();
            await RefreshCatalogAsync();
            StatusMessage = "Sources updated successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sources");
            StatusMessage = "Failed to update sources";
            MessageBox.Show($"Failed to update sources: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Downloads all uncached packages.
    /// </summary>
    [RelayCommand]
    private async Task DownloadAllAsync()
    {
        try
        {
            _logger.LogInformation("Preparing to download all uncached packages");
            
            // Find all packages that need to be downloaded (not cached and not downloading)
            var packagesToDownload = new List<DistroPackage>();
            
            foreach (var group in GroupedPackages)
            {
                foreach (var package in group.Packages)
                {
                    if (!package.IsCached && !package.IsDownloading)
                    {
                        packagesToDownload.Add(package);
                    }
                }
            }
            
            if (packagesToDownload.Count == 0)
            {
                MessageBox.Show("All packages are already cached or currently downloading.", 
                    "Download All", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Show confirmation dialog with package list
            var packageList = string.Join("\n", packagesToDownload.Select((p, i) => $"{i + 1}. {p.Name} ({p.Version})"));
            var message = $"The following {packagesToDownload.Count} package(s) will be downloaded:\n\n{packageList}\n\nDo you want to continue?";
            
            var result = MessageBox.Show(message, 
                "Confirm Download All", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                _logger.LogInformation("Download all cancelled by user");
                StatusMessage = "Download cancelled by user";
                return;
            }

            // Queue all downloads using the same logic as single download
            _logger.LogInformation("Starting download all: {Count} packages", packagesToDownload.Count);
            StatusMessage = "Starting download of all packages...";
            
            int successCount = 0;
            foreach (var package in packagesToDownload)
            {
                try
                {
                    // Set downloading state
                    package.IsDownloading = true;

                    // Use the configured cache path from catalog service
                    var cachePath = _catalogService.GetPackageCachePath();
                    var fileName = !string.IsNullOrWhiteSpace(package.DownloadUrl)
                        ? Path.GetFileName(new Uri(package.DownloadUrl).LocalPath)
                        : $"{package.Id}.tar.gz";
                    
                    var destinationPath = Path.Combine(cachePath, fileName);
                    
                    _logger.LogInformation("Queuing download for {PackageName} to {Path}", package.Name, destinationPath);
                    
                    var downloadTask = _downloadTaskManager.AddTask(package, destinationPath);
                    
                    // Track the download
                    lock (_activeDownloads)
                    {
                        _activeDownloads[package.Id] = downloadTask;
                    }
                    
                    // Subscribe to status changes to update UI
                    _ = MonitorDownloadTaskAsync(downloadTask, package);
                    
                    successCount++;
                    StatusMessage = $"Queued {successCount} of {packagesToDownload.Count} packages for download...";
                }
                catch (Exception ex)
                {
                    package.IsDownloading = false;
                    _logger.LogError(ex, "Failed to queue download for {PackageName}", package.Name);
                }
            }
            
            StatusMessage = $"Successfully queued {successCount} of {packagesToDownload.Count} packages for download";
            _logger.LogInformation("Download all completed: {SuccessCount}/{TotalCount} queued", successCount, packagesToDownload.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download all packages");
            StatusMessage = "Failed to queue downloads";
            MessageBox.Show($"Failed to start download all: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Installs a cached package.
    /// </summary>
    [RelayCommand]
    private async Task InstallCachedPackageAsync(DistroPackage package)
    {
        if (package == null) return;
        
        _logger.LogInformation("Installing cached package: {PackageId}", package.Id);
        
        try
        {
            StatusMessage = $"Starting installation of {package.Name}...";
            
            // For now, just show a confirmation message as the wizard needs more implementation
            var result = MessageBox.Show(
                $"Install {package.Name} ({package.Id})?\n\nThis will start the installation process.", 
                "Install Package", 
                MessageBoxButton.YesNoCancel, 
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                StatusMessage = $"Installation of {package.Name} started";
                _logger.LogInformation("Installation started for package: {PackageId}", package.Id);
            }
            else
            {
                StatusMessage = $"Installation of {package.Name} cancelled";
                _logger.LogInformation("Installation cancelled for package: {PackageId}", package.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install cached package: {PackageId}", package.Id);
            StatusMessage = $"Failed to install {package.Name}";
            MessageBox.Show($"Failed to install {package.Name}: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
