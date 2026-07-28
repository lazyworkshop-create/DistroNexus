using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.Desktop.Controls;
using DistroNexus.Desktop.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class MonitoringViewModelTests
{
    [Fact]
    public async Task TabSelectionRace_StopsSessionWhenMonitorActivationFinishesAfterNavigation()
    {
        var session = new BlockingSession();
        var monitor = new Mock<IMonitoringService>();
        monitor.Setup(x => x.CreateSession(It.IsAny<WslInstance>(), It.IsAny<TimeSpan>())).Returns(session);
        await using var vm = NewDetail(monitor.Object);

        vm.SelectedTabIndex = 7;
        await session.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        vm.SelectedTabIndex = 0;
        session.AllowStart.TrySetResult();

        await WaitUntilAsync(() => session.Disposed, TimeSpan.FromSeconds(2));
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task VisibleMonitorTab_ReplacesOfflineSessionAfterInstanceRestarts()
    {
        var instance = NewInstance();
        var first = new SimpleSession();
        var second = new SimpleSession();
        var monitoring = new Mock<IMonitoringService>();
        monitoring.SetupSequence(x => x.CreateSession(It.IsAny<WslInstance>(), It.IsAny<TimeSpan>())).Returns(first).Returns(second);
        await using var tab = new MonitorTabViewModel(instance, monitoring.Object, Mock.Of<IDialogService>());

        await tab.ActivateAsync();
        instance.UpdateState("Stopped");
        await WaitUntilAsync(() => first.Disposed, TimeSpan.FromSeconds(2));
        instance.UpdateState("Running");
        await WaitUntilAsync(() => second.Started, TimeSpan.FromSeconds(2));

        monitoring.Verify(x => x.CreateSession(It.IsAny<WslInstance>(), It.IsAny<TimeSpan>()), Times.Exactly(2));
        Assert.True(second.Started);
    }

    [Fact]
    public void RollingMetricChart_ProjectsCoreMetricsForRollingRender()
    {
        var sample = new MonitoringSample(DateTimeOffset.UtcNow, 1, 50, 100, 4, 5, 25, 100, 8, 9, 10, 11, 12, 13, [], new Dictionary<string, string>());

        Assert.Equal(50, RollingMetricChart.GetMetricValue(sample, RollingMetric.MemoryPercent));
        Assert.Equal(25, RollingMetricChart.GetMetricValue(sample, RollingMetric.FilesystemPercent));
        Assert.Equal(21, RollingMetricChart.GetMetricValue(sample, RollingMetric.NetworkBytesPerSecond));
    }

    [Fact]
    public async Task MonitorTab_DisplaysLinuxFilesystemUsageAndCapacity()
    {
        var session = new SimpleSession();
        var monitoring = new Mock<IMonitoringService>();
        monitoring.Setup(x => x.CreateSession(It.IsAny<WslInstance>(), It.IsAny<TimeSpan>())).Returns(session);
        await using var tab = new MonitorTabViewModel(NewInstance(), monitoring.Object, Mock.Of<IDialogService>());

        await tab.ActivateAsync();
        session.Publish(new MonitoringSample(DateTimeOffset.UtcNow, null, null, null, null, null, 2_048, 8_192, null, null, null, null, null, null, [], new Dictionary<string, string>()));

        var format = DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString("MonitorTab_FilesystemUsageDisplay")!;
        Assert.Equal(string.Format(format, 2_048, 8_192), tab.FilesystemDisplay);
    }

    private static InstanceDetailViewModel NewDetail(IMonitoringService monitoring)
    {
        var instance = NewInstance();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        return new InstanceDetailViewModel(instance, Mock.Of<IWslManagerService>(), Mock.Of<IDockerIntegrationService>(), Mock.Of<INetworkService>(), Mock.Of<IBackupService>(), Mock.Of<IRecoveryPointService>(), Mock.Of<IWslConfigService>(), dialogs.Object, Mock.Of<IDistributionConfigurationService>(), Mock.Of<IPlatformCapabilityService>(), Mock.Of<ISystemdService>(), Mock.Of<INetworkDiagnosticsService>(), Mock.Of<IFirewallOperationBroker>(), Mock.Of<INetworkConfigurationService>(), Mock.Of<INetworkStatusAdapter>(), Mock.Of<IBrowserLauncher>(), monitoring);
    }

    private static WslInstanceViewModel NewInstance() => new(new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(), Mock.Of<ISettingsService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= until) throw new TimeoutException("The monitor session was not stopped.");
            await Task.Delay(10);
        }
    }

    private sealed class BlockingSession : IMonitoringSession
    {
        public TaskCompletionSource StartEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowStart { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public IReadOnlyList<MonitoringSample> Samples => [];
        public bool IsRunning { get; private set; }
        public string? UnavailableReason => null;
        public event EventHandler<MonitoringSample>? SampleAvailable { add { } remove { } }
        public async IAsyncEnumerable<MonitoringSample> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public async Task StartAsync(CancellationToken cancellationToken = default) { StartEntered.TrySetResult(); await AllowStart.Task.WaitAsync(cancellationToken); IsRunning = true; }
        public Task StopAsync() { IsRunning = false; return Task.CompletedTask; }
        public Task SetThresholdsAsync(MonitoringThresholds thresholds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProcessActionPreview> PreviewProcessActionAsync(MonitoredProcess process, MonitoringProcessAction action, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProcessActionResult> ExecuteProcessActionAsync(ProcessActionPreview preview, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async ValueTask DisposeAsync() { Disposed = true; await StopAsync(); }
    }

    private sealed class SimpleSession : IMonitoringSession
    {
        private event EventHandler<MonitoringSample>? _sampleAvailable;
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public IReadOnlyList<MonitoringSample> Samples => [];
        public bool IsRunning => Started && !Disposed;
        public string? UnavailableReason => null;
        public event EventHandler<MonitoringSample>? SampleAvailable { add => _sampleAvailable += value; remove => _sampleAvailable -= value; }
        public async IAsyncEnumerable<MonitoringSample> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public void Publish(MonitoringSample sample) => _sampleAvailable?.Invoke(this, sample);
        public Task StartAsync(CancellationToken cancellationToken = default) { Started = true; return Task.CompletedTask; }
        public Task StopAsync() { Started = false; return Task.CompletedTask; }
        public Task SetThresholdsAsync(MonitoringThresholds thresholds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProcessActionPreview> PreviewProcessActionAsync(MonitoredProcess process, MonitoringProcessAction action, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProcessActionResult> ExecuteProcessActionAsync(ProcessActionPreview preview, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposed = true; Started = false; return ValueTask.CompletedTask; }
    }
}
