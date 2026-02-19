using System.Collections.ObjectModel;
using System.Windows.Controls;
using DistroNexus.Desktop.Wizard;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step for configuring advanced template options.
/// </summary>
public partial class TemplateOptionsStep : WizardStepBase
{
    public override string StepId => "template-options";
    public override string Title => Properties.Resources.TemplateAdvancedOptions;
    public override string Description => Properties.Resources.TemplateVersionOptions;

    public override bool ShouldSkip(WizardContext context)
    {
        return context.SelectedTemplate == null ||
               !context.ApplyTemplateAfterInstall ||
               context.SelectedTemplate.VersionOptions.Count == 0;
    }

    public ObservableCollection<TemplateVersionSelectionItem> VersionSelections { get; private set; } = new();

    protected override UserControl CreateContent()
    {
        return new TemplateOptionsStepView { DataContext = this };
    }

    public override Task OnEnterAsync()
    {
        if (Context == null || Context.SelectedTemplate == null)
        {
            VersionSelections = new ObservableCollection<TemplateVersionSelectionItem>();
            OnPropertyChanged(nameof(VersionSelections));
            return Task.CompletedTask;
        }

        BuildSelections(Context.SelectedTemplate);
        ErrorMessage = string.Empty;
        return Task.CompletedTask;
    }

    public override bool Validate()
    {
        if (Context == null || Context.SelectedTemplate == null)
        {
            return true;
        }

        foreach (var selection in VersionSelections.Where(item => item.IsRequired))
        {
            if (string.IsNullOrWhiteSpace(selection.SelectedValue))
            {
                ErrorMessage = string.Format(Properties.Resources.ErrorTemplateRequiredOptionMissingFormat, selection.Label);
                return false;
            }
        }

        ErrorMessage = string.Empty;
        return true;
    }

    public override Task OnExitAsync()
    {
        if (Context != null)
        {
            Context.TemplateVariableSelections = VersionSelections
                .Where(item => !string.IsNullOrWhiteSpace(item.SelectedValue))
                .ToDictionary(item => item.Key, item => item.SelectedValue!);
        }

        return Task.CompletedTask;
    }

    private void BuildSelections(Core.Models.Template template)
    {
        if (Context == null)
        {
            VersionSelections = new ObservableCollection<TemplateVersionSelectionItem>();
            OnPropertyChanged(nameof(VersionSelections));
            return;
        }

        var contextSelections = Context.TemplateVariableSelections;
        var selectionItems = new List<TemplateVersionSelectionItem>();

        foreach (var option in template.VersionOptions)
        {
            string? selectedValue = null;

            if (contextSelections.TryGetValue(option.Key, out var existingValue))
            {
                selectedValue = existingValue;
            }
            else if (template.DefaultSelections.TryGetValue(option.Key, out var templateDefaultValue))
            {
                selectedValue = templateDefaultValue;
            }
            else if (!string.IsNullOrWhiteSpace(option.DefaultValue))
            {
                selectedValue = option.DefaultValue;
            }
            else
            {
                selectedValue = option.Options.FirstOrDefault()?.Value;
            }

            selectionItems.Add(new TemplateVersionSelectionItem(option, selectedValue));
        }

        VersionSelections = new ObservableCollection<TemplateVersionSelectionItem>(selectionItems);
        OnPropertyChanged(nameof(VersionSelections));
    }
}
