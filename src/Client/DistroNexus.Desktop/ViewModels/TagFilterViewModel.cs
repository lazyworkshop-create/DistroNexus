using CommunityToolkit.Mvvm.ComponentModel;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// Represents a single tag entry in the tag filter bar with selection state.
/// </summary>
public partial class TagFilterViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
