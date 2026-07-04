using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// Backup frequency options for the schedule editor.
/// </summary>
public enum BackupFrequency { Daily, Weekly, Monthly }

/// <summary>
/// ViewModel for the Backup tab of InstanceDetailDialog.
/// Handles backup schedule editor, manual backup invocation, and history list (D-01).
/// </summary>
public partial class BackupTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IBackupService _backupService;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    // ── Static ComboBox data ────────────────────────────────────────────────────

    public static IReadOnlyList<BackupFrequency> FrequencyOptions { get; } =
        [BackupFrequency.Daily, BackupFrequency.Weekly, BackupFrequency.Monthly];

    public static IReadOnlyList<string> DaysOfWeek { get; } =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public static IReadOnlyList<int> DaysOfMonth { get; } =
        [.. Enumerable.Range(1, 31)];

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

    // ── Computed ───────────────────────────────────────────────────────────────

    public bool ShowDayOfWeekPicker => Frequency == BackupFrequency.Weekly;
    public bool ShowDayOfMonthPicker => Frequency == BackupFrequency.Monthly;
    public bool IsFormEnabled => !IsLoading && !IsBackingUp;

    public WslInstanceViewModel Instance => _instance;

    // ── Constructor ────────────────────────────────────────────────────────────

    public BackupTabViewModel(
        WslInstanceViewModel instance,
        IBackupService backupService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
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

                await LoadBackupHistoryAsync();
            }
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

    internal async Task LoadBackupHistoryAsync()
    {
        var entries = new List<BackupHistoryEntry>();

        try
        {
            if (!string.IsNullOrWhiteSpace(DestinationPath) && Directory.Exists(DestinationPath))
            {
                entries.AddRange(Directory
                    .EnumerateFiles(DestinationPath)
                    .Where(f => f.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .Select(fi => new BackupHistoryEntry
                    {
                        Timestamp    = new DateTimeOffset(fi.CreationTimeUtc),
                        FileSizeBytes = fi.Length,
                        FilePath     = fi.FullName,
                        IsSuccess    = true
                    }));
            }

            entries.AddRange(await LoadFailureHistoryAsync());

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

    private async Task<IEnumerable<BackupHistoryEntry>> LoadFailureHistoryAsync()
    {
        var notifPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DistroNexus", "pending-notifications.json");
        if (!File.Exists(notifPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(notifPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("notifications", out var notifications))
                return [];

            return notifications
                .EnumerateArray()
                .Where(n => string.Equals(
                    n.TryGetProperty("type", out var type) ? type.GetString() : null,
                    "BackupFailure",
                    StringComparison.OrdinalIgnoreCase))
                .Where(n => string.Equals(
                    n.TryGetProperty("instance", out var inst) ? inst.GetString() : null,
                    _instance.Name,
                    StringComparison.OrdinalIgnoreCase))
                .Select(n =>
                {
                    var message = n.TryGetProperty("message", out var messageEl)
                        ? messageEl.GetString()
                        : Properties.Resources.BackupTab_StatusFailed;
                    var timestamp = n.TryGetProperty("time", out var timeEl)
                        && DateTimeOffset.TryParse(timeEl.GetString(), out var parsed)
                            ? parsed
                            : DateTimeOffset.Now;

                    return new BackupHistoryEntry
                    {
                        Timestamp = timestamp,
                        ErrorMessage = message ?? Properties.Resources.BackupTab_StatusFailed,
                        IsSuccess = false
                    };
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
