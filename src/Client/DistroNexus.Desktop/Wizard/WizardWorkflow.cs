using CommunityToolkit.Mvvm.ComponentModel;

namespace DistroNexus.Desktop.Wizard;

/// <summary>
/// Manages wizard step navigation and lifecycle.
/// </summary>
public partial class WizardWorkflow : ObservableObject
{
    private readonly List<IWizardStep> _steps = [];
    private int _currentIndex = -1;

    /// <summary>
    /// Gets the shared wizard context.
    /// </summary>
    public WizardContext Context { get; }

    /// <summary>
    /// Gets the list of all steps.
    /// </summary>
    public IReadOnlyList<IWizardStep> Steps => _steps;

    /// <summary>
    /// Gets the steps that should be shown in the step indicator.
    /// </summary>
    public IReadOnlyList<IWizardStep> IndicatorSteps => 
        _steps.Where(s => s.ShowInStepIndicator && !ShouldSkipStep(s)).ToList();

    /// <summary>
    /// Gets the total number of indicator steps.
    /// </summary>
    public int TotalIndicatorSteps => IndicatorSteps.Count;

    [ObservableProperty]
    private IWizardStep? _currentStep;

    [ObservableProperty]
    private int _currentStepNumber;

    [ObservableProperty]
    private string _currentStepTitle = string.Empty;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext;

    /// <summary>
    /// Event raised when the wizard is completed.
    /// </summary>
    public event EventHandler<bool>? Completed;

    public WizardWorkflow()
    {
        Context = new WizardContext();
    }

    /// <summary>
    /// Adds a step to the workflow.
    /// </summary>
    public void AddStep(WizardStepBase step)
    {
        step.Context = Context;
        step.Workflow = this;

        _steps.Add(step);
        RefreshStepNumbers();
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(IndicatorSteps));
        OnPropertyChanged(nameof(TotalIndicatorSteps));
    }

    /// <summary>
    /// Starts the workflow at the specified step index.
    /// </summary>
    public async Task StartAsync(int startIndex = 0)
    {
        if (_steps.Count == 0)
            return;

        if (startIndex < 0 || startIndex >= _steps.Count)
            startIndex = 0;

        _currentIndex = startIndex;

        while (_currentIndex < _steps.Count && ShouldSkipStep(_steps[_currentIndex]))
        {
            _currentIndex++;
        }

        if (_currentIndex >= _steps.Count)
        {
            return;
        }

        await NavigateToCurrentStepAsync();
    }

    /// <summary>
    /// Navigates to the previous step.
    /// </summary>
    public async Task GoBackAsync()
    {
        if (_currentIndex <= 0)
            return;

        if (CurrentStep != null)
        {
            await CurrentStep.OnExitAsync();
        }

        _currentIndex--;

        while (_currentIndex >= 0 && (ShouldSkipStep(_steps[_currentIndex]) || ShouldSkipForQuickInstall(_steps[_currentIndex])))
        {
            _currentIndex--;
        }

        if (_currentIndex < 0)
        {
            _currentIndex = 0;
        }

        await NavigateToCurrentStepAsync();
    }

    /// <summary>
    /// Synchronous wrapper for GoBackAsync.
    /// </summary>
    public void GoBack()
    {
        _ = GoBackAsync();
    }

    /// <summary>
    /// Navigates to the next step.
    /// </summary>
    public async Task GoNextAsync()
    {
        if (CurrentStep == null || _currentIndex >= _steps.Count - 1)
            return;

        if (!CurrentStep.Validate())
            return;

        await CurrentStep.OnExitAsync();

        _currentIndex++;

        while (_currentIndex < _steps.Count)
        {
            var step = _steps[_currentIndex];

            if (Context.UseQuickInstall && ShouldSkipForQuickInstall(step))
            {
                if (step is WizardStepBase wizardStep)
                {
                    await wizardStep.ApplyQuickInstallDefaultsAsync();
                }

                _currentIndex++;
                continue;
            }

            if (ShouldSkipStep(step))
            {
                _currentIndex++;
                continue;
            }

            break;
        }

        if (_currentIndex >= _steps.Count)
        {
            return;
        }

        await NavigateToCurrentStepAsync();
    }

    /// <summary>
    /// Synchronous wrapper for GoNextAsync.
    /// </summary>
    public void GoNext()
    {
        _ = GoNextAsync();
    }

    /// <summary>
    /// Navigates to a specific step by ID.
    /// </summary>
    public async Task GoToStepAsync(string stepId)
    {
        var index = _steps.FindIndex(s => s.StepId == stepId);
        if (index < 0)
            return;

        if (CurrentStep != null)
        {
            await CurrentStep.OnExitAsync();
        }

        _currentIndex = index;
        await NavigateToCurrentStepAsync();
    }

    /// <summary>
    /// Completes the wizard with success or failure.
    /// </summary>
    public void Complete(bool success)
    {
        Completed?.Invoke(this, success);
    }

    /// <summary>
    /// Cancels the wizard.
    /// </summary>
    public void Cancel()
    {
        Complete(false);
    }

    private async Task NavigateToCurrentStepAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _steps.Count)
            return;

        RefreshStepNumbers();

        CurrentStep = _steps[_currentIndex];
        CurrentStepNumber = CurrentStep.ShowInStepIndicator ? CurrentStep.StepNumber : 0;
        CurrentStepTitle = CurrentStep.Title;

        UpdateNavigationState();

        await CurrentStep.OnEnterAsync();

        RefreshStepNumbers();

        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IndicatorSteps));
        OnPropertyChanged(nameof(TotalIndicatorSteps));
        OnPropertyChanged(nameof(CurrentStepNumber));
    }

    private void UpdateNavigationState()
    {
        CanGoBack = _currentIndex > 0 && !Context.IsInstalling;
        CanGoNext = _currentIndex < _steps.Count - 1 && !Context.IsInstalling;
    }

    /// <summary>
    /// Refreshes the navigation state.
    /// </summary>
    public void RefreshNavigationState()
    {
        UpdateNavigationState();
        
        // Refresh current step buttons
        if (CurrentStep is WizardStepBase stepBase)
        {
            // Trigger property change for buttons
            OnPropertyChanged(nameof(CurrentStep));
        }
    }

    private bool ShouldSkipForQuickInstall(IWizardStep step)
    {
        return step.StepId == "install-path" ||
               step.StepId == "user-configuration" ||
               step.StepId == "review";
    }

    private bool ShouldSkipStep(IWizardStep step)
    {
        return step is WizardStepBase wizardStep && wizardStep.ShouldSkip(Context);
    }

    private void RefreshStepNumbers()
    {
        var index = 1;
        foreach (var step in _steps)
        {
            if (step.ShowInStepIndicator && !ShouldSkipStep(step))
            {
                step.StepNumber = index++;
            }
            else
            {
                step.StepNumber = 0;
            }
        }
    }
}
