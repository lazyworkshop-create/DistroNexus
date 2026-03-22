using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Backup tab of InstanceDetailDialog.
/// Handles backup schedule, manual backup and history display.
/// </summary>
public partial class BackupTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IBackupService _backupService;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public BackupTabViewModel(
        WslInstanceViewModel instance,
        IBackupService backupService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;
        // Backup tab initialization will be implemented in Phase 5
        return Task.CompletedTask;
    }
}
