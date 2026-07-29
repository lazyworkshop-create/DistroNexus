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
using DistroNexus.Desktop.Views;
using DistroNexus.Desktop.Wizard;
using Microsoft.Extensions.DependencyInjection;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the package manager page.
/// </summary>
public partial class PackageManagerViewModel : ObservableObject
{
    private readonly IPowerShellModuleClient _moduleClient;
    private readonly ILogger<PackageManagerViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    

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
    private string _statusMessage = Properties.Resources.StatusReady;

    [ObservableProperty]
    private bool _isOfflineMode;

    public PackageManagerViewModel(
        IPowerShellModuleClient moduleClient,
        ILogger<PackageManagerViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    private async Task ShowAlert(string title, string message)
    {
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            MaxWidth = 400
        };

        await uiMessageBox.ShowDialogAsync();
    }

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = Properties.Resources.StatusLoadingCatalog;

            _logger.LogInformation("Loading distribution catalog");

            var packages = await _moduleClient.GetPackagesAsync();
            
            Packages.Clear();
            FilteredPackages.Clear();
            
            foreach (var package in packages)
            {
                Packages.Add(package);
                FilteredPackages.Add(package);
            }

            UpdateGroupedPackages();

            StatusMessage = string.Format(Properties.Resources.StatusLoadedDistros, Packages.Count);
            _logger.LogInformation("Loaded {Count} distributions", Packages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog");
            StatusMessage = string.Format(Properties.Resources.ErrorLoadingCatalogShort, ex.Message);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.LoadCatalogError, MainViewModel.FormatAlertMessage(ex)));
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
            StatusMessage = Properties.Resources.StatusRefreshingCatalog;

            _logger.LogInformation("Refreshing catalog");

            await _moduleClient.RefreshCatalogAsync();
            await LoadCatalogAsync();

            StatusMessage = Properties.Resources.StatusCatalogRefreshed;
            await ShowAlert(Properties.Resources.Success, Properties.Resources.StatusCatalogRefreshed);
        }
        catch (HttpRequestException ex)
        {
            // Network error - switch to offline mode
            _logger.LogWarning(ex, "Network error - switching to offline mode");
            IsOfflineMode = true;
            StatusMessage = Properties.Resources.StatusOfflineCached;
            
            // Try to load from cache
            await LoadCatalogAsync();
            
            await ShowAlert(Properties.Resources.OfflineModeTitle, Properties.Resources.ErrorOfflineModeMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh catalog");
            IsOfflineMode = true;
            StatusMessage = string.Format(Properties.Resources.StatusOfflineError, ex.Message);
            
            // Try to load from cache anyway
            await LoadCatalogAsync();
            
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorRefreshCatalogFailed, MainViewModel.FormatAlertMessage(ex)));
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

            var results = await _moduleClient.SearchPackagesAsync(SearchQuery);
            
            FilteredPackages.Clear();
            foreach (var package in results)
            {
                FilteredPackages.Add(package);
            }

            UpdateGroupedPackages();

            StatusMessage = string.Format(Properties.Resources.StatusFoundDistros, FilteredPackages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorSearchFailed, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task DownloadPackageAsync(DistroPackage package)
    {
        if (package == null)
            return;

        try
        {
            var preview = await _moduleClient.PreviewPackageDownloadJobStartAsync(package.Id);
            if (string.IsNullOrWhiteSpace(preview.PreviewToken))
                throw new InvalidOperationException(preview.OutcomeCode);
            var result = await _moduleClient.StartPackageDownloadJobAsync(preview.PreviewToken);
            if (string.IsNullOrWhiteSpace(result.JobId))
                throw new InvalidOperationException(result.OutcomeCode);

            StatusMessage = string.Format(Properties.Resources.StatusDownloadQueued, package.Name);
            _logger.LogInformation("Download queued successfully for {PackageName}", package.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue download");
            StatusMessage = Properties.Resources.StatusQueueFailed;
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorQueueDownload, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task CancelDownload(DistroPackage package)
    {
        if (package == null)
            return;

        try
        {
            var job = (await _moduleClient.GetPackageDownloadJobsAsync()).FirstOrDefault(x => x.PackageId == package.Id && x.State is "Queued" or "Running");
            if (job is null) return;
            var preview = await _moduleClient.PreviewPackageDownloadJobActionAsync(job.JobId, "cancel");
            if (string.IsNullOrWhiteSpace(preview.PreviewToken)) throw new InvalidOperationException(preview.OutcomeCode);
            await _moduleClient.ExecutePackageDownloadJobActionAsync(preview.PreviewToken);
            StatusMessage = string.Format(Properties.Resources.StatusDownloadCancelled, package.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel download for {PackageName}", package.Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorCancelDownload, MainViewModel.FormatAlertMessage(ex)));
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
            var refreshedPackages = await _moduleClient.GetPackagesAsync(forceReload: true);
            
            // Update existing package objects with new cache status
            foreach (var pkg in Packages)
            {
                var refreshed = refreshedPackages.FirstOrDefault(p => p.Id == pkg.Id);
                if (refreshed != null)
                {
                    pkg.IsCached = refreshed.IsCached;
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
            var mergedPackages = MergeSameFilePackages(group);

            GroupedPackages.Add(new PackageGroup
            {
                Category = group.Key,
                Packages = new ObservableCollection<DistroPackage>(mergedPackages)
            });
        }
    }

    private List<DistroPackage> MergeSameFilePackages(IEnumerable<DistroPackage> packages)
    {
        return packages
            .GroupBy(GetSameFileKey)
            .Select(packageGroup =>
            {
                var candidates = packageGroup.ToList();
                var representative = candidates.FirstOrDefault(p => p.IsDownloading)
                    ?? candidates.FirstOrDefault(p => p.IsCached)
                    ?? candidates[0];

                if (candidates.Count > 1)
                {
                    representative.IsSameFileMerged = true;
                    representative.SameFileTagText = BuildSameFileTagText(candidates, representative);
                }
                else
                {
                    representative.IsSameFileMerged = false;
                    representative.SameFileTagText = string.Empty;
                }

                return representative;
            })
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Version)
            .ToList();
    }

    private static string BuildSameFileTagText(IReadOnlyCollection<DistroPackage> candidates, DistroPackage representative)
    {
        var otherDistroNames = candidates
            .Where(p => !string.Equals(p.Id, representative.Id, StringComparison.OrdinalIgnoreCase))
            .Select(GetPackageDisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (otherDistroNames.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", otherDistroNames);
    }

    private static string GetPackageDisplayName(DistroPackage package)
    {
        if (!string.IsNullOrWhiteSpace(package.Version)
            && !package.Name.Contains(package.Version, StringComparison.OrdinalIgnoreCase))
        {
            return $"{package.Name} {package.Version}";
        }

        return package.Name;
    }

    private static string GetSameFileKey(DistroPackage package)
    {
        if (!string.IsNullOrWhiteSpace(package.Sha256))
        {
            return $"sha256:{package.Sha256.Trim().ToLowerInvariant()}";
        }

        return $"id:{package.Id}";
    }

    [RelayCommand]
    private async Task DeletePackageAsync(DistroPackage package)
    {
        if (package == null)
            return;

        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            "Confirm Delete",
            $"Are you sure you want to delete the cached package '{package.Name}'?",
            "Delete");

        if (!confirmed)
            return;

        try
        {
            _logger.LogInformation("Deleting cached package {PackageName}", package.Name);
            
            await DeletePackageCacheEntryAsync(package);

            // Refresh all package cache states so same-file merged variants stay consistent.
            await RefreshCatalogCacheStatusAsync();

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
            
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorDeletePackage, errorMessage));
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

    private async Task DeletePackageCacheEntryAsync(DistroPackage package)
    {
        var usage = await _moduleClient.GetPackageCacheUsageAsync();
        var entry = usage.CachedPackages.SingleOrDefault(item =>
            string.Equals(item.PackageId, package.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, package.Name, StringComparison.OrdinalIgnoreCase));
        if (entry is null || string.IsNullOrWhiteSpace(entry.CacheEntryId))
            throw new InvalidOperationException("PackageCache.EntryInvalid");
        await _moduleClient.DeletePackageCacheEntryAsync(entry.CacheEntryId);
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
                await DeletePackageCacheEntryAsync(package);
                package.IsCached = false;
            }

            // Download the package
            await DownloadPackageAsync(package);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to redownload package");
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorRedownloadPackage, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task AddCustomSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomSourceUrl))
        {
            await ShowAlert(Properties.Resources.TitleInvalidUrl, Properties.Resources.ErrorInvalidUrl);
            return;
        }

        try
        {
            _logger.LogInformation("Adding custom source: {Url}", CustomSourceUrl);
            
            // Validate URL format
            if (!Uri.TryCreate(CustomSourceUrl, UriKind.Absolute, out var uri))
            {
                await ShowAlert(Properties.Resources.ErrorApplicationTitle, Properties.Resources.ErrorCustomUrlInvalidFormat);
                return;
            }

            var name = string.IsNullOrWhiteSpace(uri.Host) ? "Custom source" : uri.Host;
            await _moduleClient.AddCatalogSourceAsync(new DistroNexusCatalogSourceCreateRequest(name, uri.AbsoluteUri));
            
            CustomSourceUrl = string.Empty;
            IsAddSourcePanelVisible = false;
            await LoadCatalogAsync();
            
            await ShowAlert(Properties.Resources.TitleSuccess, Properties.Resources.SuccessCustomSourceAdded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add custom source");
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorAddCustomSource, MainViewModel.FormatAlertMessage(ex)));
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
            await _moduleClient.RefreshCatalogAsync();
            await LoadCatalogAsync();
            StatusMessage = "Sources updated successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sources");
            StatusMessage = "Failed to update sources";
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorUpdateSources, MainViewModel.FormatAlertMessage(ex)));
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
                await ShowAlert(Properties.Resources.TitleDownloadAll, Properties.Resources.InfoAllPackagesCached);
                return;
            }

            // Show confirmation dialog with package list
            var packageList = string.Join("\n", packagesToDownload.Select((p, i) => $"{i + 1}. {p.Name} ({p.Version})"));
            var message = $"The following {packagesToDownload.Count} package(s) will be downloaded:\n\n{packageList}\n\nDo you want to continue?";
            
            var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
                "Confirm Download All",
                message,
                "Download");

            if (!confirmed)
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
                    await DownloadPackageAsync(package);
                    successCount++;
                    StatusMessage = $"Queued {successCount} of {packagesToDownload.Count} packages for download...";
                }
                catch (Exception ex)
                {
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
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorStartDownloadAll, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    /// <summary>
    /// Installs a cached package.
    /// </summary>
    [RelayCommand]
    private async Task InstallCachedPackage(DistroPackage package)
    {
        if (package == null) return;
        
        _logger.LogInformation("Installing cached package: {PackageId}", package.Id);
        
        try
        {
            StatusMessage = $"Starting installation of {package.Name}...";
            
            // Create the wizard window via DI
            var wizardWindow = _serviceProvider.GetRequiredService<InstallWizardDialogNew>();
            wizardWindow.Owner = Application.Current.MainWindow;
            
            // Pre-select the distribution in the workflow context
            if (wizardWindow.DataContext is InstallWizardWorkflowViewModel wizardVm)
            {
                wizardVm.SetStartupRequest(new InstallWizardStartupRequest
                {
                    SelectedDistributionId = package.Id
                });
            }
            
            _logger.LogInformation("Opening install wizard for package: {PackageId}", package.Id);
            
            // Show the wizard as a modal dialog
            var result = wizardWindow.ShowDialog();
            
            if (result == true)
            {
                StatusMessage = $"Installation completed for {package.Name}";
                _logger.LogInformation("Installation completed for package: {PackageId}", package.Id);
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
            await ShowAlert(Properties.Resources.ErrorApplicationTitle, string.Format(Properties.Resources.ErrorInstallPackage, package.Name, MainViewModel.FormatAlertMessage(ex)));
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
