using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Services;

namespace DistroNexus.Desktop.ViewModels;

public sealed partial class UsbDevicesViewModel : ObservableObject, IDisposable
{
    private readonly IUsbDeviceService _devices;
    private CancellationTokenSource? _operation;
    private readonly IUsbDeviceChangeWatcher? _watcher;
    private readonly IDialogService? _dialogs;
    public ObservableCollection<UsbDeviceInfo> Devices { get; } = [];
    [ObservableProperty] private UsbDeviceInfo? _selectedDevice;
    [ObservableProperty] private string _distributionName = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private string _selectedGuidance = string.Empty;
    [ObservableProperty] private string _operationPhase = string.Empty;
    [ObservableProperty] private int _operationPercent;
    [ObservableProperty] private string _diagnosticCode = string.Empty;
    [ObservableProperty] private string _diagnosticMessage = string.Empty;
    private bool _disposed;
    public bool CanOperate => IsAvailable && !IsBusy && SelectedDevice is not null;
    public UsbDevicesViewModel(IUsbDeviceService devices, IUsbDeviceChangeWatcher? watcher = null, IDialogService? dialogs = null)
    {
        (_devices, _watcher, _dialogs) = (devices, watcher, dialogs);
        if (_watcher is not null) _watcher.DevicesChanged += OnDevicesChanged;
    }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanOperate));
    partial void OnIsAvailableChanged(bool value) => OnPropertyChanged(nameof(CanOperate));
    partial void OnSelectedDeviceChanged(UsbDeviceInfo? value)
    {
        OnPropertyChanged(nameof(CanOperate));
        SelectedGuidance = value?.GuidanceCode is { } key ? L(key, string.Empty) : string.Empty;
    }
    public async Task InitializeAsync(CancellationToken cancellationToken = default) { _watcher?.Start(); await RefreshAsync(cancellationToken); }
    private void OnDevicesChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(new Action(RefreshFromWatcher));
            return;
        }
        RefreshFromWatcher();
    }
    private async void RefreshFromWatcher()
    {
        if (!_disposed && !IsBusy) await RefreshAsync();
    }
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var status = await _devices.GetStatusAsync(cancellationToken);
            // Discovery remains available when usbipd is installed, but action controls require its running approved service.
            IsAvailable = status.IsInstalled && status.IsServiceRunning && status.SupportsMutation;
            Status = StatusFor(status);
            Devices.Clear();
            if (!status.IsInstalled) return;
            foreach (var item in await _devices.ListAsync(cancellationToken)) Devices.Add(item);
            Status = string.Format(L("Devices_Ready", "{0} USB device(s) discovered."), Devices.Count);
        }
        catch (OperationCanceledException) { Status = L("Devices_Cancelled", "Operation cancelled."); }
        catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); }
        finally { IsBusy = false; }
    }
    [RelayCommand] private Task BindAsync() => RunAsync(UsbDeviceAction.Bind);
    [RelayCommand] private Task UnbindAsync() => RunAsync(UsbDeviceAction.Unbind);
    [RelayCommand] private Task AttachAsync() => RunAsync(UsbDeviceAction.Attach);
    [RelayCommand] private Task DetachAsync() => RunAsync(UsbDeviceAction.Detach);
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    [RelayCommand]
    private void CopyDiagnostic()
    {
        if (!string.IsNullOrWhiteSpace(DiagnosticCode)) Clipboard.SetText($"{DiagnosticCode}: {DiagnosticMessage}");
    }
    private async Task RunAsync(UsbDeviceAction action)
    {
        if (!CanOperate || SelectedDevice is null) return;
        _operation = new CancellationTokenSource(); IsBusy = true;
        DiagnosticCode = string.Empty; DiagnosticMessage = string.Empty;
        try
        {
            var target = action == UsbDeviceAction.Attach ? DistributionName.Trim() : null;
            var preview = await _devices.PreviewAsync(action, SelectedDevice.BusId, target, _operation.Token);
            if (_dialogs is null || !await OperationPreviewDialog.ShowAsync(_dialogs, L("Devices_ConfirmTitle", "Confirm USB operation"), preview.Effects, preview.Warnings))
            { Status = L("Devices_Cancelled", "Operation cancelled."); return; }
            var progress = new Progress<UsbOperationProgress>(value =>
            {
                OperationPhase = L(value.PhaseCode, value.PhaseCode);
                OperationPercent = value.Percent;
            });
            var result = await _devices.ExecuteAsync(preview, progress, _operation.Token);
            // Attach verification is advisory: usbipd may succeed even when the distro lacks lsusb.
            Status = result.Succeeded ? (result.Guidance ?? L("Devices_Succeeded", "USB operation completed.")) : (result.Guidance ?? L(result.OutcomeCode, result.OutcomeCode));
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Guidance)) SelectedGuidance = result.Guidance;
            if (!result.Succeeded && result.Diagnostic is not null)
            {
                DiagnosticCode = result.Diagnostic.Code;
                DiagnosticMessage = result.Diagnostic.Message;
            }
            Devices.Clear();
            foreach (var item in await _devices.ListAsync(_operation.Token)) Devices.Add(item);
        }
        catch (OperationCanceledException) { Status = L("Devices_Cancelled", "Operation cancelled."); }
        catch (Exception ex)
        {
            var formatted = MainViewModel.FormatAlertMessage(ex);
            Status = formatted;
            DiagnosticCode = "DN-8011";
            DiagnosticMessage = "The USB operation could not be completed. Review the current device state and try again.";
        }
        finally { _operation.Dispose(); _operation = null; IsBusy = false; OperationPhase = string.Empty; }
    }
    private static string L(string key, string fallback) => Properties.Resources.ResourceManager.GetString(key) ?? fallback;
    private static string StatusFor(UsbIpdStatus status) => status.ReasonCode switch
    {
        "Usb.Unavailable" => L("Devices_Unavailable", "usbipd-win is unavailable. Install or repair it before managing USB devices."),
        "Usb.ServiceStopped" => L("Devices_ServiceStopped", "The usbipd service is stopped. Start its Windows service, then refresh."),
        "Usb.VersionMalformed" => L("Devices_VersionMalformed", "usbipd-win reported an unrecognized version. Discovery may be shown, but device operations are disabled."),
        "Usb.UnknownVersion" or "Usb.UnsupportedVersion" => L("Devices_UnsupportedVersion", "This usbipd-win version is not approved for device operations."),
        _ => L(status.ReasonCode, status.ReasonCode)
    };
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operation?.Cancel();
        if (_watcher is not null) { _watcher.DevicesChanged -= OnDevicesChanged; _watcher.Stop(); _watcher.Dispose(); }
    }
}
