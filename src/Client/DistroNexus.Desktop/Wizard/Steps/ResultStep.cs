using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.Services;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 6: Show installation result (success or failure).
/// </summary>
public partial class ResultStep : WizardStepBase
{
    private readonly IPowerShellModuleClient? _moduleClient;
    private readonly ProductLogRevealLauncher _logRevealLauncher;
    public override string StepId => "result";
    public override string Title => Properties.Resources.ResultCompleteTitle;
    public override string Description => Properties.Resources.ResultCompleteDescription;

    /// <summary>
    /// This step is not shown in the step indicator.
    /// </summary>
    public override bool ShowInStepIndicator => false;

    /// <summary>
    /// Gets the full installation path.
    /// </summary>
    public string FullInstallPath => Context != null
        ? $"{Context.InstallPath}\\{Context.InstanceName}"
        : string.Empty;

    /// <summary>
    /// Gets the success message.
    /// </summary>
    public string SuccessMessage => Context != null
        ? string.Format(Properties.Resources.ResultSuccess, Context.SelectedDistribution?.Name)
        : Properties.Resources.ResultSuccessSimple;

    /// <summary>
    /// Gets whether there are error details to show.
    /// Only show if ErrorMessage exists and is different from ResultMessage.
    /// </summary>
    public bool HasErrorDetails
    {
        get
        {
            if (Context == null || string.IsNullOrWhiteSpace(Context.ErrorMessage))
                return false;

            // Don't show if the error message is the same as the result message
            if (string.Equals(Context.ErrorMessage?.Trim(), Context.ResultMessage?.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            // Show if ErrorMessage contains technical details (e.g., exception type, stack trace)
            return !string.IsNullOrEmpty(Context.ErrorMessage) && 
                   (Context.ErrorMessage.Length > 100 || 
                   Context.ErrorMessage.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                   Context.ErrorMessage.Contains("at ", StringComparison.Ordinal));
        }
    }

    public ResultStep(IPowerShellModuleClient? moduleClient = null, IBrowserLauncher? browserLauncher = null)
    { _moduleClient = moduleClient ?? App.ServiceProvider?.GetService(typeof(IPowerShellModuleClient)) as IPowerShellModuleClient; _logRevealLauncher = new ProductLogRevealLauncher(); }

    [RelayCommand]
    private async Task OpenLogFolder()
    {
        try
        {
            if (_moduleClient is null) throw new InvalidOperationException("The product log service is unavailable.");
            var target = await _moduleClient.GetProductLogRevealTargetAsync();
            if (target.OutcomeCode == "ProductLog.Ready" && target.RevealUri is not null) _logRevealLauncher.Reveal(target.RevealUri);
        }
        catch (Exception ex)
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = Properties.Resources.ErrorApplicationTitle,
                Content = string.Format(Properties.Resources.ErrorOpenLogFolder, ex.Message),
                CloseButtonText = "OK",
                MaxWidth = 400
            };

            await uiMessageBox.ShowDialogAsync();
        }
    }

    protected override UserControl CreateContent()
    {
        return new ResultStepView { DataContext = this };
    }

    protected override List<WizardButtonAction> CreateButtons()
    {
        var buttons = new List<WizardButtonAction>();

        // Show "Try Again" button if failed
        if (Context?.InstallFailed == true)
        {
            buttons.Add(new WizardButtonAction
            {
                Content = Properties.Resources.ButtonTryAgain,
                Command = new RelayCommand(TryAgain),
                IsVisible = true,
                IsPrimary = false
            });
        }

        // Always show Finish/Close button
        buttons.Add(new WizardButtonAction
        {
            Content = Context?.InstallCompleted == true ? Properties.Resources.ButtonFinish : Properties.Resources.ButtonClose,
            Command = new RelayCommand(Finish),
            IsVisible = true,
            IsPrimary = true
        });

        return buttons;
    }

    public override Task OnEnterAsync()
    {
        // Clear the step's ErrorMessage since ResultStep displays errors in its own UI
        ErrorMessage = string.Empty;

        // Refresh computed properties
        OnPropertyChanged(nameof(FullInstallPath));
        OnPropertyChanged(nameof(SuccessMessage));
        OnPropertyChanged(nameof(HasErrorDetails));

        // Refresh buttons based on result
        RefreshButtons();

        return Task.CompletedTask;
    }

    private void TryAgain()
    {
        // Reset to first step
        Context?.Reset();
        _ = Workflow?.GoToStepAsync("select-distribution");
    }

    private void Finish()
    {
        // Complete the wizard with success status
        Workflow?.Complete(Context?.InstallCompleted == true);
    }
}
