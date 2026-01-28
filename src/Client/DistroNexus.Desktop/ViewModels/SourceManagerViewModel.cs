using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the catalog source manager page.
/// </summary>
public partial class SourceManagerViewModel : ObservableObject
{
    private readonly ICatalogSourceManager _sourceManager;
    private readonly ILogger<SourceManagerViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<CatalogSource> _sources = new();

    [ObservableProperty]
    private CatalogSource? _selectedSource;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _newSourceName = string.Empty;

    [ObservableProperty]
    private string _newSourceUrl = string.Empty;

    [ObservableProperty]
    private string _newSourceDescription = string.Empty;

    [ObservableProperty]
    private bool _isNewSourceDialogOpen;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    private bool _isTestingSource;

    public SourceManagerViewModel(
        ICatalogSourceManager sourceManager,
        ILogger<SourceManagerViewModel> logger)
    {
        _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading sources...";

            _logger.LogInformation("Initializing source manager");

            var sources = await _sourceManager.GetSourcesAsync();
            
            Sources.Clear();
            foreach (var source in sources.OrderBy(s => s.Priority))
            {
                Sources.Add(source);
            }

            StatusMessage = $"Loaded {Sources.Count} source(s)";
            _logger.LogInformation("Loaded {Count} sources", Sources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize source manager");
            StatusMessage = "Failed to load sources";
            MessageBox.Show($"Failed to load sources: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowAddSourceDialog()
    {
        IsEditing = false;
        NewSourceName = string.Empty;
        NewSourceUrl = string.Empty;
        NewSourceDescription = string.Empty;
        TestResult = string.Empty;
        IsNewSourceDialogOpen = true;
        
        _logger.LogInformation("Opened add source dialog");
    }

    [RelayCommand]
    private void ShowEditSourceDialog()
    {
        if (SelectedSource == null)
        {
            MessageBox.Show("Please select a source to edit.", 
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsEditing = true;
        NewSourceName = SelectedSource.Name;
        NewSourceUrl = SelectedSource.Url;
        NewSourceDescription = SelectedSource.Description;
        TestResult = string.Empty;
        IsNewSourceDialogOpen = true;
        
        _logger.LogInformation("Opened edit source dialog for: {SourceId}", SelectedSource.Id);
    }

    [RelayCommand]
    private async Task SaveSourceAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NewSourceName) || string.IsNullOrWhiteSpace(NewSourceUrl))
            {
                MessageBox.Show("Name and URL are required.", 
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsEditing && SelectedSource != null)
            {
                var updatedSource = new CatalogSource
                {
                    Id = SelectedSource.Id,
                    Name = NewSourceName,
                    Url = NewSourceUrl,
                    Description = NewSourceDescription,
                    IsActive = SelectedSource.IsActive,
                    Priority = SelectedSource.Priority
                };

                await _sourceManager.UpdateSourceAsync(updatedSource);
                await RefreshSourcesAsync();
                StatusMessage = "Source updated successfully";
            }
            else
            {
                var newSource = new CatalogSource
                {
                    Name = NewSourceName,
                    Url = NewSourceUrl,
                    Description = NewSourceDescription,
                    IsActive = true
                };

                await _sourceManager.AddSourceAsync(newSource);
                await RefreshSourcesAsync();
                StatusMessage = "Source added successfully";
            }

            IsNewSourceDialogOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save source");
            MessageBox.Show($"Failed to save source: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CloseSourceDialog()
    {
        IsNewSourceDialogOpen = false;
    }

    [RelayCommand]
    private async Task RemoveSourceAsync()
    {
        if (SelectedSource == null)
        {
            MessageBox.Show("Please select a source to remove.", 
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SelectedSource.IsDefault)
        {
            MessageBox.Show("Default sources cannot be removed.", 
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to remove the source '{SelectedSource.Name}'?",
            "Confirm Remove",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _sourceManager.RemoveSourceAsync(SelectedSource.Id);
                await RefreshSourcesAsync();
                StatusMessage = "Source removed successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove source");
                MessageBox.Show($"Failed to remove source: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ToggleSourceActiveAsync()
    {
        if (SelectedSource == null)
            return;

        try
        {
            var newActiveState = !SelectedSource.IsActive;
            await _sourceManager.SetSourceActiveAsync(SelectedSource.Id, newActiveState);
            
            SelectedSource.IsActive = newActiveState;
            StatusMessage = $"Source {(newActiveState ? "enabled" : "disabled")}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle source active state");
            MessageBox.Show($"Failed to toggle source: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task TestSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSourceUrl))
        {
            TestResult = "URL is required";
            return;
        }

        try
        {
            IsTestingSource = true;
            TestResult = "Testing...";

            var isAccessible = await _sourceManager.TestSourceAsync(NewSourceUrl);
            
            TestResult = isAccessible ? "✓ Source is accessible" : "✗ Source is not accessible";
            
            _logger.LogInformation("Source test result: {Url} -> {Success}", NewSourceUrl, isAccessible);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test source");
            TestResult = $"✗ Test failed: {ex.Message}";
        }
        finally
        {
            IsTestingSource = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSourcesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Refreshing sources...";

            var sources = await _sourceManager.GetSourcesAsync();
            
            Sources.Clear();
            foreach (var source in sources.OrderBy(s => s.Priority))
            {
                Sources.Add(source);
            }

            StatusMessage = $"Refreshed {Sources.Count} source(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh sources");
            StatusMessage = "Failed to refresh sources";
            MessageBox.Show($"Failed to refresh sources: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset to default sources? This will remove all custom sources.",
            "Confirm Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _sourceManager.ResetToDefaultsAsync();
                await RefreshSourcesAsync();
                StatusMessage = "Reset to default sources successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset sources");
                MessageBox.Show($"Failed to reset sources: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task MoveSourceUpAsync()
    {
        if (SelectedSource == null || Sources.Count <= 1)
            return;

        try
        {
            var currentIndex = Sources.IndexOf(SelectedSource);
            if (currentIndex <= 0)
                return;

            // Move up in the collection
            Sources.RemoveAt(currentIndex);
            Sources.Insert(currentIndex - 1, SelectedSource);

            // Update priorities and save
            var sourceIds = Sources.Select(s => s.Id).ToList();
            await _sourceManager.ReorderSourcesAsync(sourceIds);

            StatusMessage = "Source moved up";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move source up");
            MessageBox.Show($"Failed to move source: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task MoveSourceDownAsync()
    {
        if (SelectedSource == null || Sources.Count <= 1)
            return;

        try
        {
            var currentIndex = Sources.IndexOf(SelectedSource);
            if (currentIndex >= Sources.Count - 1)
                return;

            // Move down in the collection
            Sources.RemoveAt(currentIndex);
            Sources.Insert(currentIndex + 1, SelectedSource);

            // Update priorities and save
            var sourceIds = Sources.Select(s => s.Id).ToList();
            await _sourceManager.ReorderSourcesAsync(sourceIds);

            StatusMessage = "Source moved down";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move source down");
            MessageBox.Show($"Failed to move source: {ex.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}