using System.Collections.ObjectModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step for selecting a development environment template.
/// </summary>
public partial class SelectTemplateStep : WizardStepBase
{
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    public override string StepId => "select-template";
    public override string Title => "Select Template"; 
    public override string Description => "Choose a development environment template (Optional)";

    [ObservableProperty]
    private ObservableCollection<Template> _templates = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public Template? SelectedTemplate
    {
        get => Context?.SelectedTemplate;
        set
        {
            if (Context != null)
            {
                Context.SelectedTemplate = value;
                OnPropertyChanged(nameof(SelectedTemplate));
            }
        }
    }

    public SelectTemplateStep(ITemplateService templateService, ILogger logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override UserControl CreateContent()
    {
        return new SelectTemplateStepView { DataContext = this };
    }

    public override async Task OnEnterAsync()
    {
        if (Templates.Count == 0)
        {
            await LoadTemplatesAsync();
        }
    }
    
    [RelayCommand]
    private async Task LoadTemplatesAsync() 
    {
        IsLoading = true;
        try 
        {
            var loaded = await _templateService.LoadTemplatesAsync();
            
            // Filter logic if needed
            Templates = new ObservableCollection<Template>(loaded);
        }
        catch(Exception ex) 
        {
            _logger.LogError(ex, "Failed to load templates");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
