using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>Presentation-only consumer of the broker-free USB discovery module contract.</summary>
public sealed partial class UsbDevicesViewModel : ObservableObject, IDisposable
{
    private readonly IPowerShellModuleClient _module;
    private CancellationTokenSource? _visibleLifetime;
    private Task? _pollTask;
    private int _generation;
    public ObservableCollection<UsbDeviceResult> Devices { get; } = [];
    [ObservableProperty] private UsbDeviceResult? _selectedDevice;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private string _selectedGuidance = string.Empty;

    public UsbDevicesViewModel(IPowerShellModuleClient module) => _module = module;

    partial void OnSelectedDeviceChanged(UsbDeviceResult? value) => SelectedGuidance = value?.Guidance ?? string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_visibleLifetime is not null) return;
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _visibleLifetime = lifetime;
        var generation = Interlocked.Increment(ref _generation);
        await RefreshCoreAsync(generation, lifetime.Token);
        if (!lifetime.IsCancellationRequested && ReferenceEquals(_visibleLifetime, lifetime))
            _pollTask = PollAsync(generation, lifetime.Token);
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        var lifetime = _visibleLifetime;
        return lifetime is null ? Task.CompletedTask : RefreshCoreAsync(Volatile.Read(ref _generation), lifetime.Token);
    }

    private async Task PollAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await RefreshCoreAsync(generation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task RefreshCoreAsync(int generation, CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var status = await _module.GetUsbStatusAsync(cancellationToken);
            if (!IsCurrent(generation, cancellationToken)) return;
            IsAvailable = status.IsInstalled && status.ServiceState == "Running";
            Status = StatusFor(status);
            var list = await _module.GetUsbDevicesAsync(cancellationToken);
            if (!IsCurrent(generation, cancellationToken)) return;
            Devices.Clear();
            foreach (var item in list.Devices) Devices.Add(item);
            if (list.OutcomeCode == "Usb.Ready")
                Status = string.Format(L("Devices_Ready", "{0} USB device(s) discovered."), Devices.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (IsCurrent(generation, cancellationToken)) { Status = MainViewModel.FormatAlertMessage(ex); }
        finally { if (IsCurrent(generation, cancellationToken)) IsBusy = false; }
    }

    private bool IsCurrent(int generation, CancellationToken token) => generation == Volatile.Read(ref _generation) && !token.IsCancellationRequested;
    private static string L(string key, string fallback) => Properties.Resources.ResourceManager.GetString(key) ?? fallback;
    private static string StatusFor(UsbStatusResult status) => status.OutcomeCode switch
    {
        "Usb.NotInstalled" => L("Devices_Unavailable", "usbipd-win is unavailable. Install or repair it before viewing USB devices."),
        "Usb.ServiceUnavailable" => L("Devices_ServiceStopped", "The usbipd service is stopped. Start its Windows service, then refresh."),
        _ => status.Reason ?? status.OutcomeCode
    };

    public void Dispose()
    {
        var lifetime = Interlocked.Exchange(ref _visibleLifetime, null);
        if (lifetime is null) return;
        Interlocked.Increment(ref _generation);
        lifetime.Cancel();
        lifetime.Dispose();
        IsBusy = false;
    }
}
