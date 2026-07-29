using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class UsbDevicesViewModelTests
{
    [Fact]
    public async Task Initialize_UsesOnlyTypedModuleReadsAndRendersSanitizedGuidance()
    {
        var module = ReadModule();
        using var vm = new UsbDevicesViewModel(module.Object);
        await vm.InitializeAsync();
        vm.SelectedDevice = Assert.Single(vm.Devices);
        Assert.True(vm.IsAvailable);
        Assert.Equal("Devices_GuidanceArduino", vm.SelectedGuidance);
        module.Verify(x => x.GetUsbStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
        module.Verify(x => x.GetUsbDevicesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unload_CancelsVisibleLifetimeAndSuppressesLateRefresh()
    {
        var completion = new TaskCompletionSource<UsbStatusResult>();
        var module = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        module.Setup(x => x.GetUsbStatusAsync(It.IsAny<CancellationToken>())).Returns(completion.Task);
        using var vm = new UsbDevicesViewModel(module.Object);
        var initialize = vm.InitializeAsync();
        vm.Dispose();
        completion.SetResult(new(true, "Running", "5.1", false, null, "Usb.Ready"));
        await initialize;
        Assert.Empty(vm.Devices);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void DesktopUsbSurface_HasNoServiceWatcherOrActionAuthority()
    {
        var root = FindRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "ViewModels", "UsbDevicesViewModel.cs"));
        var app = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "App.xaml.cs"));
        Assert.DoesNotContain("IUsbDeviceService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IUsbDeviceChangeWatcher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IUsbDeviceService", app, StringComparison.Ordinal);
        Assert.DoesNotContain("IUsbDeviceChangeWatcher", app, StringComparison.Ordinal);
        var project = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "DistroNexus.Desktop.csproj"));
        Assert.DoesNotContain("DistroNexus.UsbElevatedHelper", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyUsbElevatedHelper", project, StringComparison.Ordinal);
    }

    private static Mock<IPowerShellModuleClient> ReadModule()
    {
        var module = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        module.Setup(x => x.GetUsbStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UsbStatusResult(true, "Running", "5.1", false, null, "Usb.Ready"));
        module.Setup(x => x.GetUsbDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UsbDeviceListResult(
            [new UsbDeviceResult("1-2", "Arduino", "Shared", true, false, false, null, "Devices_GuidanceArduino")], "Usb.Ready"));
        return module;
    }
    private static string FindRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "AGENTS.md"))) path = Directory.GetParent(path)?.FullName ?? throw new DirectoryNotFoundException();
        return path;
    }
}
