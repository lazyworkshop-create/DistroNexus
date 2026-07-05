using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DistroNexus.Desktop.Controls;

/// <summary>
/// A pill-shaped chip that displays a tag name with a remove button.
/// </summary>
public partial class TagChipControl : UserControl
{
    public static readonly DependencyProperty TagNameProperty =
        DependencyProperty.Register(nameof(TagName), typeof(string), typeof(TagChipControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RemoveTagCommandProperty =
        DependencyProperty.Register(nameof(RemoveTagCommand), typeof(ICommand), typeof(TagChipControl),
            new PropertyMetadata(null));

    public string TagName
    {
        get => (string)GetValue(TagNameProperty);
        set => SetValue(TagNameProperty, value);
    }

    public ICommand? RemoveTagCommand
    {
        get => (ICommand?)GetValue(RemoveTagCommandProperty);
        set => SetValue(RemoveTagCommandProperty, value);
    }

    public TagChipControl()
    {
        InitializeComponent();
    }
}
