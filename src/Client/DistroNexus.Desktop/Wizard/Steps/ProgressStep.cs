using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Controls;
using System.Security;
using System.Windows;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 5: Installation progress (base distro installation only).
/// </summary>
public partial class ProgressStep : WizardStepBase
{
    private readonly IPowerShellModuleClient _moduleClient;
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

    public ProgressStep(IPowerShellModuleClient moduleClient, ILogger logger)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
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

            var packageId = Context.SelectedDistribution?.Id ?? throw new InvalidOperationException("A package is required.");
            Context.InstallProgress = 15;
            var source = await _moduleClient.ResolveInstallSourceAsync(packageId, _installCts.Token);
            Context.InstallProgress = 30;
            var acquisition = await _moduleClient.PreviewPackageAcquisitionAsync(source.PackageId, _installCts.Token);
            var package = await _moduleClient.AcquirePackageAsync(acquisition.PreviewToken, _installCts.Token);
            Context.InstallProgress = 55;
            using var password = await PromptForPasswordAsync(Context.CreateUser, _installCts.Token);
            var result = await _moduleClient.InstallVerifiedInstanceAsync(package.PackageReference, Context.InstanceName, Context.InstallPath, Context.CreateUser ? Context.Username : "root", "bash", null, Context.SetAsDefault, password, _installCts.Token);
            if (!result.Succeeded) throw new InvalidOperationException(result.OutcomeCode);

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

    private static async Task<SecureString?> PromptForPasswordAsync(bool createUser, CancellationToken cancellationToken)
    {
        if (!createUser) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var password = new PasswordBox { MinWidth = 220 };
        var confirmation = new PasswordBox { MinWidth = 220 };
        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = Properties.Resources.WizardStepConfigureUser,
            Content = new StackPanel { Children = { new TextBlock { Text = Properties.Resources.LabelPassword }, password, new TextBlock { Text = Properties.Resources.LabelConfirmPassword }, confirmation } },
            PrimaryButtonText = Properties.Resources.ButtonOK,
            CloseButtonText = Properties.Resources.ButtonCancel
        };
        var response = await dialog.ShowDialogAsync();
        try
        {
            if (response != Wpf.Ui.Controls.MessageBoxResult.Primary) throw new OperationCanceledException(cancellationToken);
            using var entered = password.SecurePassword.Copy();
            using var repeated = confirmation.SecurePassword.Copy();
            if (entered.Length < 4 || !SecurePasswordAdapter.AreEqual(entered, repeated)) throw new InvalidOperationException(Properties.Resources.ErrorPasswordMismatch);
            return entered.Copy();
        }
        finally { SecurePasswordAdapter.ClearPassword(password, confirmation); }
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
