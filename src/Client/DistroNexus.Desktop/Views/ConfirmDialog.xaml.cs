using System.Windows;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Custom confirmation dialog with consistent styling.
/// </summary>
public partial class ConfirmDialog : FluentWindow
{
    public new string Title { get; set; } = "Confirm";
    public string Message { get; set; } = "Are you sure?";
    public string ConfirmButtonText { get; set; } = "OK";

    public ConfirmDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ConfirmDialog(string title, string message, string confirmButtonText = "OK")
    {
        InitializeComponent();
        
        Title = title;
        Message = message;
        ConfirmButtonText = confirmButtonText;
        
        DataContext = this;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Shows a confirmation dialog and returns the result.
    /// </summary>
    public static bool Show(string title, string message, string confirmButtonText = "OK", Window? owner = null)
    {
        var dialog = new ConfirmDialog(title, message, confirmButtonText)
        {
            Owner = owner ?? Application.Current.MainWindow
        };
        
        return dialog.ShowDialog() == true;
    }
}
