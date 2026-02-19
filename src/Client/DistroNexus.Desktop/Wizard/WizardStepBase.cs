using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard;

/// <summary>
/// Base class for wizard step view models.
/// </summary>
public abstract partial class WizardStepBase : ObservableObject, IWizardStep
{
    private UserControl? _content;
    private List<WizardButtonAction>? _buttons;

    /// <inheritdoc />
    public abstract string StepId { get; }

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public virtual string Description => string.Empty;

    /// <inheritdoc />
    [ObservableProperty]
    private int _stepNumber;

    /// <inheritdoc />
    public virtual bool ShowInStepIndicator => true;

    /// <summary>
    /// Gets a value indicating whether this step should use fullscreen log layout.
    /// </summary>
    public virtual bool IsLogFullscreen => false;

    /// <summary>
    /// Gets or sets the wizard context shared between steps.
    /// </summary>
    public WizardContext? Context { get; set; }

    /// <summary>
    /// Gets or sets the workflow that owns this step.
    /// </summary>
    public WizardWorkflow? Workflow { get; set; }

    /// <inheritdoc />
    public UserControl Content => _content ??= CreateContent();

    /// <inheritdoc />
    public IReadOnlyList<WizardButtonAction> Buttons => _buttons ??= CreateButtons();

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Creates the UserControl content for this step.
    /// </summary>
    protected abstract UserControl CreateContent();

    /// <summary>
    /// Creates the buttons for this step.
    /// </summary>
    protected virtual List<WizardButtonAction> CreateButtons()
    {
        var buttons = new List<WizardButtonAction>();

        // Default Back button (if not first step)
        if (StepNumber > 1)
        {
            buttons.Add(new WizardButtonAction
            {
                Content = Properties.Resources.ButtonBack,
                Command = new RelayCommand(() => Workflow?.GoBack()),
                IsVisible = true,
                IsPrimary = false
            });
        }

        // Default Next button
        buttons.Add(new WizardButtonAction
        {
            Content = Properties.Resources.ButtonNext,
            Command = new RelayCommand(() => Workflow?.GoNext()),
            IsVisible = true,
            IsPrimary = true
        });

        return buttons;
    }

    /// <summary>
    /// Refreshes the buttons collection.
    /// </summary>
    protected void RefreshButtons()
    {
        _buttons = CreateButtons();
        OnPropertyChanged(nameof(Buttons));
    }

    /// <inheritdoc />
    public virtual bool Validate()
    {
        ErrorMessage = string.Empty;
        return true;
    }

    /// <inheritdoc />
    public virtual Task OnEnterAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnExitAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies default values for this step when quick install mode is enabled.
    /// Override in derived classes to provide step-specific defaults.
    /// </summary>
    public virtual Task ApplyQuickInstallDefaultsAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether this step should be skipped for the current context.
    /// </summary>
    public virtual bool ShouldSkip(WizardContext context)
    {
        return false;
    }
}
