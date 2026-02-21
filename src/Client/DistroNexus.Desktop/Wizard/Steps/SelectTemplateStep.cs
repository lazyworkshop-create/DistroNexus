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
    public override string Title => Properties.Resources.WizardStepSelectTemplate;
    public override string Description => Properties.Resources.WizardStepSelectTemplateDescription;

    [ObservableProperty]
    private ObservableCollection<Template> _templates = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private string _selectedScenarioTag = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categoryOptions = new(["All"]);

    [ObservableProperty]
    private ObservableCollection<string> _scenarioTagOptions = new(["All"]);

    [ObservableProperty]
    private bool _useTemplate;

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
                    UseTemplate = true;
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

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedScenarioTagChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnUseTemplateChanged(bool value)
    {
        if (Context == null)
        {
            return;
        }

        Context.ApplyTemplateAfterInstall = value;

        if (!value)
        {
            Context.SelectedTemplate = null;
            Context.TemplateVariableSelections = new Dictionary<string, string>();
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
            UseTemplate = Context.ApplyTemplateAfterInstall;
            if (Context.SelectedTemplate != null && !IsTemplateCompatible(Context.SelectedTemplate))
            {
                ErrorMessage = string.Format(
                    Properties.Resources.WizardTemplateCompatibilityWarningFormat,
                    Context.SelectedTemplate.Name,
                    Context.SelectedDistribution?.Name ?? Properties.Resources.LabelDistributionUnknownValue);
            }
            else
            {
                ErrorMessage = string.Empty;
            }
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

        if (!UseTemplate)
        {
            ErrorMessage = string.Empty;
            Context.ApplyTemplateAfterInstall = false;
            Context.SelectedTemplate = null;
            Context.TemplateVariableSelections = new Dictionary<string, string>();
            return true;
        }

        if (SelectedTemplate == null)
        {
            ErrorMessage = Properties.Resources.ErrorTemplateSelectionRequired;
            return false;
        }

        if (!IsTemplateCompatible(SelectedTemplate))
        {
            ErrorMessage = string.Format(
                Properties.Resources.WizardTemplateCompatibilityWarningFormat,
                SelectedTemplate.Name,
                Context.SelectedDistribution?.Name ?? Properties.Resources.LabelDistributionUnknownValue);
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
            Context.ApplyTemplateAfterInstall = UseTemplate;
            if (!UseTemplate)
            {
                Context.SelectedTemplate = null;
                Context.TemplateVariableSelections = new Dictionary<string, string>();
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
            CategoryOptions = new ObservableCollection<string>(new[] { "All" }
                .Concat(_allTemplates.Select(t => t.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c)));
            ScenarioTagOptions = new ObservableCollection<string>(new[] { "All" }
                .Concat(_allTemplates.SelectMany(t => t.ScenarioTags).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c)));

            if (!CategoryOptions.Contains(SelectedCategory))
            {
                SelectedCategory = "All";
            }

            if (!ScenarioTagOptions.Contains(SelectedScenarioTag))
            {
                SelectedScenarioTag = "All";
            }

            ApplyFilters();
        }
        catch(Exception ex) 
        {
            _logger.LogError(ex, "Failed to load templates");
            ErrorMessage = Properties.Resources.ErrorTemplateLoadFailed;
        }
        finally
        {
            IsLoading = false;
        }
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
                t.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.ScenarioTags.Any(tag => tag.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => string.Equals(t.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedScenarioTag, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => t.ScenarioTags.Any(tag => string.Equals(tag, SelectedScenarioTag, StringComparison.OrdinalIgnoreCase)));
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

public partial class TemplateVersionSelectionItem : ObservableObject
{
    public string Key { get; }
    public string Label { get; }
    public string Description { get; }
    public bool IsRequired { get; }
    public TemplateOptionType Type { get; }
    public ObservableCollection<TemplateOptionValueItem> Options { get; }

    [ObservableProperty]
    private string? _selectedValue;

    public TemplateVersionSelectionItem(TemplateVersionOption option, string? selectedValue)
    {
        Key = option.Key;
        Label = option.Label;
        Description = option.Description;
        IsRequired = option.Required;
        Type = option.Type;
        
        var selectedValues = selectedValue?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList() ?? new List<string>();
        
        Options = new ObservableCollection<TemplateOptionValueItem>(
            option.Options.Select(o => new TemplateOptionValueItem(o, selectedValues.Contains(o.Value), this))
        );
        
        SelectedValue = selectedValue;
    }

    public void UpdateSelectedValueFromOptions()
    {
        if (Type == TemplateOptionType.MultiSelect)
        {
            SelectedValue = string.Join(",", Options.Where(o => o.IsSelected).Select(o => o.Value));
        }
    }
}

public partial class TemplateOptionValueItem : ObservableObject
{
    public string Value { get; }
    public string Label { get; }
    public string Description { get; }
    
    private readonly TemplateVersionSelectionItem _parent;

    [ObservableProperty]
    private bool _isSelected;

    public TemplateOptionValueItem(TemplateOptionValue option, bool isSelected, TemplateVersionSelectionItem parent)
    {
        Value = option.Value;
        Label = option.Label;
        Description = option.Description;
        _isSelected = isSelected;
        _parent = parent;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _parent.UpdateSelectedValueFromOptions();
    }
}
