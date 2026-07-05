using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Forms;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Import Instance dialog.
/// </summary>
public partial class ImportInstanceViewModel : ObservableObject
{
    private readonly IReadOnlyCollection<string> _existingNames;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameError))]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private string _instanceName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallPathError))]
    [NotifyPropertyChangedFor(nameof(HasInstallPathError))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private string _installPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourcePathError))]
    [NotifyPropertyChangedFor(nameof(HasSourcePathError))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private string _sourcePath = string.Empty;

    /// <summary>Set to true when the user confirms the dialog.</summary>
    public bool Confirmed { get; private set; }

    public string? NameError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(InstanceName))
                return Properties.Resources.Import_NameRequired;
            if (_existingNames.Contains(InstanceName.Trim(), StringComparer.OrdinalIgnoreCase))
                return Properties.Resources.Import_NameExists;
            return null;
        }
    }

    public string? InstallPathError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(InstallPath))
                return Properties.Resources.Import_PathRequired;
            return null;
        }
    }

    public string? SourcePathError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
                return Properties.Resources.Import_SourceRequired;
            if (!File.Exists(SourcePath))
                return Properties.Resources.Import_SourceNotFound;
            return null;
        }
    }

    public bool HasNameError => NameError is not null;
    public bool HasInstallPathError => InstallPathError is not null;
    public bool HasSourcePathError => SourcePathError is not null;

    public bool CanImport =>
        NameError is null && InstallPathError is null && SourcePathError is null;

    /// <summary>Event raised when dialog should close.</summary>
    public event EventHandler? CloseRequested;

    public ImportInstanceViewModel(IEnumerable<string> existingNames)
    {
        _existingNames = existingNames.ToList();
    }

    [RelayCommand]
    private void BrowseInstallPath()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = Properties.Resources.Import_PathBrowseTitle,
            UseDescriptionForTitle = true,
            SelectedPath = InstallPath
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            InstallPath = dlg.SelectedPath;
    }

    [RelayCommand]
    private void BrowseSource()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Properties.Resources.Import_SourceBrowseTitle,
            Filter = "TAR archive (*.tar)|*.tar|All files (*.*)|*.*",
            FileName = SourcePath
        };
        if (dlg.ShowDialog() == true)
            SourcePath = dlg.FileName;
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void Confirm()
    {
        Confirmed = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
