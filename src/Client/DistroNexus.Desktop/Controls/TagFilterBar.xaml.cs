using System.Windows.Controls;

namespace DistroNexus.Desktop.Controls;

/// <summary>
/// Tag filter bar displayed on the dashboard above the instance list.
/// Collapses automatically when no tags exist.
/// </summary>
public partial class TagFilterBar : UserControl
{
    public TagFilterBar()
    {
        InitializeComponent();
    }
}
