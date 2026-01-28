using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 4: Review installation settings before proceeding.
/// </summary>
public partial class ReviewStep : WizardStepBase
{
    public override string StepId => "review";
    public override string Title => "Review and Install";
    public override string Description => "Review your settings before installation";

    /// <summary>
    /// Gets the full installation path including instance name.
    /// </summary>
    public string FullInstallPath => Context != null 
        ? Path.Combine(Context.InstallPath, Context.InstanceName) 
        : string.Empty;

    /// <summary>
    /// Gets the display username.
    /// </summary>
    public string DisplayUsername => Context?.CreateUser == true 
        ? Context.Username 
        : "root (default)";

    public ReviewStep()
    {
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
                Content = "Back",
                Command = new RelayCommand(() => Workflow?.GoBack()),
                IsVisible = true,
                IsPrimary = false
            },
            new WizardButtonAction
            {
                Content = "Install",
                Command = new RelayCommand(() => Workflow?.GoNext()),
                IsVisible = true,
                IsPrimary = true,
                IconSymbol = "Play20"
            }
        ];
    }

    public override Task OnEnterAsync()
    {
        // Refresh computed properties
        OnPropertyChanged(nameof(FullInstallPath));
        OnPropertyChanged(nameof(DisplayUsername));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies default values for quick install mode.
    /// Review step just needs default options set.
    /// </summary>
    public override Task ApplyQuickInstallDefaultsAsync()
    {
        if (Context == null)
            return Task.CompletedTask;

        // Set default options for quick install
        Context.SetAsDefault = false;
        Context.LaunchAfterInstall = true;
        
        return Task.CompletedTask;
    }
}
