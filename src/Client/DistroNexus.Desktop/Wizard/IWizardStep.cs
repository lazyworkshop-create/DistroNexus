using System.Windows.Controls;
using System.Windows.Input;

namespace DistroNexus.Desktop.Wizard;

/// <summary>
/// Represents a button action in a wizard step.
/// </summary>
public class WizardButtonAction
{
    /// <summary>
    /// Gets or sets the button content/label.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command to execute when the button is clicked.
    /// </summary>
    public ICommand? Command { get; set; }

    /// <summary>
    /// Gets or sets whether the button is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this is the primary button.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets the symbol icon name (optional).
    /// </summary>
    public string? IconSymbol { get; set; }
}

/// <summary>
/// Defines the contract for a wizard step.
/// </summary>
public interface IWizardStep
{
    /// <summary>
    /// Gets the step identifier.
    /// </summary>
    string StepId { get; }

    /// <summary>
    /// Gets the step title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the step description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the step number (1-based index for display).
    /// </summary>
    int StepNumber { get; set; }

    /// <summary>
    /// Gets whether this step is visible in the step indicator.
    /// </summary>
    bool ShowInStepIndicator { get; }

    /// <summary>
    /// Gets the UserControl content for this step.
    /// </summary>
    UserControl Content { get; }

    /// <summary>
    /// Gets the buttons to display for this step.
    /// </summary>
    IReadOnlyList<WizardButtonAction> Buttons { get; }

    /// <summary>
    /// Validates the current step.
    /// </summary>
    /// <returns>True if validation passes, false otherwise.</returns>
    bool Validate();

    /// <summary>
    /// Called when the step is entered.
    /// </summary>
    Task OnEnterAsync();

    /// <summary>
    /// Called when the step is exited.
    /// </summary>
    Task OnExitAsync();
}
