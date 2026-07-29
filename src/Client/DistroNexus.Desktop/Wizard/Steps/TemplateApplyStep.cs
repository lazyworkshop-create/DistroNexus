using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 6: Apply selected template with immersive full-screen log view.
/// </summary>
public partial class TemplateApplyStep : WizardStepBase
{
    internal static Func<bool>? RecoveryDeclineConfirmationOverride { get; set; }
    internal static Func<bool>? CancelConfirmationOverride { get; set; }
    private static readonly string[] ErrorKeywords =
    [
        "error",
        "failed",
        "fatal",
        "exception",
        "no such file or directory",
        "command not found",
        "permission denied",
        "traceback"
    ];

    private static readonly string[] WarningKeywords =
    [
        "warning",
        "warn",
        "falling back",
        "deprecated"
    ];

    private readonly IPowerShellModuleClient _moduleClient;
    private readonly ILogger _logger;
    private string? _operationId;
    private TaskCompletionSource<string?>? _operationIdReady;
    private Task? _cancelTask;
    private bool _cancellationRequested;
    private readonly object _cancelGate = new();

    public override string StepId => "template-apply";
    public override string Title => "Applying Template";
    public override string Description => "Template application in progress";
    public override bool ShowInStepIndicator => false;

    public override bool IsLogFullscreen => true;

    [ObservableProperty]
    private bool _canCancel = true;

    [ObservableProperty]
    private ObservableCollection<TemplateOutputLine> _templateOutputLines = new();

    [ObservableProperty]
    private ObservableCollection<TemplateOutputLine> _filteredTemplateOutputLines = new();

    [ObservableProperty]
    private bool _hasTemplateOutput;

    [ObservableProperty]
    private bool _showOnlyErrors;

    partial void OnShowOnlyErrorsChanged(bool value)
    {
        RebuildFilteredOutput();
    }

    public TemplateApplyStep(IPowerShellModuleClient moduleClient, ILogger logger)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override UserControl CreateContent()
    {
        return new TemplateApplyStepView { DataContext = this };
    }

    protected override List<WizardButtonAction> CreateButtons()
    {
        return [];
    }

    public override async Task OnEnterAsync()
    {
        if (Context == null || Workflow == null)
            return;

        ErrorMessage = string.Empty;

        if (Context.InstallFailed)
        {
            await Workflow.GoNextAsync();
            return;
        }

        TemplateOutputLines.Clear();
        FilteredTemplateOutputLines.Clear();
        HasTemplateOutput = false;
        ShowOnlyErrors = false;

        if (!Context.ApplyTemplateAfterInstall || Context.SelectedTemplate == null)
        {
            Context.InstallCompleted = true;
            Context.InstallFailed = false;
            if (string.IsNullOrWhiteSpace(Context.ResultMessage))
            {
                Context.ResultMessage = "Installation completed successfully!";
            }

            await Workflow.GoNextAsync();
            return;
        }

        await StartTemplateApplyAsync();
    }

