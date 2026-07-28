using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using DistroNexus.Core.Exceptions;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// Backup frequency options for the schedule editor.
/// </summary>
public enum BackupFrequency { Daily, Weekly, Monthly }
public enum BackupHistoryFilter { All, Scheduled, RecoveryPoints, Failures }

/// <summary>
/// ViewModel for the Backup tab of InstanceDetailDialog.
/// Handles backup schedule editor, manual backup invocation, and history list (D-01).
/// </summary>
public partial class BackupTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IBackupService _backupService;
    private readonly IDialogService _dialogService;
    private readonly IRecoveryPointService _recoveryService;
    private readonly IPowerShellModuleClient _moduleClient;

    private bool _initialized;

    // ── Static ComboBox data ────────────────────────────────────────────────────

    public static IReadOnlyList<BackupFrequency> FrequencyOptions { get; } =
        [BackupFrequency.Daily, BackupFrequency.Weekly, BackupFrequency.Monthly];

    public static IReadOnlyList<string> DaysOfWeek { get; } =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public static IReadOnlyList<int> DaysOfMonth { get; } =
        [.. Enumerable.Range(1, 31)];
    public static IReadOnlyList<BackupHistoryFilter> HistoryFilters { get; } = [BackupHistoryFilter.All, BackupHistoryFilter.Scheduled, BackupHistoryFilter.RecoveryPoints, BackupHistoryFilter.Failures];
    public ObservableCollection<RecoveryPointFormat> RecoveryFormats { get; } = [RecoveryPointFormat.Tar];

    // ── Observable form fields ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBackingUp;

    [ObservableProperty]
    private bool _hasSchedule;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDayOfWeekPicker))]
    [NotifyPropertyChangedFor(nameof(ShowDayOfMonthPicker))]
    [NotifyPropertyChangedFor(nameof(IsFormEnabled))]
    private BackupFrequency _frequency = BackupFrequency.Daily;

    [ObservableProperty]
    private string _selectedDayOfWeek = "Monday";

    [ObservableProperty]
    private int _selectedDayOfMonth = 1;

    /// <summary>Backup time in "HH:mm" format.</summary>
    [ObservableProperty]
    private string _backupTimeText = "02:00";

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormEnabled))]
    private int _retentionCount = 7;

    [ObservableProperty]
    private ObservableCollection<BackupHistoryEntry> _backupHistory = [];
    [ObservableProperty] private BackupHistoryFilter _selectedHistoryFilter;

    [ObservableProperty] private ObservableCollection<RecoveryPointSummary> _recoveryPoints = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImportInPlace))]
    [NotifyPropertyChangedFor(nameof(CanRestoreSelectedRecoveryPoint))]
    private RecoveryPointSummary? _selectedRecoveryPoint;
    [ObservableProperty] private string _recoveryName = Properties.Resources.ResourceManager.GetString("Recovery_DefaultName") ?? string.Empty;
    [ObservableProperty] private string _recoveryDescription = string.Empty;
    [ObservableProperty] private string _recoveryTags = string.Empty;
    [ObservableProperty] private string _recoveryTargetInstance = string.Empty;
    [ObservableProperty] private string _recoveryTargetDirectory = string.Empty;
    [ObservableProperty] private RecoveryPointFormat _recoveryFormat = RecoveryPointFormat.Tar;
    [ObservableProperty] private bool _stopAndRestartForRecovery;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImportInPlace))]
    [NotifyPropertyChangedFor(nameof(CanManageTargetDirectory))]
    private bool _recoveryImportInPlace;
    [ObservableProperty] private bool _recoveryPinned;
    [ObservableProperty] private int _recoveryRetention = 7;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImportInPlace))]
    [NotifyPropertyChangedFor(nameof(CanRestoreSelectedRecoveryPoint))]
    private bool _canUseVhdx;
    [ObservableProperty] private bool _isRecovering;
    [ObservableProperty] private string _recoveryOperationStatus = string.Empty;
    [ObservableProperty] private string _recoveryDiagnosticDetails = string.Empty;
    private CancellationTokenSource? _recoveryOperationCts;

    // ── Computed ───────────────────────────────────────────────────────────────

    public bool ShowDayOfWeekPicker => Frequency == BackupFrequency.Weekly;
    public bool ShowDayOfMonthPicker => Frequency == BackupFrequency.Monthly;
    public bool IsFormEnabled => !IsLoading && !IsBackingUp;
    public bool CanImportInPlace => CanUseVhdx && SelectedRecoveryPoint?.Manifest.Format == RecoveryPointFormat.Vhdx;
    /// <summary>Import-in-place intentionally lets WSL own the registration location.</summary>
    public bool CanManageTargetDirectory => !RecoveryImportInPlace;
    /// <summary>VHDX restore and clone require the same capability preflight as VHDX creation.</summary>
    public bool CanRestoreSelectedRecoveryPoint => SelectedRecoveryPoint?.Manifest.Format != RecoveryPointFormat.Vhdx || CanUseVhdx;

    public WslInstanceViewModel Instance => _instance;

    // ── Constructor ────────────────────────────────────────────────────────────

    public BackupTabViewModel(
        WslInstanceViewModel instance,
        IBackupService backupService,
        IDialogService dialogService, IRecoveryPointService recoveryService, IPowerShellModuleClient moduleClient)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _recoveryService = recoveryService ?? throw new ArgumentNullException(nameof(recoveryService));
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
    }

    // ── Initialization ─────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            var schedules = await _backupService.GetSchedulesAsync();
            var schedule = schedules.FirstOrDefault(s =>
                string.Equals(s.Name, _instance.Name, StringComparison.OrdinalIgnoreCase));

            if (schedule is not null)
            {
                HasSchedule = true;
                ParseFrequency(schedule.Frequency);
                BackupTimeText = $"{schedule.Time.Hours:D2}:{schedule.Time.Minutes:D2}";
                DestinationPath = schedule.Destination;
                RetentionCount = schedule.RetentionCount;

            }
            // History and recovery records are independent of an optional schedule or its
            // destination. A removed schedule must not hide manual recovery evidence.
            await LoadBackupHistoryAsync();
            await LoadRecoveryPointsAsync();
            RecoveryRetention = await _recoveryService.GetRetentionAsync(_instance.Name) ?? RecoveryRetention;
            await RefreshVhdxCapabilityAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
            _instance.IsBusy = false;
        }
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                Properties.Resources.BackupTab_NoDestination);
            return;
        }

        if (RetentionCount < 1 || RetentionCount > 30)
            RetentionCount = Math.Max(1, Math.Min(30, RetentionCount));

        if (!TimeSpan.TryParse(BackupTimeText, out var time))
            time = new TimeSpan(2, 0, 0);

        var schedule = new BackupSchedule
        {
            Name = _instance.Name,
            Destination = DestinationPath,
            Frequency = EncodeFrequency(),
            RetentionCount = RetentionCount,
            Time = time
        };

        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            await _backupService.SaveScheduleAsync(schedule);
            HasSchedule = true;
            await _dialogService.ShowAlertAsync(
                Properties.Resources.SuccessTitle,
                Properties.Resources.BackupTab_SavedSchedule);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
            _instance.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveScheduleAsync()
    {
        var confirmed = await _dialogService.ShowConfirmAsync(
            Properties.Resources.BackupTab_RemoveSchedule,
            string.Format(Properties.Resources.BackupTab_RemoveConfirm, _instance.Name));

        if (!confirmed) return;

        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            await _backupService.RemoveScheduleAsync(_instance.Name);
            HasSchedule = false;
            await _dialogService.ShowAlertAsync(
                Properties.Resources.SuccessTitle,
                Properties.Resources.BackupTab_ScheduleRemoved);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
            _instance.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                Properties.Resources.BackupTab_NoDestination);
            return;
        }

        IsBackingUp = true;
        _instance.IsBusy = true;
        OnPropertyChanged(nameof(IsFormEnabled));
        try
        {
            await _backupService.InvokeBackupAsync(
                _instance.Name,
                DestinationPath,
                RetentionCount);

            await _dialogService.ShowAlertAsync(
                Properties.Resources.SuccessTitle,
                Properties.Resources.BackupTab_BackupComplete);

            await LoadBackupHistoryAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsBackingUp = false;
            _instance.IsBusy = false;
            OnPropertyChanged(nameof(IsFormEnabled));
        }
    }

    [RelayCommand]
    private async Task CreateRecoveryPointAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath)) { await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle, Properties.Resources.BackupTab_NoDestination); return; }
        using var operation = BeginRecoveryOperation("Recovery_CreatePreparing");
        try
        {
            var request = new RecoveryPointCreateRequest(_instance.Name, RecoveryName, DestinationPath, RecoveryFormat, RecoveryDescription, ParseTags(), StopAndRestartForRecovery);
            var preview = await _recoveryService.PreviewCreateAsync(request, operation.Token);
            var warning = preview.Warnings.Count == 0 ? "" : "\n\n" + string.Join("\n", preview.Warnings);
            if (!await _dialogService.ShowConfirmAsync(L("Recovery_CreateTitle"), L("Recovery_CreateConfirm") + warning)) return;
            await _recoveryService.CreateAsync(request, preview.Token, operation.Token, RecoveryProgress());
            await LoadRecoveryPointsAsync(); await LoadBackupHistoryAsync();
        }
        catch (Exception ex) { await ShowRecoveryErrorAsync(ex); }
        finally { CompleteRecoveryOperation(operation); }
    }

    [RelayCommand]
    private async Task VerifyRecoveryPointAsync()
    {
        if (SelectedRecoveryPoint is null) return;
        var result = await _recoveryService.VerifyAsync(SelectedRecoveryPoint.Manifest.Id);
        await _dialogService.ShowAlertAsync(L("Recovery_VerifyTitle"), result.ToString());
        await LoadRecoveryPointsAsync();
    }

    [RelayCommand]
    private async Task DeleteRecoveryPointAsync()
    {
        if (SelectedRecoveryPoint is null) return;
        var preview = await _recoveryService.PreviewDeleteAsync(SelectedRecoveryPoint.Manifest.Id);
        var confirmation = string.Join(Environment.NewLine, [L("Recovery_DeleteConfirm"), .. preview.Warnings]);
        if (!await _dialogService.ShowConfirmAsync(L("Recovery_DeleteTitle"), confirmation)) return;
        await _recoveryService.DeleteAsync(SelectedRecoveryPoint.Manifest.Id, preview.Token); await LoadRecoveryPointsAsync(); await LoadBackupHistoryAsync();
    }

    [RelayCommand]
    private async Task RestoreRecoveryPointAsync()
    {
        if (SelectedRecoveryPoint is null || string.IsNullOrWhiteSpace(RecoveryTargetInstance)
            || (!RecoveryImportInPlace && string.IsNullOrWhiteSpace(RecoveryTargetDirectory))) return;
        using var operation = BeginRecoveryOperation("Recovery_RestorePreparing");
        try
        {
            var request = new RecoveryRestoreRequest(SelectedRecoveryPoint.Manifest.Id, RecoveryTargetInstance.Trim(), RecoveryImportInPlace ? "" : RecoveryTargetDirectory.Trim(), true, RecoveryImportInPlace);
            var preview = await _recoveryService.PreviewRestoreAsync(request, operation.Token);
            if (!await _dialogService.ShowConfirmAsync(L("Recovery_RestoreTitle"), string.Join("\n", preview.Warnings))) return;
            await _recoveryService.RestoreAsync(request, preview.Token, operation.Token, RecoveryProgress());
            await _dialogService.ShowAlertAsync(Properties.Resources.SuccessTitle, L("Recovery_RestoreComplete"));
        }
        catch (Exception ex) { await ShowRecoveryErrorAsync(ex); }
        finally { CompleteRecoveryOperation(operation); }
    }

    [RelayCommand]
    private async Task CloneRecoveryPointAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath) || string.IsNullOrWhiteSpace(RecoveryTargetInstance)
            || (!RecoveryImportInPlace && string.IsNullOrWhiteSpace(RecoveryTargetDirectory))) return;
        using var operation = BeginRecoveryOperation("Recovery_ClonePreparing");
        try
        {
            var request = new RecoveryCloneRequest(new(_instance.Name, RecoveryName, DestinationPath, RecoveryFormat, RecoveryDescription, ParseTags(), StopAndRestartForRecovery), RecoveryTargetInstance.Trim(), RecoveryImportInPlace ? "" : RecoveryTargetDirectory.Trim(), RecoveryImportInPlace);
            var preview = await _recoveryService.PreviewCloneAsync(request, operation.Token);
            if (!await _dialogService.ShowConfirmAsync(L("Recovery_CloneTitle"), string.Join("\n", preview.Warnings))) return;
            await _recoveryService.RestoreCloneAsync(request, preview.Token, operation.Token, RecoveryProgress());
            await LoadRecoveryPointsAsync(); await LoadBackupHistoryAsync();
        }
        catch (Exception ex) { await ShowRecoveryErrorAsync(ex); }
        finally { CompleteRecoveryOperation(operation); }
    }

    [RelayCommand]
    private async Task SaveRecoveryNotesAsync()
    {
        if (SelectedRecoveryPoint is null) return;
        await _recoveryService.UpdateNotesAsync(SelectedRecoveryPoint.Manifest.Id, RecoveryDescription, ParseTags(), RecoveryPinned);
        await LoadRecoveryPointsAsync();
    }

    [RelayCommand]
    private async Task ApplyRecoveryRetentionAsync()
    {
        if (RecoveryRetention < 1) RecoveryRetention = 1;
        await _recoveryService.ApplyRetentionAsync(_instance.Name, RecoveryRetention);
        await LoadRecoveryPointsAsync(); await LoadBackupHistoryAsync();
    }

    partial void OnSelectedRecoveryPointChanged(RecoveryPointSummary? value)
    {
        if (value is not null)
        {
            RecoveryDescription = value.Manifest.Description;
            RecoveryTags = string.Join(", ", value.Manifest.Tags);
            RecoveryPinned = value.Manifest.Pinned;
        }
        if (value?.Manifest.Format != RecoveryPointFormat.Vhdx) RecoveryImportInPlace = false;
        OnPropertyChanged(nameof(CanImportInPlace));
    }
    partial void OnRecoveryImportInPlaceChanged(bool value)
    {
        if (value) RecoveryTargetDirectory = string.Empty;
        OnPropertyChanged(nameof(CanManageTargetDirectory));
    }
    partial void OnDestinationPathChanged(string value) => _ = RefreshVhdxCapabilityAsync();
    partial void OnRecoveryFormatChanged(RecoveryPointFormat value)
    {
        if (value == RecoveryPointFormat.Vhdx && !CanUseVhdx) RecoveryFormat = RecoveryPointFormat.Tar;
    }
    partial void OnCanUseVhdxChanged(bool value)
    {
        if (value && !RecoveryFormats.Contains(RecoveryPointFormat.Vhdx)) RecoveryFormats.Add(RecoveryPointFormat.Vhdx);
        if (!value)
        {
            RecoveryFormats.Remove(RecoveryPointFormat.Vhdx);
            RecoveryFormat = RecoveryPointFormat.Tar;
            RecoveryImportInPlace = false;
        }
        OnPropertyChanged(nameof(CanImportInPlace));
    }

    [RelayCommand]
    private async Task RevealRecoveryPointAsync()
    {
        if (SelectedRecoveryPoint is null) return;
        try { await _moduleClient.OpenRecoveryPointFolderAsync(SelectedRecoveryPoint.Manifest.Id); }
        catch (Exception ex) { await ShowRecoveryErrorAsync(ex); }
    }

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Properties.Resources.BackupTab_SelectDestinationTitle
        };

        if (dialog.ShowDialog() == true)
            DestinationPath = dialog.FolderName;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void ParseFrequency(string frequency)
    {
        if (frequency.StartsWith("Weekly:", StringComparison.OrdinalIgnoreCase))
        {
            Frequency = BackupFrequency.Weekly;
            var day = frequency["Weekly:".Length..];
            if (DaysOfWeek.Contains(day, StringComparer.OrdinalIgnoreCase))
                SelectedDayOfWeek = day;
        }
        else if (frequency.StartsWith("Monthly:", StringComparison.OrdinalIgnoreCase))
        {
            Frequency = BackupFrequency.Monthly;
            if (int.TryParse(frequency["Monthly:".Length..], out var d) && d >= 1 && d <= 31)
                SelectedDayOfMonth = d;
        }
        else
        {
            Frequency = BackupFrequency.Daily;
        }
    }

    private string EncodeFrequency() => Frequency switch
    {
        BackupFrequency.Weekly  => $"Weekly:{SelectedDayOfWeek}",
        BackupFrequency.Monthly => $"Monthly:{SelectedDayOfMonth}",
        _                       => "Daily"
    };
    private IReadOnlyList<string> ParseTags() => RecoveryTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string L(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
    private CancellationTokenSource BeginRecoveryOperation(string initialStatus)
    {
        _recoveryOperationCts?.Cancel(); _recoveryOperationCts?.Dispose();
        _recoveryOperationCts = new CancellationTokenSource();
        IsRecovering = true; RecoveryOperationStatus = L(initialStatus); RecoveryDiagnosticDetails = string.Empty;
        return _recoveryOperationCts;
    }
    private IProgress<RecoveryOperationProgress> RecoveryProgress() => new Progress<RecoveryOperationProgress>(p => RecoveryOperationStatus = L($"Recovery_Status_{p.Stage}"));
    private void CompleteRecoveryOperation(CancellationTokenSource operation)
    {
        if (ReferenceEquals(_recoveryOperationCts, operation)) { _recoveryOperationCts = null; IsRecovering = false; }
    }
    private async Task ShowRecoveryErrorAsync(Exception ex)
    {
        // Recovery adapters can surface filesystem/process exceptions.  Keep the UI's
        // diagnostic contract stable even when their implementation-specific text varies.
        RecoveryDiagnosticDetails = ex is WslOperationException
            ? MainViewModel.FormatAlertMessage(ex)
            : $"[DN-{(int)DistroNexusErrorCode.RecoveryOperationFailed:D4}] {MainViewModel.FormatAlertMessage(ex)}";
        RecoveryOperationStatus = ex is WslOperationException { Code: DistroNexusErrorCode.RecoveryManualRecoveryRequired }
            ? L("Recovery_ManualRecoveryRequired")
            : ex is OperationCanceledException ? L("Recovery_Cancelled") : L("Recovery_Failed");
        await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle, RecoveryDiagnosticDetails);
    }
    [RelayCommand]
    private void CancelRecoveryOperation() => _recoveryOperationCts?.Cancel();
    [RelayCommand]
    private void CopyRecoveryDiagnostic()
    {
        if (!string.IsNullOrWhiteSpace(RecoveryDiagnosticDetails)) System.Windows.Clipboard.SetText(RecoveryDiagnosticDetails);
    }
    partial void OnIsRecoveringChanged(bool value) { if (!value) RecoveryOperationStatus = string.Empty; }

    internal async Task LoadBackupHistoryAsync()
    {
        try
        {
            var entries = (await _recoveryService.GetHistoryAsync())
                .Where(x => string.Equals(x.InstanceName, _instance.Name, StringComparison.OrdinalIgnoreCase))
                .Select(x => new BackupHistoryEntry { Timestamp = x.CreatedAt, FilePath = x.Location ?? string.Empty,
                    ErrorMessage = x.Kind == "RecoveryPoint" ? L("Recovery_HistoryPoint") : x.Kind == "ScheduledBackup" ? L("Recovery_HistoryScheduled") : x.Status == "Failed" ? L("BackupTab_StatusFailed") : string.Empty,
                    IsSuccess = x.Status != "Failed", Kind = x.Kind }).ToList();

            entries = SelectedHistoryFilter switch
            {
                BackupHistoryFilter.Scheduled => entries.Where(x => x.Kind == "ScheduledBackup").ToList(),
                BackupHistoryFilter.RecoveryPoints => entries.Where(x => x.Kind == "RecoveryPoint").ToList(),
                BackupHistoryFilter.Failures => entries.Where(x => !x.IsSuccess).ToList(),
                _ => entries
            };

            BackupHistory = new ObservableCollection<BackupHistoryEntry>(
                entries
                    .OrderByDescending(e => e.Timestamp)
                    .Take(10));
        }
        catch
        {
            BackupHistory = [];
        }
    }

    partial void OnSelectedHistoryFilterChanged(BackupHistoryFilter value) => _ = LoadBackupHistoryAsync();

    private async Task LoadRecoveryPointsAsync()
    {
        RecoveryPoints = new ObservableCollection<RecoveryPointSummary>((await _recoveryService.ListAsync()).Where(x => string.Equals(x.Manifest.SourceInstance, _instance.Name, StringComparison.OrdinalIgnoreCase)));
    }
    private async Task RefreshVhdxCapabilityAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath)) { DisableVhdxRecoveryOptions(); return; }
        try
        {
            await _recoveryService.PreviewCreateAsync(new RecoveryPointCreateRequest(_instance.Name, "capability-probe", DestinationPath, RecoveryPointFormat.Vhdx));
            CanUseVhdx = true;
        }
        catch { DisableVhdxRecoveryOptions(); }
    }

    private void DisableVhdxRecoveryOptions()
    {
        CanUseVhdx = false;
        // The capability value can already be false while the user changes the checkbox;
        // do not rely on its generated changed hook to clear an unsafe stale selection.
        RecoveryImportInPlace = false;
        if (RecoveryFormat == RecoveryPointFormat.Vhdx) RecoveryFormat = RecoveryPointFormat.Tar;
    }

}
