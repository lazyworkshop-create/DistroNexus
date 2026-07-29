using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>Visible-only, stateless monitoring presentation. Core owns probes and process authority.</summary>
public partial class MonitorTabViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WslInstanceViewModel _instance;
    private readonly IPowerShellModuleClient _module;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _pullCancellation;
    private bool _isVisible;
    private int _pulling;
    private string? _snapshotToken;
    [ObservableProperty] private ObservableCollection<MonitoringSample> _samples = [];
    [ObservableProperty] private ObservableCollection<MonitoredProcess> _processes = [];
    [ObservableProperty] private ObservableCollection<ListeningPort> _listeningPorts = [];
    [ObservableProperty] private MonitoringSample? _latest;
    [ObservableProperty] private string _unavailableReason = string.Empty;
    [ObservableProperty] private int _intervalSeconds = 2;
    [ObservableProperty] private bool _isCollecting;
    [ObservableProperty] private MonitoredProcess? _selectedProcess;
    [ObservableProperty] private double _cpuWarningThreshold = 90;
    [ObservableProperty] private double _memoryWarningThreshold = 90;
    [ObservableProperty] private double _filesystemWarningThreshold = 90;
    public IReadOnlyList<int> Intervals { get; } = [1, 2, 5, 10];
    public bool IsAvailable => string.IsNullOrEmpty(UnavailableReason);
    public string CpuDisplay => Latest?.CpuPercent is double value ? string.Format(L("MonitorTab_CpuDisplay"), value) : L("MonitorTab_Unavailable");
    public string MemoryDisplay => Latest?.MemoryUsedBytes is long value ? string.Format(L("MonitorTab_BytesMemoryDisplay"), value) : L("MonitorTab_Unavailable");
    public string SwapDisplay => Latest?.SwapUsedBytes is long value ? string.Format(L("MonitorTab_BytesSwapDisplay"), value) : L("MonitorTab_Unavailable");
    public string FilesystemDisplay => Latest is { FilesystemUsedBytes: long used, FilesystemTotalBytes: long total } ? string.Format(L("MonitorTab_FilesystemUsageDisplay"), used, total) : L("MonitorTab_Unavailable");
    public string DiskIoDisplay => Latest?.DiskReadBytesPerSecond is long read ? string.Format(L("MonitorTab_DiskIoDisplay"), read, Latest.DiskWriteBytesPerSecond ?? 0) : L("MonitorTab_Unavailable");
    public string NetworkDisplay => Latest?.NetworkReceiveBytesPerSecond is long receive ? string.Format(L("MonitorTab_NetworkDisplay"), receive, Latest.NetworkTransmitBytesPerSecond ?? 0) : L("MonitorTab_Unavailable");
    public string VhdxDisplay => Latest?.VhdxPhysicalBytes is long value ? string.Format(L("MonitorTab_BytesMemoryDisplay"), value) : L("MonitorTab_Unavailable");
    public string ReclaimDisplay => Latest?.EstimatedReclaimableBytes is long value ? string.Format(L("MonitorTab_BytesMemoryDisplay"), value) : L("MonitorTab_Unavailable");
    public string LimitsDisplay => Latest?.HostLimits is { } limits ? string.Format(L("MonitorTab_LimitsDisplay"), limits.MemoryLimitBytes?.ToString() ?? "—", limits.SwapLimitBytes?.ToString() ?? "—", limits.ProcessorLimit?.ToString() ?? "—") : L("MonitorTab_Unavailable");

    public MonitorTabViewModel(WslInstanceViewModel instance, IPowerShellModuleClient module, IDialogService dialogs)
    {
        (_instance, _module, _dialogs) = (instance, module, dialogs);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(IntervalSeconds) };
        _timer.Tick += OnTimerTick;
        _instance.PropertyChanged += OnInstancePropertyChanged;
    }
    public Task ActivateAsync()
    {
        _isVisible = true;
        IsCollecting = true;
        _timer.Interval = TimeSpan.FromSeconds(IntervalSeconds);
        _timer.Start();
        return PullAsync();
    }
    public Task StopAsync()
    {
        _isVisible = false;
        IsCollecting = false;
        _timer.Stop();
        _snapshotToken = null;
        _pullCancellation?.Cancel();
        return Task.CompletedTask;
    }
    private async void OnTimerTick(object? sender, EventArgs e) { await PullAsync(); }
    private async Task PullAsync()
    {
        if (!_isVisible || !_instance.Instance.IsRunning || Interlocked.Exchange(ref _pulling, 1) != 0) return;
        var previous = Interlocked.Exchange(ref _pullCancellation, new CancellationTokenSource());
        previous?.Cancel(); previous?.Dispose();
        var cancellation = _pullCancellation!;
        var instanceName = _instance.Instance.Name;
        try
        {
            var result = await _module.GetMonitoringSnapshotAsync(instanceName, IntervalSeconds, cancellation.Token);
            if (cancellation.IsCancellationRequested || !_isVisible || !string.Equals(instanceName, _instance.Instance.Name, StringComparison.Ordinal) || !_instance.Instance.IsRunning) return;
            _snapshotToken = result.SnapshotToken;
            Apply(result.Sample);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (_isVisible) { UnavailableReason = ex.Message; OnPropertyChanged(nameof(IsAvailable)); }
        finally { Interlocked.Exchange(ref _pulling, 0); }
    }
    private void Apply(MonitoringSample sample)
    {
        Latest = sample;
        Processes = new ObservableCollection<MonitoredProcess>(sample.Processes);
        ListeningPorts = new ObservableCollection<ListeningPort>(sample.ListeningPorts ?? []);
        Samples.Add(sample); if (Samples.Count > 300) Samples.RemoveAt(0);
        UnavailableReason = sample.UnavailableMetrics.Count == 0 ? string.Empty : L("MonitorTab_SomeMetricsUnavailable");
        OnPropertyChanged(nameof(IsAvailable)); UpdateDisplays();
    }
    partial void OnIntervalSecondsChanged(int value) { if (Intervals.Contains(value) && _isVisible) { _timer.Interval = TimeSpan.FromSeconds(value); _ = PullAsync(); } }
    [RelayCommand] private Task RefreshAsync() => PullAsync();
    [RelayCommand] private Task TerminateAsync(MonitoredProcess? process) => RunActionAsync(process, MonitoringProcessAction.Terminate);
    [RelayCommand] private Task KillAsync(MonitoredProcess? process) => RunActionAsync(process, MonitoringProcessAction.Kill);
    [RelayCommand] private Task ReniceAsync(MonitoredProcess? process) => RunActionAsync(process, MonitoringProcessAction.Renice);
    private async Task RunActionAsync(MonitoredProcess? process, MonitoringProcessAction action)
    {
        if (process is null || string.IsNullOrWhiteSpace(_snapshotToken)) return;
        try
        {
            var preview = await _module.GetMonitoringProcessActionPreviewAsync(_snapshotToken, process.Pid, action);
            var warning = preview.RequiresAdditionalWarning ? L("MonitorTab_PrivilegedWarning") : string.Empty;
            if (!await _dialogs.ShowConfirmAsync(L("MonitorTab_ConfirmAction"), preview.Message + warning)) return;
            var result = await _module.InvokeMonitoringProcessActionAsync(preview.PreviewToken);
            if (!result.Succeeded) await _dialogs.ShowAlertAsync(L("MonitorTab_ActionFailed"), result.Guidance ?? result.OutcomeCode);
        }
        catch (Exception ex) { await _dialogs.ShowAlertAsync(L("MonitorTab_ActionFailed"), ex.Message); }
    }
    private void UpdateDisplays() { OnPropertyChanged(nameof(CpuDisplay)); OnPropertyChanged(nameof(MemoryDisplay)); OnPropertyChanged(nameof(SwapDisplay)); OnPropertyChanged(nameof(FilesystemDisplay)); OnPropertyChanged(nameof(DiskIoDisplay)); OnPropertyChanged(nameof(NetworkDisplay)); OnPropertyChanged(nameof(VhdxDisplay)); OnPropertyChanged(nameof(ReclaimDisplay)); OnPropertyChanged(nameof(LimitsDisplay)); }
    private void OnInstancePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WslInstanceViewModel.RawState) || !_isVisible) return;
        if (!_instance.Instance.IsRunning) { _ = StopAsync(); UnavailableReason = "Monitor.InstanceStopped"; OnPropertyChanged(nameof(IsAvailable)); }
        else _ = PullAsync();
    }
    private static string L(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
    public async ValueTask DisposeAsync() { _instance.PropertyChanged -= OnInstancePropertyChanged; await StopAsync(); _timer.Tick -= OnTimerTick; }
}
