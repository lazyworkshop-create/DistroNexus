using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Controls;

/// <summary>
/// A reusable confirmation dialog for user confirmations.
/// </summary>
public partial class ConfirmationDialog : FluentWindow
{
    #region Dependency Properties

    public static readonly DependencyProperty DialogTitleProperty =
        DependencyProperty.Register(nameof(DialogTitle), typeof(string), typeof(ConfirmationDialog),
            new PropertyMetadata("Confirmation"));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ConfirmationDialog),
            new PropertyMetadata("Are you sure you want to continue?"));

    public static readonly DependencyProperty PrimaryButtonTextProperty =
        DependencyProperty.Register(nameof(PrimaryButtonText), typeof(string), typeof(ConfirmationDialog),
            new PropertyMetadata("Yes"));

    public static readonly DependencyProperty SecondaryButtonTextProperty =
        DependencyProperty.Register(nameof(SecondaryButtonText), typeof(string), typeof(ConfirmationDialog),
            new PropertyMetadata("No"));

    public static readonly DependencyProperty PrimaryButtonAppearanceProperty =
        DependencyProperty.Register(nameof(PrimaryButtonAppearance), typeof(ControlAppearance), typeof(ConfirmationDialog),
            new PropertyMetadata(ControlAppearance.Primary));

    public static readonly DependencyProperty DialogTypeProperty =
        DependencyProperty.Register(nameof(DialogType), typeof(ConfirmationDialogType), typeof(ConfirmationDialog),
            new PropertyMetadata(ConfirmationDialogType.Question, OnDialogTypeChanged));

    public static readonly DependencyProperty AdditionalContentProperty =
        DependencyProperty.Register(nameof(AdditionalContent), typeof(object), typeof(ConfirmationDialog),
            new PropertyMetadata(null));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the dialog title.
    /// </summary>
    public string DialogTitle
    {
        get => (string)GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the message to display.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the primary button text.
    /// </summary>
    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the secondary button text.
    /// </summary>
    public string SecondaryButtonText
    {
        get => (string)GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the primary button appearance.
    /// </summary>
    public ControlAppearance PrimaryButtonAppearance
    {
        get => (ControlAppearance)GetValue(PrimaryButtonAppearanceProperty);
        set => SetValue(PrimaryButtonAppearanceProperty, value);
    }

    /// <summary>
    /// Gets or sets the dialog type which determines the icon and styling.
    /// </summary>
    public ConfirmationDialogType DialogType
    {
        get => (ConfirmationDialogType)GetValue(DialogTypeProperty);
        set => SetValue(DialogTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets additional content to display in the dialog.
    /// </summary>
    public object? AdditionalContent
    {
        get => GetValue(AdditionalContentProperty);
        set => SetValue(AdditionalContentProperty, value);
    }

    #endregion

    public ConfirmationDialog()
    {
        InitializeComponent();
        UpdateIconForType(DialogType);
    }

    private static void OnDialogTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConfirmationDialog dialog && e.NewValue is ConfirmationDialogType type)
        {
            dialog.UpdateIconForType(type);
        }
    }

    private void UpdateIconForType(ConfirmationDialogType type)
    {
        (DialogIcon.Symbol, PrimaryButtonAppearance) = type switch
        {
            ConfirmationDialogType.Question => (SymbolRegular.Question48, ControlAppearance.Primary),
            ConfirmationDialogType.Warning => (SymbolRegular.Warning48, ControlAppearance.Caution),
            ConfirmationDialogType.Danger => (SymbolRegular.ErrorCircle48, ControlAppearance.Danger),
            ConfirmationDialogType.Info => (SymbolRegular.Info48, ControlAppearance.Info),
            _ => (SymbolRegular.Question48, ControlAppearance.Primary)
        };
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #region Static Factory Methods

    /// <summary>
    /// Shows a simple confirmation dialog.
    /// </summary>
    public static bool? Show(Window owner, string title, string message, 
        ConfirmationDialogType type = ConfirmationDialogType.Question)
    {
        var dialog = new ConfirmationDialog
        {
            Owner = owner,
            Title = "Confirmation",
            DialogTitle = title,
            Message = message,
            DialogType = type
        };

        return dialog.ShowDialog();
    }

    /// <summary>
    /// Shows a confirmation dialog with custom button text.
    /// </summary>
    public static bool? Show(Window owner, string title, string message,
        string primaryButtonText, string secondaryButtonText,
        ConfirmationDialogType type = ConfirmationDialogType.Question)
    {
        var dialog = new ConfirmationDialog
        {
            Owner = owner,
            Title = "Confirmation",
            DialogTitle = title,
            Message = message,
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            DialogType = type
        };

        return dialog.ShowDialog();
    }

    /// <summary>
    /// Shows a delete confirmation dialog.
    /// </summary>
    public static bool? ShowDelete(Window owner, string itemName)
    {
        return Show(owner, 
            "Delete Confirmation",
            $"Are you sure you want to delete '{itemName}'?\n\nThis action cannot be undone.",
            "Delete", "Cancel",
            ConfirmationDialogType.Danger);
    }

    /// <summary>
    /// Shows a warning confirmation dialog.
    /// </summary>
    public static bool? ShowWarning(Window owner, string title, string message)
    {
        return Show(owner, title, message, "Continue", "Cancel", ConfirmationDialogType.Warning);
    }

    #endregion
}

/// <summary>
/// Types of confirmation dialogs.
/// </summary>
public enum ConfirmationDialogType
{
    /// <summary>
    /// A general question dialog.
    /// </summary>
    Question,

    /// <summary>
    /// A warning dialog.
    /// </summary>
    Warning,

    /// <summary>
    /// A danger/destructive action dialog.
    /// </summary>
    Danger,

    /// <summary>
    /// An informational dialog.
    /// </summary>
    Info
}
