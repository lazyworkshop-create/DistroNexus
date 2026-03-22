using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.Desktop.Views;

/// <summary>
/// Code-behind for ImportInstanceDialog.
/// </summary>
public partial class ImportInstanceDialog
{
    public ImportInstanceDialog(ImportInstanceViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, _) => Close();
    }
}
