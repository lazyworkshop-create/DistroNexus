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
    private List<Template> _allTemplates = [];

    public override string StepId => "select-template";
    public override string Title => "Select Template"; 
    public override string Description => "Choose a development environment template (Optional)";

    [ObservableProperty]
    private ObservableCollection<Template> _templates = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _skipTemplate;

    public Template? SelectedTemplate
    {
        get => Context?.SelectedTemplate;
        set
        {
            if (Context != null)
            {
                Context.SelectedTemplate = value;
                if (value != null)
                {
                    SkipTemplate = false;
                    Context.ApplyTemplateAfterInstall = true;
                }

                OnPropertyChanged(nameof(SelectedTemplate));
            }
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSkipTemplateChanged(bool value)
    {
        if (Context == null)
        {
            return;
        }

        Context.ApplyTemplateAfterInstall = !value;

        if (value)
        {
            Context.SelectedTemplate = null;
            OnPropertyChanged(nameof(SelectedTemplate));
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
        if (Context != null)
        {
            SkipTemplate = !Context.ApplyTemplateAfterInstall;
        }

        if (Templates.Count == 0)
        {
            await LoadTemplatesAsync();
        }
        else
        {
            ApplyFilters();
        }
    }

    public override bool Validate()
    {
        if (Context == null)
        {
            return false;
        }

        if (SkipTemplate)
        {
            ErrorMessage = string.Empty;
            Context.ApplyTemplateAfterInstall = false;
            Context.SelectedTemplate = null;
            return true;
        }

        if (SelectedTemplate == null)
        {
            ErrorMessage = "Select a template or enable skip template.";
            return false;
        }

        if (!IsTemplateCompatible(SelectedTemplate))
        {
            ErrorMessage = "Selected template is not compatible with the selected distribution.";
            return false;
        }

        ErrorMessage = string.Empty;
        Context.ApplyTemplateAfterInstall = true;
        return true;
    }

    public override Task OnExitAsync()
    {
        if (Context != null)
        {
            Context.ApplyTemplateAfterInstall = !SkipTemplate;
            if (SkipTemplate)
            {
                Context.SelectedTemplate = null;
            }
        }

        return Task.CompletedTask;
    }
    
    [RelayCommand]
    private async Task LoadTemplatesAsync() 
    {
        IsLoading = true;
        try 
        {
            var loaded = await _templateService.LoadTemplatesAsync();

            _allTemplates = loaded;
            ApplyFilters();
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

    [RelayCommand]
    private void SkipTemplateSelection()
    {
        SkipTemplate = true;
    }

    private void ApplyFilters()
    {
        IEnumerable<Template> filtered = _allTemplates;

        filtered = filtered.Where(IsTemplateCompatible);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = filtered.Where(t =>
                t.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        Templates = new ObservableCollection<Template>(filtered);

        if (SelectedTemplate != null && Templates.All(t => !string.Equals(t.Id, SelectedTemplate.Id, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedTemplate = null;
        }
    }

    private bool IsTemplateCompatible(Template template)
    {
        if (Context?.SelectedDistribution == null || template.CompatibleDistros.Count == 0)
        {
            return true;
        }

        var distro = Context.SelectedDistribution;
        var candidates = new[] { distro.Name, distro.DefaultName, distro.Id }
            .Where(s => !string.IsNullOrWhiteSpace(s));

        return template.CompatibleDistros.Any(compat =>
            candidates.Any(c => c.Contains(compat, StringComparison.OrdinalIgnoreCase) || compat.Contains(c, StringComparison.OrdinalIgnoreCase)));
    }
}
