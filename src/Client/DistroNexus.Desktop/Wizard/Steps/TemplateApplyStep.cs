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
                null,
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

        var isError = text.StartsWith("[ERR]", StringComparison.OrdinalIgnoreCase);
        var message = isError ? text[5..].TrimStart() : text;
        var header = string.IsNullOrWhiteSpace(scriptName) ? "Script" : scriptName;

        TemplateOutputLines.Add(new TemplateOutputLine($"[{header}] {message}", isError));
        HasTemplateOutput = true;
        RebuildFilteredOutput();
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

public sealed record TemplateOutputLine(string Text, bool IsError);
