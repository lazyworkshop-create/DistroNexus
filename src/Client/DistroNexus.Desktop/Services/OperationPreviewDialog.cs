using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.Services;

/// <summary>Shared presentation boundary for destructive-operation previews. It keeps effects and
/// warnings together and deliberately delegates confirmation to the application's dialog service.</summary>
public static class OperationPreviewDialog
{
    public static Task<bool> ShowAsync(IDialogService dialogs, string title, IEnumerable<string> effects, IEnumerable<string> warnings) =>
        dialogs.ShowConfirmAsync(title, string.Join(Environment.NewLine, effects.Concat(warnings)));
}
