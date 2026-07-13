using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
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

    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;
    private CancellationTokenSource? _applyCts;

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

    public TemplateApplyStep(ITemplateService templateService, ILogger logger)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
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

        _applyCts = new CancellationTokenSource();
        Context.IsInstalling = true;
        Context.InstallProgress = 0;
        Context.InstallStatusMessage = $"Applying template: {Context.SelectedTemplate?.Name}...";

        try
        {
            var recoveryOffer = await _templateService.GetRecoveryOfferAsync(Context.InstanceName, _applyCts.Token);
            if (recoveryOffer.IsAvailable && !ConfirmRecoveryDecline())
                throw new OperationCanceledException("Template application was paused so the user can create a recovery point.");
            if (Context.SelectedTemplate != null && Context.SelectedTemplate.IsCustom && !ConfirmCustomTemplateApplication(Context.SelectedTemplate.Name))
            {
                throw new OperationCanceledException("Custom template application was cancelled by user confirmation.");
            }

            var tplProgress = new Progress<DistroNexus.Core.Models.TemplateProgress>(p =>
            {
                Context.InstallProgress = p.PercentComplete;
                Context.InstallStatusMessage = $"Template: {p.StatusMessage}";

                if (!string.IsNullOrWhiteSpace(p.LatestOutput))
                {
                    AppendTemplateOutput(p.CurrentScript, p.LatestOutput);
                }
            });

            var templateResult = await _templateService.ApplyTemplateAsync(
                Context.SelectedTemplate!.Id,
                Context.InstanceName,
                Context.TemplateVariableSelections.Count > 0 ? Context.TemplateVariableSelections : null,
                tplProgress,
                _applyCts.Token);

            if (!templateResult.Success)
            {
                var firstError = templateResult.Errors.FirstOrDefault() ?? templateResult.Message;
                throw new InvalidOperationException($"Template application failed: {firstError}");
            }

            Context.InstallProgress = 100;
            Context.InstallCompleted = true;
            Context.InstallFailed = false;
            Context.ResultMessage = $"Template '{Context.SelectedTemplate.Name}' applied ({templateResult.ExecutedScripts.Count} scripts, {templateResult.Duration.TotalSeconds:F1}s).";
        }
        catch (OperationCanceledException)
        {
            Context.InstallFailed = true;
            Context.InstallCompleted = false;
            Context.ResultMessage = "Template application was cancelled by user.";
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
            _applyCts?.Dispose();
            _applyCts = null;
            await Workflow.GoNextAsync();
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

    private static bool ConfirmRecoveryDecline() => MessageBox.Show(
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
    private void Cancel()
    {
        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.TitleCancelInstallation,
            "Cancel template application?",
            "Yes");

        if (confirmed)
        {
            _applyCts?.Cancel();
            CanCancel = false;
        }
    }

    private static bool ConfirmCustomTemplateApplication(string templateName)
    {
        var message = $"You are about to apply a custom template '{templateName}'. Continue?";
        var result = MessageBox.Show(message, "Custom Template Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
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
