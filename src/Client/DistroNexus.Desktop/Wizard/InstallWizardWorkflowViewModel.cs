using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.Wizard.Steps;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DistroNexus.Desktop.Wizard;

/// <summary>
/// View model for the installation wizard dialog using workflow pattern.
/// </summary>
public partial class InstallWizardWorkflowViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IWslManagerService _wslManager;
    private readonly ISettingsService _settingsService;
    private readonly ITemplateService _templateService;
    private readonly ILogger<InstallWizardWorkflowViewModel> _logger;
    private InstallWizardStartupRequest? _startupRequest;

    [ObservableProperty]
    private WizardWorkflow _workflow;

    /// <summary>
    /// Event raised when the wizard is completed or cancelled.
    /// </summary>
    public event EventHandler<bool>? WizardCompleted;

    public InstallWizardWorkflowViewModel(
        ICatalogService catalogService,
        IWslManagerService wslManager,
        ISettingsService settingsService,
        ITemplateService templateService,
        ILogger<InstallWizardWorkflowViewModel> logger)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _workflow = CreateWorkflow();
    }

    private WizardWorkflow CreateWorkflow()
    {
        var workflow = new WizardWorkflow();

        // Subscribe to workflow completion
        workflow.Completed += OnWorkflowCompleted;

        // Add steps in order
        workflow.AddStep(new SelectDistributionStep(_catalogService, _templateService, _logger));
        workflow.AddStep(new InstallPathStep(_settingsService, _wslManager, _logger));
        workflow.AddStep(new UserConfigurationStep(_settingsService, _logger));
        workflow.AddStep(new SelectTemplateStep(_templateService, _logger));
        workflow.AddStep(new TemplateOptionsStep());
        workflow.AddStep(new ReviewStep());
        workflow.AddStep(new ProgressStep(_wslManager, _logger));
        workflow.AddStep(new TemplateApplyStep(_templateService, _logger));
        workflow.AddStep(new ResultStep());

        return workflow;
    }

    /// <summary>
    /// Sets optional startup request for wizard initialization.
    /// </summary>
    /// <param name="startupRequest">Startup payload.</param>
    public void SetStartupRequest(InstallWizardStartupRequest? startupRequest)
    {
        _startupRequest = startupRequest;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing install wizard workflow");

        Workflow.Context.StartupWarningMessage = string.Empty;

        await ApplyStartupRequestAsync();
        
        // If a distribution is already pre-selected (e.g. from Package Manager),
        // skip the selection step (Step 0) and start at the path step (Step 1).
        int startStep = Workflow.Context.SelectedDistribution != null ? 1 : 0;
        
        if (startStep > 0)
        {
            _logger.LogInformation("Distribution {Distro} pre-selected, skipping to step {Step}", 
                Workflow.Context.SelectedDistribution?.Name, startStep);
        }

        await Workflow.StartAsync(startStep);
    }

    private async Task ApplyStartupRequestAsync()
    {
        Workflow.Context.ApplyTemplateAfterInstall = false;

        if (_startupRequest == null)
        {
            return;
        }

        var warnings = new StringBuilder();

        if (Workflow.Context.SelectedDistribution == null &&
            !string.IsNullOrWhiteSpace(_startupRequest.SelectedDistributionId))
        {
            try
            {
                var packages = await _catalogService.LoadCatalogAsync();
                var selectedDistribution = packages.FirstOrDefault(package =>
                    string.Equals(package.Id, _startupRequest.SelectedDistributionId, StringComparison.OrdinalIgnoreCase));

                if (selectedDistribution != null)
                {
                    Workflow.Context.SelectedDistribution = selectedDistribution;
                }
                else
                {
                    _logger.LogWarning("Startup distribution payload was not found: {DistributionId}", _startupRequest.SelectedDistributionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve startup distribution payload: {DistributionId}", _startupRequest.SelectedDistributionId);
            }
        }

        if (!string.IsNullOrWhiteSpace(_startupRequest.TemplateId))
        {
            try
            {
                var template = await _templateService.GetTemplateByIdAsync(_startupRequest.TemplateId);
                if (template != null)
                {
                    Workflow.Context.SelectedTemplate = template;
                    Workflow.Context.ApplyTemplateAfterInstall = true;
                }
                else
                {
                    _logger.LogWarning("Startup template payload was not found: {TemplateId}", _startupRequest.TemplateId);
                    Workflow.Context.SelectedTemplate = null;
                    Workflow.Context.ApplyTemplateAfterInstall = false;
                    warnings.AppendLine(string.Format(Properties.Resources.WizardStartupTemplateNotFoundWarningFormat, _startupRequest.TemplateId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve startup template payload: {TemplateId}", _startupRequest.TemplateId);
                Workflow.Context.SelectedTemplate = null;
                Workflow.Context.ApplyTemplateAfterInstall = false;
                warnings.AppendLine(string.Format(Properties.Resources.WizardStartupTemplateLoadFailedWarningFormat, _startupRequest.TemplateId));
            }
        }

        Workflow.Context.StartupWarningMessage = warnings.ToString().Trim();
    }

    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("Install wizard cancelled");
        Workflow.Cancel();
    }

    private void OnWorkflowCompleted(object? sender, bool success)
    {
        _logger.LogInformation("Install wizard completed with success={Success}", success);
        WizardCompleted?.Invoke(this, success);
    }
}
