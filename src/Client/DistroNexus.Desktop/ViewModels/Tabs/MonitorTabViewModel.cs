using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>Owns a monitor session only while the Monitor tab is selected.</summary>
public partial class MonitorTabViewModel : ObservableObject, IAsyncDisposable
{
    private readonly WslInstanceViewModel _instance; private readonly IMonitoringService _monitoring; private readonly IDialogService _dialogs;
    private IMonitoringSession? _session;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private bool _isVisible;
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
    public string FilesystemDisplay => Latest is { FilesystemUsedBytes: long used, FilesystemTotalBytes: long total }
        ? string.Format(L("MonitorTab_FilesystemUsageDisplay"), used, total)
        : L("MonitorTab_Unavailable");
    public string DiskIoDisplay => Latest?.DiskReadBytesPerSecond is long read ? string.Format(L("MonitorTab_DiskIoDisplay"), read, Latest.DiskWriteBytesPerSecond ?? 0) : L("MonitorTab_Unavailable");
    public string NetworkDisplay => Latest?.NetworkReceiveBytesPerSecond is long receive ? string.Format(L("MonitorTab_NetworkDisplay"), receive, Latest.NetworkTransmitBytesPerSecond ?? 0) : L("MonitorTab_Unavailable");
    public string VhdxDisplay => Latest?.VhdxPhysicalBytes is long value ? string.Format(L("MonitorTab_VhdxDisplay"), value) : L("MonitorTab_Unavailable");
    public string ReclaimDisplay => Latest?.EstimatedReclaimableBytes is long value ? string.Format(L("MonitorTab_ReclaimDisplay"), value) : L("MonitorTab_Unavailable");
    public string LimitsDisplay => Latest?.HostLimits is { } limits ? string.Format(L("MonitorTab_LimitsDisplay"), limits.MemoryLimitBytes?.ToString() ?? "—", limits.SwapLimitBytes?.ToString() ?? "—", limits.ProcessorLimit?.ToString() ?? "—") : L("MonitorTab_Unavailable");
    public MonitorTabViewModel(WslInstanceViewModel instance, IMonitoringService monitoring, IDialogService dialogs)
    {
        (_instance, _monitoring, _dialogs) = (instance, monitoring, dialogs);
        _instance.PropertyChanged += OnInstancePropertyChanged;
    }
    public async Task ActivateAsync()
    {
        _isVisible = true;
        await _lifecycle.WaitAsync();
        try
        {
            await StopCoreAsync();
            // CreateSession deliberately returns an offline-only session for a stopped
            // distribution. It is never started and it is replaced after a later Running
            // transition while this tab remains visible.
            _session = _monitoring.CreateSession(_instance.Instance, TimeSpan.FromSeconds(IntervalSeconds));
            _session.SampleAvailable += OnSample;
            UnavailableReason = _session.UnavailableReason ?? string.Empty; OnPropertyChanged(nameof(IsAvailable));
            Latest = _session.Samples.LastOrDefault();
            if (Latest is not null) UpdateDisplays();
            await _session.SetThresholdsAsync(new MonitoringThresholds(CpuWarningThreshold, MemoryWarningThreshold, FilesystemWarningThreshold));
            if (_instance.Instance.IsRunning) await _session.StartAsync();
            IsCollecting = _session.IsRunning;
        }
        finally { _lifecycle.Release(); }
    }
    public async Task StopAsync()
    {
        _isVisible = false;
        await _lifecycle.WaitAsync(); try { await StopCoreAsync(); } finally { _lifecycle.Release(); }
    }
    private async Task StopCoreAsync()
    {
        if (_session is null) return;
        _session.SampleAvailable -= OnSample; await _session.DisposeAsync(); _session = null; IsCollecting = false;
    }
    private void OnSample(object? sender, MonitoringSample sample)
    {
        void Apply()
        {
            Latest = sample; Processes = new ObservableCollection<MonitoredProcess>(sample.Processes); ListeningPorts = new ObservableCollection<ListeningPort>(sample.ListeningPorts ?? []);
            Samples.Add(sample); if (Samples.Count > 300) Samples.RemoveAt(0);
            UnavailableReason = sample.UnavailableMetrics.Count == 0 ? string.Empty : L("MonitorTab_SomeMetricsUnavailable"); OnPropertyChanged(nameof(IsAvailable)); UpdateDisplays();
        }
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Apply(); else _ = dispatcher.InvokeAsync(Apply);
    }
    partial void OnIntervalSecondsChanged(int value) { if (Intervals.Contains(value) && IsCollecting) _ = ActivateAsync(); }
    partial void OnCpuWarningThresholdChanged(double value) => ApplyThresholds();
    partial void OnMemoryWarningThresholdChanged(double value) => ApplyThresholds();
    partial void OnFilesystemWarningThresholdChanged(double value) => ApplyThresholds();
    private void ApplyThresholds()
    {
        if (_session is null) return;
        var thresholds = new MonitoringThresholds(CpuWarningThreshold, MemoryWarningThreshold, FilesystemWarningThreshold);
        if (thresholds.IsValid) _ = _session.SetThresholdsAsync(thresholds);
    }
    [RelayCommand] private Task RefreshAsync() => ActivateAsync();
    [RelayCommand] private async Task TerminateAsync(MonitoredProcess? process) => await RunActionAsync(process, MonitoringProcessAction.Terminate);
    [RelayCommand] private async Task KillAsync(MonitoredProcess? process) => await RunActionAsync(process, MonitoringProcessAction.Kill);
    [RelayCommand] private async Task ReniceAsync(MonitoredProcess? process) => await RunActionAsync(process, MonitoringProcessAction.Renice);
    private async Task RunActionAsync(MonitoredProcess? process, MonitoringProcessAction action)
    {
        if (process is null || _session is null) return;
        try { var preview = await _session.PreviewProcessActionAsync(process, action); var warning = preview.RequiresAdditionalWarning ? L("MonitorTab_PrivilegedWarning") : string.Empty; if (!await _dialogs.ShowConfirmAsync(L("MonitorTab_ConfirmAction"), preview.Message + warning)) return; var result = await _session.ExecuteProcessActionAsync(preview); if (!result.Succeeded) await _dialogs.ShowAlertAsync(L("MonitorTab_ActionFailed"), result.Guidance ?? result.OutcomeCode); }
        catch (Exception ex) { await _dialogs.ShowAlertAsync(L("MonitorTab_ActionFailed"), ex.Message); }
    }
    private void UpdateDisplays() { OnPropertyChanged(nameof(CpuDisplay)); OnPropertyChanged(nameof(MemoryDisplay)); OnPropertyChanged(nameof(SwapDisplay)); OnPropertyChanged(nameof(FilesystemDisplay)); OnPropertyChanged(nameof(DiskIoDisplay)); OnPropertyChanged(nameof(NetworkDisplay)); OnPropertyChanged(nameof(VhdxDisplay)); OnPropertyChanged(nameof(ReclaimDisplay)); OnPropertyChanged(nameof(LimitsDisplay)); }
    private void OnInstancePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WslInstanceViewModel.RawState) || !_isVisible) return;
        // State updates may originate from a background refresh.  ActivateAsync serializes
        // session replacement and observes the current state before it calls StartAsync.
        if (_instance.Instance.IsRunning) _ = ActivateAsync();
        else _ = StopAfterInstanceStopsAsync();
    }
    private async Task StopAfterInstanceStopsAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (!_instance.Instance.IsRunning)
            {
                await StopCoreAsync();
                UnavailableReason = "Monitor.InstanceStopped";
                OnPropertyChanged(nameof(IsAvailable));
            }
        }
        finally { _lifecycle.Release(); }
    }
    private static string L(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
    public async ValueTask DisposeAsync() { _instance.PropertyChanged -= OnInstancePropertyChanged; await StopAsync(); _lifecycle.Dispose(); }
}
