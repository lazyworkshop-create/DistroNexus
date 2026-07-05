using System.Windows.Controls;

namespace DistroNexus.Desktop.Controls;

/// <summary>
/// Manage Tags section for the Settings page.
/// </summary>
public partial class ManageTagsSectionView : UserControl
{
    public ManageTagsSectionView()
    {
        InitializeComponent();
    }

    private async void OnExpanded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ManageTagsViewModel vm)
            await vm.LoadAsync();
    }

    // Start inline rename when the edit button is clicked
    private void OnStartRename(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button btn && btn.DataContext is ViewModels.TagItemViewModel item)
            item.IsRenaming = true;
    }
}
