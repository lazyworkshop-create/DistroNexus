using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

public partial class TemplatesViewModel : ObservableObject
{
    private readonly ITemplateService _templateService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<TemplatesViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private List<Template> _allTemplates = new();

    [ObservableProperty]
    private ObservableCollection<Template> _templates = new();

    [ObservableProperty]
    private string _searchQuery = "";

    partial void OnSearchQueryChanged(string value)
    {
        FilterTemplates();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTemplateCommand))]
    private Template? _selectedTemplate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    public TemplatesViewModel(
        ITemplateService templateService,
        INavigationService navigationService,
        ILogger<TemplatesViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadTemplatesAsync();
    }

    [RelayCommand]
    private async Task LoadTemplatesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading templates...";
            
            var templates = await _templateService.LoadTemplatesAsync(true);
            _allTemplates = templates;
            FilterTemplates();
            
            var path = _templateService.GetTemplateScriptsPath();
            StatusMessage = $"Ready ({_allTemplates.Count} templates). Src: {path}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading templates");
            StatusMessage = "Error loading templates";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void FilterTemplates()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Templates = new ObservableCollection<Template>(_allTemplates);
        }
        else
        {
            var filtered = _allTemplates.Where(t => 
                t.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || 
                t.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            Templates = new ObservableCollection<Template>(filtered);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
        {
             mainVm.IsOnDashboard = true;
        }
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import Template"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Importing template...";
                var imported = await _templateService.ImportTemplateAsync(openFileDialog.FileName);
                if (imported != null)
                {
                    await LoadTemplatesAsync();
                    StatusMessage = "Template imported successfully";
                }
                else
                {
                    MessageBox.Show("Failed to import template. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing template");
                 MessageBox.Show($"Error importing template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task ExportTemplateAsync(Template? template)
    {
         var target = template ?? SelectedTemplate;
         if (target == null) return;

        var saveFileDialog = new SaveFileDialog
        {
            FileName = $"{target.Id}.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Export Template"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                 StatusMessage = "Exporting template...";
                await _templateService.ExportTemplateAsync(target.Id, saveFileDialog.FileName);
                StatusMessage = "Template exported successfully";
            }
             catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting template");
                 MessageBox.Show($"Error exporting template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemoveTemplateAsync(Template? template)
    {
        var target = template ?? SelectedTemplate;
        if (target == null || !target.IsCustom) return;
        
        if (MessageBox.Show($"Are you sure you want to delete template '{target.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
             try
            {
                IsLoading = true;
                await _templateService.RemoveCustomTemplateAsync(target.Id);
                await LoadTemplatesAsync();
                if (SelectedTemplate == target) SelectedTemplate = null;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error removing template");
                 MessageBox.Show($"Error removing template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
    
    [RelayCommand]
    private void InstallNewInstance(Template? template)
    {
        if (template == null) return;

        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
        {
             mainVm.ShowInstallWizardCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void ApplyToInstance(Template? template)
    {
        if (template == null) return;
        MessageBox.Show("Applying template to existing instance is coming soon.", "Feature Not Implemented");
    }

    private bool CanReferenceTemplate() => SelectedTemplate != null;
    private bool CanRemoveTemplate() => SelectedTemplate != null && SelectedTemplate.IsCustom;
}
