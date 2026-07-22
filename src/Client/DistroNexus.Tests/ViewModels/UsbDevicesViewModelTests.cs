using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.Tests.ViewModels;

public sealed class UsbDevicesViewModelTests
{
    [Fact]
    public async Task Initialize_TracksAvailabilityAndDeviceGuidance()
    {
        var watcher = new FakeWatcher();
        using var vm = new UsbDevicesViewModel(new FakeService(), watcher);
        await vm.InitializeAsync();
        vm.SelectedDevice = Assert.Single(vm.Devices);
        Assert.True(vm.IsAvailable);
        Assert.True(watcher.Started);
        Assert.False(string.IsNullOrWhiteSpace(vm.SelectedGuidance));
        watcher.Raise(); // Exercises the view-model notification path without a physical device notification.
    }

    [Fact]
    public void Dispose_StopsAndDisposesThePageScopedWatcherOnce()
    {
        var watcher = new FakeWatcher();
        var vm = new UsbDevicesViewModel(new FakeService(), watcher);
        vm.Dispose(); vm.Dispose();
        Assert.True(watcher.Stopped);
        Assert.Equal(1, watcher.DisposeCalls);
    }
    [Fact]
    public async Task Initialize_DisablesActionsWhenServiceIsStoppedWhileLeavingDiscoverySafe()
    {
        var service = new FakeService { Status = new UsbIpdStatus(true, false, new Version(4, 2), true, "Usb.ServiceStopped") };
        using var vm = new UsbDevicesViewModel(service);
        await vm.InitializeAsync();
        Assert.False(vm.IsAvailable);
        Assert.Single(vm.Devices);
    }

    private sealed class FakeService : IUsbDeviceService
    {
        private static readonly UsbDeviceInfo Device = new(new UsbBusId("1-2"), "2341:0043", "Arduino Uno", UsbDeviceAvailability.Shared, true, false, false, null, "Devices_GuidanceArduino");
        public UsbIpdStatus Status { get; set; } = new(true, true, new Version(4, 2), true, "Usb.Available");
        public Task<UsbIpdStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(Status);
        public Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UsbDeviceInfo>>([Device]);
        public Task<UsbDeviceActionPreview> PreviewAsync(UsbDeviceAction action, UsbBusId busId, string? distributionName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UsbDeviceActionResult> ExecuteAsync(UsbDeviceActionPreview preview, IProgress<UsbOperationProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FakeWatcher : IUsbDeviceChangeWatcher
    {
        public event EventHandler? DevicesChanged;
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public int DisposeCalls { get; private set; }
        public void Start() => Started = true;
        public void Stop() => Stopped = true;
        public void Dispose() => DisposeCalls++;
        public void Raise() => DevicesChanged?.Invoke(this, EventArgs.Empty);
    }
}
