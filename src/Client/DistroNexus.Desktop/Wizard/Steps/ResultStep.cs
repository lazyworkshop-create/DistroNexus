using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 6: Show installation result (success or failure).
/// </summary>
public partial class ResultStep : WizardStepBase
{
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
        ? Path.Combine(Context.InstallPath, Context.InstanceName)
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

    public ResultStep()
    {
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            // Determine log folder path
            string logFolder;

            if (Context != null && !string.IsNullOrEmpty(Context.LogFilePath))
            {
                // Use log file path from context
                var logDir = Path.GetDirectoryName(Context.LogFilePath);
                if (!string.IsNullOrEmpty(logDir) && Directory.Exists(logDir))
                {
                    logFolder = logDir;
                }
                else
                {
                    // Fallback to default
                    logFolder = GetDefaultLogFolder();
                }
            }
            else
            {
                // Use default log folder
                logFolder = GetDefaultLogFolder();
            }

            // Ensure folder exists
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            // Open folder in Windows Explorer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logFolder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                string.Format(Properties.Resources.ErrorOpenLogFolder, ex.Message),
                Properties.Resources.ErrorApplicationTitle,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Gets the default log folder path (AppData\Roaming\DistroNexus\Logs).
    /// </summary>
    private static string GetDefaultLogFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DistroNexus",
            "Logs");
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
