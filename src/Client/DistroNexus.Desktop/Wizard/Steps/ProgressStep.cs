using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 5: Installation progress (base distro installation only).
/// </summary>
public partial class ProgressStep : WizardStepBase
{
    private readonly IWslManagerService _wslManager;
    private readonly ILogger _logger;
    private CancellationTokenSource? _installCts;

    public override string StepId => "progress";
    public override string Title => Properties.Resources.WizardStepInstalling;
    public override string Description => "Installation in progress";

    /// <summary>
    /// This step is not shown in the step indicator.
    /// </summary>
    public override bool ShowInStepIndicator => false;

    [ObservableProperty]
    private bool _canCancel = true;

    public ProgressStep(IWslManagerService wslManager, ILogger logger)
    {
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override UserControl CreateContent()
    {
        return new ProgressStepView { DataContext = this };
    }

    protected override List<WizardButtonAction> CreateButtons()
    {
        // No buttons during installation - cancel is in the view
        return [];
    }

    public override async Task OnEnterAsync()
    {
        if (Context == null || Workflow == null)
            return;

        ErrorMessage = string.Empty;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Context.LogFilePath = Path.Combine(appDataPath, "DistroNexus", "logs", "distronexus.log");

        Context.IsInstalling = true;
        Context.InstallProgress = 0;
        Context.InstallStatusMessage = "Preparing installation...";
        Workflow.RefreshNavigationState();

        await StartInstallationAsync();
    }

    private async Task StartInstallationAsync()
    {
        if (Context == null || Workflow == null)
            return;

        _installCts = new CancellationTokenSource();

        try
        {
            _logger.LogInformation("Starting installation of {DistroName} to {Path}",
                Context.SelectedDistribution?.Name, Context.InstallPath);

            var options = Context.ToInstallOptions();

            var progress = new Progress<(double percentage, string message)>(p =>
            {
                Context.InstallProgress = p.percentage;
                Context.InstallStatusMessage = p.message;
            });

            await _wslManager.InstallInstanceAsync(options, progress, _installCts.Token);

            Context.InstallProgress = 100;
            Context.InstallStatusMessage = "Base installation completed.";
            Context.InstallFailed = false;
            Context.InstallCompleted = false;
            Context.ResultMessage = "Base installation completed.";

            _logger.LogInformation("Base installation completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Installation cancelled by user at their request");
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            Context.ErrorMessage = BuildCancellationContext();
            Context.ResultMessage = "Installation was cancelled by user.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation failed");
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            Context.ErrorMessage = BuildErrorContext();

            var friendlyMessage = ex.Message;
            if (friendlyMessage.StartsWith("Installation failed:", StringComparison.OrdinalIgnoreCase))
            {
                friendlyMessage = friendlyMessage["Installation failed:".Length..].Trim();
            }
            Context.ResultMessage = friendlyMessage;
        }
        finally
        {
            Context.IsInstalling = false;
            CanCancel = false;
            _installCts?.Dispose();
            _installCts = null;

            await Workflow.GoNextAsync();
        }
    }

    private string BuildErrorContext()
    {
        if (Context == null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Instance Name: {Context.InstanceName}");
        sb.AppendLine($"Distribution: {Context.SelectedDistribution?.Name ?? "N/A"} {Context.SelectedDistribution?.Version ?? ""}".Trim());
        sb.AppendLine($"Install Path: {MaskPath(Context.InstallPath)}");
        sb.AppendLine($"WSL Version: {Context.WslVersion}");
        return sb.ToString().TrimEnd();
    }

    private string BuildCancellationContext()
    {
        if (Context == null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Instance Name: {Context.InstanceName}");
        sb.AppendLine($"Distribution: {Context.SelectedDistribution?.Name ?? "N/A"} {Context.SelectedDistribution?.Version ?? ""}".Trim());
        sb.AppendLine($"Install Path: {MaskPath(Context.InstallPath)}");
        return sb.ToString().TrimEnd();
    }

    private static string MaskPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "(none)";

        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && path.Contains(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                var username = Path.GetFileName(userProfile);
                return path.Replace(username, "***", StringComparison.OrdinalIgnoreCase);
            }

            return path;
        }
        catch
        {
            return path;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.TitleCancelInstallation,
            Properties.Resources.ConfirmCancelInstallation,
            "Yes");

        if (confirmed)
        {
            _logger.LogInformation("User confirmed cancellation of installation");
            _installCts?.Cancel();
            CanCancel = false;
        }
        else
        {
            _logger.LogInformation("User cancelled the cancellation dialog");
        }
    }

    public override Task OnExitAsync()
    {
        if (Context != null)
        {
            Context.IsInstalling = false;
        }

        Workflow?.RefreshNavigationState();
        return Task.CompletedTask;
    }
}
