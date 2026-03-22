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
    private string _statusMessage = Properties.Resources.StatusReady;

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
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = Properties.Resources.StatusLoadingSources;

            _logger.LogInformation("Initializing source manager");

            var sources = await _sourceManager.GetSourcesAsync();
            
            Sources.Clear();
            foreach (var source in sources.OrderBy(s => s.Priority))
            {
                Sources.Add(source);
            }

            StatusMessage = string.Format(Properties.Resources.StatusLoadedSources, Sources.Count);
            _logger.LogInformation("Loaded {Count} sources", Sources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize source manager");
            StatusMessage = Properties.Resources.StatusLoadSourcesFailed;
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorLoadSourcesEx, MainViewModel.FormatAlertMessage(ex)));
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
    private async Task ShowEditSourceDialog()
    {
        if (SelectedSource == null)
        {
            await ShowAlert(Properties.Resources.InformationTitle, Properties.Resources.InfoSelectSourceEdit);
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
                await ShowAlert(Properties.Resources.ValidationTitle, Properties.Resources.ValidationNameUrlRequired);
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
                StatusMessage = Properties.Resources.StatusSourceUpdated;
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
                StatusMessage = Properties.Resources.StatusSourceAdded;
            }

            IsNewSourceDialogOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save source");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorSaveSourceEx, MainViewModel.FormatAlertMessage(ex)));
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
            await ShowAlert(Properties.Resources.InformationTitle, Properties.Resources.InfoSelectSourceRemove);
            return;
        }

        if (SelectedSource.IsDefault)
        {
            await ShowAlert(Properties.Resources.InformationTitle, Properties.Resources.InfoDefaultSourceRemove);
            return;
        }

        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.ConfirmRemoveSourceTitle,
            string.Format(Properties.Resources.ConfirmVerifyRemoveSource, SelectedSource.Name),
            "Remove"); // Should ideally be a resource like ButtonRemove

        if (confirmed)
        {
            try
            {
                await _sourceManager.RemoveSourceAsync(SelectedSource.Id);
                await RefreshSourcesAsync();
                StatusMessage = Properties.Resources.StatusSourceRemoved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove source");
                await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorRemoveSourceEx, MainViewModel.FormatAlertMessage(ex)));
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
            StatusMessage = string.Format(Properties.Resources.StatusSourceToggled, newActiveState ? Properties.Resources.StatusSourceEnabled : Properties.Resources.StatusSourceDisabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle source active state");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorToggleSourceEx, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task TestSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSourceUrl))
        {
            TestResult = Properties.Resources.TestResultUrlRequired;
            return;
        }

        try
        {
            IsTestingSource = true;
            TestResult = Properties.Resources.TestResultTesting;

            var isAccessible = await _sourceManager.TestSourceAsync(NewSourceUrl);
            
            TestResult = isAccessible ? Properties.Resources.TestResultAccessible : Properties.Resources.TestResultNotAccessible;
            
            _logger.LogInformation("Source test result: {Url} -> {Success}", NewSourceUrl, isAccessible);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test source");
            TestResult = string.Format(Properties.Resources.TestResultFailed, ex.Message);
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
            StatusMessage = Properties.Resources.StatusRefreshingSources;

            var sources = await _sourceManager.GetSourcesAsync();
            
            Sources.Clear();
            foreach (var source in sources.OrderBy(s => s.Priority))
            {
                Sources.Add(source);
            }

            StatusMessage = string.Format(Properties.Resources.StatusRefreshedSources, Sources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh sources");
            StatusMessage = Properties.Resources.StatusRefreshFailed;
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorRefreshSourcesEx, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.ConfirmResetTitle,
            Properties.Resources.ConfirmResetSourcesMessage,
            "Reset");

        if (confirmed)
        {
            try
            {
                await _sourceManager.ResetToDefaultsAsync();
                await RefreshSourcesAsync();
                StatusMessage = Properties.Resources.StatusResetSourcesSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset sources");
                await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorResetSourcesEx, MainViewModel.FormatAlertMessage(ex)));
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

            StatusMessage = Properties.Resources.StatusSourceMovedUp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move source up");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorMoveSource, MainViewModel.FormatAlertMessage(ex)));
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

            StatusMessage = Properties.Resources.StatusSourceMovedDown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move source down");
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorMoveSource, MainViewModel.FormatAlertMessage(ex)));
        }
    }
}