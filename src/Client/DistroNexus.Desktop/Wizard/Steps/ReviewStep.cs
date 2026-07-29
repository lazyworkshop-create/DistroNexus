using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 4: Review installation settings before proceeding.
/// </summary>
public partial class ReviewStep : WizardStepBase
{
    private readonly IPowerShellModuleClient _moduleClient;

    public override string StepId => "review";
    public override string Title => Properties.Resources.ReviewInstallTitle;
    public override string Description => Properties.Resources.ReviewInstallDescription;

    /// <summary>
    /// Gets the display-safe install target returned by the module preview.
    /// </summary>
    public string FullInstallPath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display username.
    /// </summary>
    public string DisplayUsername => Context?.CreateUser == true 
        ? Context.Username 
        : Properties.Resources.DefaultUserRoot;

    public string WslVersionDisplayText => Context != null
        ? string.Format(Properties.Resources.WslVersionFormat, Context.WslVersion)
        : string.Empty;

    public bool IsTemplateEnabled => Context?.ApplyTemplateAfterInstall == true && Context.SelectedTemplate != null;

    public string TemplateNameDisplay =>
        IsTemplateEnabled
            ? Context!.SelectedTemplate!.Name
            : Properties.Resources.LabelNoTemplateSelected;

    public string TemplateCategoryDisplay =>
        IsTemplateEnabled
            ? string.IsNullOrWhiteSpace(Context!.SelectedTemplate!.Category)
                ? Properties.Resources.LabelUnknownValue
                : Context.SelectedTemplate.Category
            : Properties.Resources.LabelNoTemplateSelected;

    public string TemplateDescriptorDisplay
    {
        get
        {
            if (!IsTemplateEnabled)
            {
                return Properties.Resources.LabelNoTemplateSelected;
            }

            var template = Context!.SelectedTemplate!;
            var packageCount = template.Packages?.Count ?? 0;
            var scriptCount = template.Scripts?.Count ?? 0;

            return string.Format(Properties.Resources.TemplateSummaryDescriptorFormat, packageCount, scriptCount);
        }
    }

    public ReviewStep(IPowerShellModuleClient moduleClient)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
    }

    protected override UserControl CreateContent()
    {
        return new ReviewStepView { DataContext = this };
    }

    protected override List<WizardButtonAction> CreateButtons()
    {
        return
        [
            new WizardButtonAction
            {
                Content = Properties.Resources.ButtonBack,
                Command = new RelayCommand(() => Workflow?.GoBack()),
                IsVisible = true,
                IsPrimary = false
            },
            new WizardButtonAction
            {
                Content = Properties.Resources.ActionInstall,
                Command = new RelayCommand(() => Workflow?.GoNext()),
                IsVisible = true,
                IsPrimary = true,
                IconSymbol = "Play20"
            }
        ];
    }

    public override async Task OnEnterAsync()
    {
        if (Context != null)
        {
            Context.SetAsDefault = false;
            Context.LaunchAfterInstall = false;

            var target = await _moduleClient.PreviewInstallTargetAsync(Context.InstallPath);
            FullInstallPath = target.DisplayName;
        }

        // Refresh computed properties
        OnPropertyChanged(nameof(FullInstallPath));
        OnPropertyChanged(nameof(DisplayUsername));
        OnPropertyChanged(nameof(WslVersionDisplayText));
        OnPropertyChanged(nameof(IsTemplateEnabled));
        OnPropertyChanged(nameof(TemplateNameDisplay));
        OnPropertyChanged(nameof(TemplateCategoryDisplay));
        OnPropertyChanged(nameof(TemplateDescriptorDisplay));
    }

    /// <summary>
    /// Applies default values for quick install mode.
    /// Review step just needs default options set.
    /// </summary>
    public override Task ApplyQuickInstallDefaultsAsync()
    {
        if (Context == null)
            return Task.CompletedTask;

        // Keep post-install options disabled
        Context.SetAsDefault = false;
        Context.LaunchAfterInstall = false;
        
        return Task.CompletedTask;
    }
}
