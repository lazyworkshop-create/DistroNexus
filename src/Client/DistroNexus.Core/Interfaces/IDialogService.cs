namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides dialog interaction for ViewModels in a testable and decoupled way.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows an informational/error alert and waits for the user to acknowledge.</summary>
    Task ShowAlertAsync(string title, string message);

    /// <summary>Shows a yes/no confirmation dialog and returns true if the user confirmed.</summary>
    Task<bool> ShowConfirmAsync(string title, string message);
}
