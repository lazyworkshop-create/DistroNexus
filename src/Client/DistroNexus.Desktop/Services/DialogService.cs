using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.Services;

/// <summary>
/// WPF UI implementation of <see cref="IDialogService"/> using Wpf.Ui MessageBox controls.
/// </summary>
public class DialogService : IDialogService
{
    public async Task ShowAlertAsync(string title, string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            MaxWidth = 460
        };
        await box.ShowDialogAsync();
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            MaxWidth = 460
        };
        var result = await box.ShowDialogAsync();
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }
}
