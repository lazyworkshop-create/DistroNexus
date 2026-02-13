using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.Wizard.Steps;
using Microsoft.Extensions.Logging;

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
        workflow.AddStep(new SelectDistributionStep(_catalogService, _logger));
        workflow.AddStep(new InstallPathStep(_settingsService, _wslManager, _logger));
        workflow.AddStep(new UserConfigurationStep(_settingsService, _logger));
        workflow.AddStep(new SelectTemplateStep(_templateService, _logger));
        workflow.AddStep(new ReviewStep());
        workflow.AddStep(new ProgressStep(_wslManager, _templateService, _logger));
        workflow.AddStep(new ResultStep());

        return workflow;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing install wizard workflow");
        
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
