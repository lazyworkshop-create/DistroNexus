using DistroNexus.Core.Interfaces;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DistroNexus.Desktop.Services;

/// <summary>
/// WPF UI implementation of <see cref="IDialogService"/> using Wpf.Ui MessageBox controls.
/// </summary>
public class DialogService : IDialogService
{
    private static readonly Regex ErrorCodePattern =
        new(@"\[DN-(\d+)\]", RegexOptions.Compiled);

    public async Task ShowAlertAsync(string title, string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = BuildContent(message),
            CloseButtonText = Properties.Resources.ButtonClose,
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
            PrimaryButtonText = Properties.Resources.ButtonOK,
            CloseButtonText = Properties.Resources.ButtonCancel,
            MaxWidth = 460
        };
        var result = await box.ShowDialogAsync();
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    private static object BuildContent(string message)
    {
        var match = ErrorCodePattern.Match(message);
        if (!match.Success)
            return message;

        var code = match.Value;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });

        var link = new Hyperlink(new Run(string.Format(Properties.Resources.ErrorCopyCode, code)));
        link.Click += (_, _) => Clipboard.SetText(code);

        var linkBlock = new TextBlock { Margin = new Thickness(0, 8, 0, 0) };
        linkBlock.Inlines.Add(link);
        panel.Children.Add(linkBlock);
        return panel;
    }
}
