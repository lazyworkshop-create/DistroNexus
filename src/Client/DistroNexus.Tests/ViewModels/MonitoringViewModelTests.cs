using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Controls;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class MonitoringViewModelTests
{
    [Fact]
    public async Task VisibleMonitorTab_UsesTypedSnapshotClientAndBoundsDisplayHistory()
    {
        var module = new Mock<IPowerShellModuleClient>();
        var sample = Sample();
        module.Setup(x => x.GetMonitoringSnapshotAsync("Ubuntu", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitoringSnapshotResult(sample, new string('a', 64), DateTimeOffset.UtcNow.AddMinutes(2)));
        await using var tab = new MonitorTabViewModel(NewInstance(), module.Object, Mock.Of<IDialogService>());

        await tab.ActivateAsync();

        Assert.Equal(sample, tab.Latest);
        Assert.Single(tab.Samples);
        module.Verify(x => x.GetMonitoringSnapshotAsync("Ubuntu", 2, It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(typeof(MonitorTabViewModel).GetConstructors().SelectMany(x => x.GetParameters()), p => p.ParameterType == typeof(IMonitoringService));
    }

    [Fact]
    public async Task Stop_CancelsLateSnapshotAndDoesNotApplyIt()
    {
        var completion = new TaskCompletionSource<MonitoringSnapshotResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken pullToken = default;
        var module = new Mock<IPowerShellModuleClient>();
        module.Setup(x => x.GetMonitoringSnapshotAsync("Ubuntu", 2, It.IsAny<CancellationToken>())).Callback<string, int, CancellationToken>((_, _, token) => pullToken = token).Returns(completion.Task);
        await using var tab = new MonitorTabViewModel(NewInstance(), module.Object, Mock.Of<IDialogService>());

        var activate = tab.ActivateAsync();
        await Task.Delay(20);
        await tab.StopAsync();
        completion.TrySetResult(new MonitoringSnapshotResult(Sample(), new string('a', 64), DateTimeOffset.UtcNow.AddMinutes(2)));
        await activate;

        Assert.Null(tab.Latest);
        Assert.False(tab.IsCollecting);
        Assert.True(pullToken.IsCancellationRequested);
    }

    [Fact]
    public void RollingMetricChart_ProjectsCoreMetricsForRollingRender()
    {
        var sample = new MonitoringSample(DateTimeOffset.UtcNow, 1, 50, 100, 4, 5, 25, 100, 8, 9, 10, 11, 12, 13, [], new Dictionary<string, string>());
        Assert.Equal(50, RollingMetricChart.GetMetricValue(sample, RollingMetric.MemoryPercent));
        Assert.Equal(25, RollingMetricChart.GetMetricValue(sample, RollingMetric.FilesystemPercent));
        Assert.Equal(21, RollingMetricChart.GetMetricValue(sample, RollingMetric.NetworkBytesPerSecond));
    }

    private static MonitoringSample Sample() => new(DateTimeOffset.UtcNow, 1, 2, 3, null, null, 2_048, 8_192, null, null, null, null, null, null, [], new Dictionary<string, string>());
    private static WslInstanceViewModel NewInstance() => new(new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());
}
