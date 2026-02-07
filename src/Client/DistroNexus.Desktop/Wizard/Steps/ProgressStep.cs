using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 5: Installation progress (hidden from step indicator).
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

        // Clear the step's ErrorMessage since ProgressStep displays status in its own UI
        ErrorMessage = string.Empty;

        // Set log file path for error reporting
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

            // Progress callback
            var progress = new Progress<(double percentage, string message)>(p =>
            {
                Context.InstallProgress = p.percentage;
                Context.InstallStatusMessage = p.message;
            });

            await _wslManager.InstallInstanceAsync(options, progress, _installCts.Token);

            Context.InstallProgress = 100;
            Context.InstallStatusMessage = "Installation completed successfully!";
            Context.InstallCompleted = true;
            Context.InstallFailed = false;
            Context.ResultMessage = "Installation completed successfully!";

            _logger.LogInformation("Installation completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Installation cancelled by user at their request");
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            
            // Build cancellation details with installation parameters
            Context.ErrorMessage = BuildCancellationContext();
            Context.ResultMessage = "Installation was cancelled by user.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation failed");
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            
            // Build detailed error context with sanitized parameters
            var errorDetails = BuildErrorContext(ex);
            
            Context.ErrorMessage = errorDetails;
            
            // Extract user-friendly message without "Installation failed:" prefix
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

            // Navigate to result step
            await Workflow.GoNextAsync();
        }
    }

    /// <summary>
    /// Builds error context with sanitized installation parameters.
    /// </summary>
    private string BuildErrorContext(Exception ex)
    {
        if (Context == null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        
        // Format installation details in a clean, readable way
        sb.AppendLine($"Instance Name: {Context.InstanceName}");
        sb.AppendLine($"Distribution: {Context.SelectedDistribution?.Name ?? "N/A"} {Context.SelectedDistribution?.Version ?? ""}".Trim());
        sb.AppendLine($"Install Path: {MaskPath(Context.InstallPath)}");
        sb.AppendLine($"WSL Version: {Context.WslVersion}");
        
        if (Context.CreateUser && !string.IsNullOrWhiteSpace(Context.Username))
        {
            sb.AppendLine($"Username: {Context.Username}");
            sb.AppendLine($"Password: {(string.IsNullOrWhiteSpace(Context.Password) ? "Not set" : "****")}");
        }
        
        if (!string.IsNullOrWhiteSpace(Context.SelectedDistribution?.DownloadUrl))
        {
            sb.AppendLine($"Download URL: {MaskUrl(Context.SelectedDistribution.DownloadUrl)}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds cancellation context with basic installation parameters.
    /// </summary>
    private string BuildCancellationContext()
    {
        if (Context == null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        
        // Show only basic info for user cancellation
        sb.AppendLine($"Instance Name: {Context.InstanceName}");
        sb.AppendLine($"Distribution: {Context.SelectedDistribution?.Name ?? "N/A"} {Context.SelectedDistribution?.Version ?? ""}".Trim());
        sb.AppendLine($"Install Path: {MaskPath(Context.InstallPath)}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Masks sensitive parts of a file path (user-specific folders).
    /// </summary>
    private static string MaskPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "(none)";

        try
        {
            // Mask username in path like C:\Users\username\... -> C:\Users\***\...
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

    /// <summary>
    /// Masks sensitive parts of a URL (query parameters, tokens).
    /// </summary>
    private static string MaskUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "(none)";

        try
        {
            var uri = new Uri(url);
            
            // If has query string, mask it
            if (!string.IsNullOrEmpty(uri.Query))
            {
                return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}?***";
            }
            
            return url;
        }
        catch
        {
            return url;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Show confirmation dialog
        var result = System.Windows.MessageBox.Show(
            Properties.Resources.ConfirmCancelInstallation,
            Properties.Resources.TitleCancelInstallation,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (result == System.Windows.MessageBoxResult.Yes)
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