    private async Task StartTemplateApplyAsync()
    {
        if (Context == null || Workflow == null)
            return;

        _operationIdReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancelTask = null;
        _cancellationRequested = false;
        Context.IsInstalling = true;
        Context.InstallProgress = 0;
        Context.InstallStatusMessage = $"Applying template: {Context.SelectedTemplate?.Name}...";

        try
        {
            var variables = Context.TemplateVariableSelections;
            var preview = await _moduleClient.PreviewTemplateApplyAsync(Context.InstanceName, Context.SelectedTemplate!.Id, variables, false);
            if (preview.RequiresRecoveryDecline)
            {
                if (!ConfirmRecoveryDecline()) throw new OperationCanceledException("Template application was paused so the user can create a recovery point.");
                preview = await _moduleClient.PreviewTemplateApplyAsync(Context.InstanceName, Context.SelectedTemplate.Id, variables, true);
            }
            if (string.IsNullOrWhiteSpace(preview.PreviewToken)) throw new InvalidOperationException("Template application preview was not approved.");
            _operationId = (await _moduleClient.StartTemplateApplyAsync(preview.PreviewToken)).OperationId;
            _operationIdReady.TrySetResult(_operationId);
            if (_cancellationRequested) _ = GetOrStartCancellation(_operationId);
            var templateResult = await WaitForCompletionAsync(_operationId);
            if (templateResult.State == TemplateOperationState.Cancelled)
            {
                Context.InstallFailed = true;
                Context.InstallCompleted = false;
                Context.ResultMessage = "Template application was cancelled by user.";
                return;
            }
            if (templateResult.State != TemplateOperationState.Succeeded)
                throw new InvalidOperationException($"Template application failed: {templateResult.Message}");

            Context.InstallProgress = 100;
            Context.InstallCompleted = true;
            Context.InstallFailed = false;
            Context.ResultMessage = $"Template '{Context.SelectedTemplate.Name}' applied ({templateResult.ExecutedScripts.Count} scripts).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template application failed");
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            Context.ResultMessage = ex.Message;
        }
        finally
        {
            Context.IsInstalling = false;
            CanCancel = false;
            _operationIdReady?.TrySetResult(null);
            _operationIdReady = null;
            _operationId = null;
            await Workflow.GoNextAsync();
        }
    }

    private async Task<TemplateApplyOperationStatus> WaitForCompletionAsync(string operationId)
    {
        while (true)
        {
            var status = await _moduleClient.GetTemplateApplyOperationStatusAsync(operationId);
            Context!.InstallProgress = status.TotalScripts == 0 ? 0 : status.CompletedScripts * 100d / status.TotalScripts;
            Context.InstallStatusMessage = $"Template: {status.Message}";
            if (!string.IsNullOrWhiteSpace(status.Message)) AppendTemplateOutput(status.CurrentScript ?? "Template", status.Message);
            if (status.State is TemplateOperationState.Succeeded or TemplateOperationState.Failed or TemplateOperationState.Cancelled or TemplateOperationState.Interrupted) return status;
            await Task.Delay(TimeSpan.FromMilliseconds(350));
        }
    }

    private void AppendTemplateOutput(string scriptName, string output)
    {
        var text = output.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fromStandardError = false;
        if (text.StartsWith("[STDERR]", StringComparison.OrdinalIgnoreCase))
        {
            fromStandardError = true;
            text = text[8..].TrimStart();
        }

        var severity = ClassifyOutputSeverity(text, fromStandardError);
        var message = TrimSeverityPrefix(text);
        var header = string.IsNullOrWhiteSpace(scriptName) ? "Script" : scriptName;

        TemplateOutputLines.Add(new TemplateOutputLine($"[{header}] {message}", severity));
        HasTemplateOutput = true;
        RebuildFilteredOutput();
    }

    private static bool ConfirmRecoveryDecline() => RecoveryDeclineConfirmationOverride?.Invoke() ?? MessageBox.Show(
        DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString("Recovery_OfferTemplate") ?? "Recovery point available.",
        DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString("Recovery_OfferTitle") ?? "Optional recovery point", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private static TemplateOutputSeverity ClassifyOutputSeverity(string message, bool fromStandardError)
    {
        if (message.StartsWith("[ERR]", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateOutputSeverity.Error;
        }

        if (message.StartsWith("[WARN]", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("[WARNING]", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateOutputSeverity.Warning;
        }

        if (ErrorKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return TemplateOutputSeverity.Error;
        }

        if (WarningKeywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return TemplateOutputSeverity.Warning;
        }

        return TemplateOutputSeverity.Info;
    }

    private static string TrimSeverityPrefix(string message)
    {
        if (message.StartsWith("[ERR]", StringComparison.OrdinalIgnoreCase))
        {
            return message[5..].TrimStart();
        }

        if (message.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase))
        {
            return message[7..].TrimStart();
        }

        if (message.StartsWith("[WARN]", StringComparison.OrdinalIgnoreCase))
        {
            return message[6..].TrimStart();
        }

        if (message.StartsWith("[WARNING]", StringComparison.OrdinalIgnoreCase))
        {
            return message[9..].TrimStart();
        }

        return message;
    }

    private void RebuildFilteredOutput()
    {
        FilteredTemplateOutputLines.Clear();

        foreach (var line in TemplateOutputLines)
        {
            if (!ShowOnlyErrors || line.IsError)
            {
                FilteredTemplateOutputLines.Add(line);
            }
        }

        HasTemplateOutput = FilteredTemplateOutputLines.Count > 0;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        var confirmed = CancelConfirmationOverride?.Invoke() ?? DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.TitleCancelInstallation,
            "Cancel template application?",
            "Yes");

        if (!confirmed) return;
        CanCancel = false;
        _cancellationRequested = true;
        var id = _operationId ?? await (_operationIdReady?.Task ?? Task.FromResult<string?>(null));
        if (string.IsNullOrWhiteSpace(id)) return;
        await GetOrStartCancellation(id);
    }

    private Task GetOrStartCancellation(string operationId)
    {
        lock (_cancelGate) return _cancelTask ??= RequestCancellationAsync(operationId);
    }

    private async Task RequestCancellationAsync(string operationId)
    {
        TemplateApplyCancelResult result;
        try
        {
            result = await _moduleClient.CancelTemplateApplyAsync(operationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Template cancellation request failed for {OperationId}", operationId);
            return;
        }

        if (!result.Accepted) return;
        var status = await WaitForCompletionAsync(operationId);
        if (Context is not null && status.State == TemplateOperationState.Cancelled)
        {
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            Context.ResultMessage = "Template application was cancelled by user.";
        }
    }

}

public enum TemplateOutputSeverity
{
    Info,
    Warning,
    Error
}

public sealed record TemplateOutputLine(string Text, TemplateOutputSeverity Severity)
{
    public bool IsWarning => Severity == TemplateOutputSeverity.Warning;

    public bool IsError => Severity == TemplateOutputSeverity.Error;
}
