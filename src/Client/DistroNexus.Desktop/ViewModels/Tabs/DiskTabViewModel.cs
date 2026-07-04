using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using System.IO;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Disk tab of InstanceDetailDialog.
/// Handles VHDX compaction, disk size display and related operations.
/// </summary>
public partial class DiskTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IWslManagerService _wslManager;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;
    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isCompacting;

    [ObservableProperty]
    private string _phaseText = string.Empty;

    [ObservableProperty]
    private bool _showResult;

    [ObservableProperty]
    private string _beforeSizeDisplay = string.Empty;

    [ObservableProperty]
    private string _afterSizeDisplay = string.Empty;

    [ObservableProperty]
    private string _savedSizeDisplay = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public bool IsWslV1 => !_instance.IsWslV2;

    public string VhdxPath => Path.Combine(_instance.InstallPath, "ext4.vhdx");

    public DiskTabViewModel(
        WslInstanceViewModel instance,
        IWslManagerService wslManager,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (!_instance.IsWslV2) return;

        IsLoading = true;
        try
        {
            await _instance.LoadDiskSizeAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CompactDiskAsync(CancellationToken ct)
    {
        if (!_instance.IsWslV2) return;

        // Step 1: whatIf phase to estimate reclaimable space
        IsCompacting = true;
        ShowResult = false;
        PhaseText = Properties.Resources.DiskTab_Estimating;
        _instance.IsBusy = true;

        string reclaimableEstimate = string.Empty;
        var estimateSucceeded = false;
        try
        {
            var whatIfProgress = new Progress<(double Percentage, string Message)>(p =>
            {
                if (!string.IsNullOrEmpty(p.Message))
                    reclaimableEstimate = p.Message;
            });

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await _wslManager.CompactInstanceAsync(_instance.Name, whatIfProgress, whatIf: true, _cts.Token);
            estimateSucceeded = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (WslOperationException ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
            return;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
            return;
        }
        finally
        {
            if (!estimateSucceeded)
            {
                IsCompacting = false;
                PhaseText = string.Empty;
                _instance.IsBusy = false;
            }
        }

        // Step 2: confirm dialog
        string confirmMsg = string.IsNullOrEmpty(reclaimableEstimate)
            ? Properties.Resources.DiskTab_CompactConfirm
            : string.Format(Properties.Resources.DiskTab_CompactConfirmEstimate, reclaimableEstimate);

        bool confirmed = await _dialogService.ShowConfirmAsync(
            Properties.Resources.DiskTab_CompactDisk, confirmMsg);
        if (!confirmed)
        {
            IsCompacting = false;
            _instance.IsBusy = false;
            return;
        }

        // Step 3: compaction — record before size then compact
        await RunCompactionAsync(_cts?.Token ?? ct);
    }

    /// <summary>
    /// Runs the actual compaction. Can be called from CompactDiskCommand or from bulk compaction in MainViewModel.
    /// </summary>
    public async Task RunCompactionAsync(CancellationToken ct = default)
    {
        try
        {
            IsCompacting = true;
            _instance.IsBusy = true;
            ShowResult = false;

            long before = await _wslManager.GetInstanceDiskSizeAsync(_instance.Name, ct);

            var progress = new Progress<(double Percentage, string Message)>(p =>
            {
                if (!string.IsNullOrEmpty(p.Message))
                    PhaseText = p.Message;
            });

            PhaseText = Properties.Resources.DiskTab_PhaseStop;
            await _wslManager.CompactInstanceAsync(_instance.Name, progress, whatIf: false, ct);

            long after = await _wslManager.GetInstanceDiskSizeAsync(_instance.Name, ct);
            _instance.UpdateDiskSize(after);

            BeforeSizeDisplay = FormatBytes(before);
            AfterSizeDisplay = FormatBytes(after);
            long saved = Math.Max(0, before - after);
            SavedSizeDisplay = FormatBytes(saved);
            ShowResult = true;
            PhaseText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            PhaseText = string.Empty;
        }
        catch (WslOperationException ex)
        {
            PhaseText = string.Empty;
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        catch (Exception ex)
        {
            PhaseText = string.Empty;
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsCompacting = false;
            _instance.IsBusy = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < units.Length - 1) { size /= 1024; order++; }
        return $"{size:0.##} {units[order]}";
    }
}
