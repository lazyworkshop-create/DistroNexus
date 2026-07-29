using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Import Instance dialog.
/// </summary>
public partial class ImportInstanceViewModel : ObservableObject
{
    private readonly IReadOnlyCollection<string> _existingNames;
    private readonly IPowerShellModuleClient _moduleClient;
    private string? _sourcePreviewError;

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

    /// <summary>Gets the lifecycle preview obtained during confirmation.</summary>
    public LifecycleOperationPreview? ImportPreview { get; private set; }

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
            return _sourcePreviewError;
        }
    }

    public bool HasNameError => NameError is not null;
    public bool HasInstallPathError => InstallPathError is not null;
    public bool HasSourcePathError => SourcePathError is not null;

    public bool CanImport =>
        NameError is null && InstallPathError is null && SourcePathError is null;

    /// <summary>Event raised when dialog should close.</summary>
    public event EventHandler? CloseRequested;

    public ImportInstanceViewModel(IEnumerable<string> existingNames, IPowerShellModuleClient moduleClient)
    {
        _existingNames = existingNames.ToList();
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
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
    private async Task ConfirmAsync()
    {
        _sourcePreviewError = null;
        OnPropertyChanged(nameof(SourcePathError));
        OnPropertyChanged(nameof(HasSourcePathError));

        try
        {
            ImportPreview = await _moduleClient.PreviewImportInstanceAsync(
                InstanceName.Trim(),
                SourcePath.Trim(),
                InstallPath.Trim());
            Confirmed = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            ImportPreview = null;
            _sourcePreviewError = Properties.Resources.Import_SourceNotFound;
            OnPropertyChanged(nameof(SourcePathError));
            OnPropertyChanged(nameof(HasSourcePathError));
            OnPropertyChanged(nameof(CanImport));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSourcePathChanged(string value)
    {
        ImportPreview = null;
        _sourcePreviewError = null;
        OnPropertyChanged(nameof(SourcePathError));
        OnPropertyChanged(nameof(HasSourcePathError));
        OnPropertyChanged(nameof(CanImport));
    }
}
