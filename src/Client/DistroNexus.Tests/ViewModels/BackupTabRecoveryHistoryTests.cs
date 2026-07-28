using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class BackupTabRecoveryHistoryTests : IDisposable
{
    private readonly string _destination = Path.Combine(Path.GetTempPath(), "DistroNexusHistory", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task History_MergesScheduledRecoveryAndFailedEntries_AndFiltersByType()
    {
        var backup = new Mock<IBackupService>();
        backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BackupSchedule { Name = "Ubuntu", Destination = _destination, Frequency = "Daily", RetentionCount = 2, Time = TimeSpan.Zero }]);
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new RecoveryHistoryEntry("scheduled", "Ubuntu", DateTimeOffset.UtcNow.AddMinutes(1), "ScheduledBackup", "Configured", _destination),
            new RecoveryHistoryEntry("recovery", "Ubuntu", DateTimeOffset.UtcNow, "RecoveryPoint", "Completed", "point"),
            new RecoveryHistoryEntry("failed", "Ubuntu", DateTimeOffset.UtcNow.AddMinutes(-1), "RecoveryPoint", "Failed", "point")]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var viewModel = new BackupTabViewModel(Instance(), backup.Object, new Mock<IDialogService>().Object, recovery.Object);

        await viewModel.InitializeAsync();
        Assert.Contains(viewModel.BackupHistory, x => x.Kind == "ScheduledBackup"); // Core has already projected all sources.
        Assert.Contains(viewModel.BackupHistory, x => x.Kind == "RecoveryPoint" && x.IsSuccess);
        Assert.Contains(viewModel.BackupHistory, x => x.Kind == "RecoveryPoint" && !x.IsSuccess);

        viewModel.SelectedHistoryFilter = BackupHistoryFilter.Scheduled;
        await WaitForAsync(() => viewModel.BackupHistory.All(x => x.Kind == "ScheduledBackup"));
        viewModel.SelectedHistoryFilter = BackupHistoryFilter.RecoveryPoints;
        await WaitForAsync(() => viewModel.BackupHistory.Count == 2 && viewModel.BackupHistory.All(x => x.Kind == "RecoveryPoint"));
        viewModel.SelectedHistoryFilter = BackupHistoryFilter.Failures;
        await WaitForAsync(() => viewModel.BackupHistory.Count == 1 && viewModel.BackupHistory.All(x => !x.IsSuccess));
    }

    [Fact]
    public async Task Initialize_LoadsRecoveryHistory_WhenNoScheduleExists()
    {
        var backup = new Mock<IBackupService>();
        backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new RecoveryHistoryEntry("manual", "Ubuntu", DateTimeOffset.UtcNow, "RecoveryPoint", "Verified", "point")]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var vm = new BackupTabViewModel(Instance(), backup.Object, new Mock<IDialogService>().Object, recovery.Object);

        await vm.InitializeAsync();

        Assert.False(vm.HasSchedule);
        Assert.Contains(vm.BackupHistory, x => x.Kind == "RecoveryPoint");
        recovery.Verify(x => x.GetHistoryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Restore_UsesCorePreviewThenExplicitConfirmationAndExecution()
    {
        var backup = new Mock<IBackupService>();
        backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var point = Point();
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([point]);
        recovery.Setup(x => x.PreviewRestoreAsync(It.IsAny<RecoveryRestoreRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryOperationPreview("restore", "Restore", point.Manifest.Id, "Ubuntu", "Clone", Path.Combine(_destination, "clone"), RecoveryPointFormat.Tar, false, false, ["distinct"], 1));
        recovery.Setup(x => x.RestoreAsync(It.IsAny<RecoveryRestoreRequest>(), "restore", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true); dialogs.Setup(x => x.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var vm = new BackupTabViewModel(Instance(), backup.Object, dialogs.Object, recovery.Object);
        await vm.InitializeAsync(); vm.SelectedRecoveryPoint = point; vm.RecoveryTargetInstance = "Clone"; vm.RecoveryTargetDirectory = Path.Combine(_destination, "clone");

        await vm.RestoreRecoveryPointCommand.ExecuteAsync(null);

        recovery.Verify(x => x.PreviewRestoreAsync(It.Is<RecoveryRestoreRequest>(r => r.TargetInstance == "Clone"), It.IsAny<CancellationToken>()), Times.Once);
        recovery.Verify(x => x.RestoreAsync(It.IsAny<RecoveryRestoreRequest>(), "restore", It.IsAny<CancellationToken>(), It.IsAny<IProgress<RecoveryOperationProgress>>()), Times.Once);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.Is<string>(m => m.Contains("distinct"))), Times.Once);
    }

    [Fact]
    public async Task DeleteAndMetadata_AreExplicitAndUseSelectedPoint()
    {
        var backup = new Mock<IBackupService>(); backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var point = Point();
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([point]);
        recovery.Setup(x => x.UpdateNotesAsync(point.Manifest.Id, "note", It.IsAny<IReadOnlyList<string>>(), true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        recovery.Setup(x => x.ApplyRetentionAsync("Ubuntu", 2, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        recovery.Setup(x => x.PreviewDeleteAsync(point.Manifest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryOperationPreview("delete", "Delete", point.Manifest.Id, "Ubuntu", "", point.DirectoryPath, RecoveryPointFormat.Tar, false, false, ["permanent"], 1));
        recovery.Setup(x => x.DeleteAsync(point.Manifest.Id, "delete", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = new BackupTabViewModel(Instance(), backup.Object, dialogs.Object, recovery.Object);
        await vm.InitializeAsync(); vm.SelectedRecoveryPoint = point; vm.RecoveryDescription = "note"; vm.RecoveryTags = "safe, local"; vm.RecoveryPinned = true; vm.RecoveryRetention = 2;

        await vm.SaveRecoveryNotesCommand.ExecuteAsync(null);
        await vm.ApplyRecoveryRetentionCommand.ExecuteAsync(null);
        await vm.DeleteRecoveryPointCommand.ExecuteAsync(null);

        recovery.Verify(x => x.UpdateNotesAsync(point.Manifest.Id, "note", It.Is<IReadOnlyList<string>>(t => t.Count == 2), true, It.IsAny<CancellationToken>()), Times.Once);
        recovery.Verify(x => x.ApplyRetentionAsync("Ubuntu", 2, It.IsAny<CancellationToken>()), Times.Once);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        recovery.Verify(x => x.PreviewDeleteAsync(point.Manifest.Id, It.IsAny<CancellationToken>()), Times.Once);
        recovery.Verify(x => x.DeleteAsync(point.Manifest.Id, "delete", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Initialize_HydratesPersistedRetention_AndSelectedPointMetadata()
    {
        var backup = new Mock<IBackupService>(); backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var point = Point() with { Manifest = Point().Manifest with { Description = "before update", Tags = ["safe", "local"], Pinned = true } };
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([point]);
        recovery.Setup(x => x.GetRetentionAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(4);
        var vm = new BackupTabViewModel(Instance(), backup.Object, new Mock<IDialogService>().Object, recovery.Object);

        await vm.InitializeAsync(); vm.SelectedRecoveryPoint = point;

        Assert.Equal(4, vm.RecoveryRetention);
        Assert.Equal("before update", vm.RecoveryDescription);
        Assert.Equal("safe, local", vm.RecoveryTags);
        Assert.True(vm.RecoveryPinned);
    }

    [Fact]
    public async Task VhdxCapability_ExposesFormatOnlyWhenCorePreflightSupportsIt()
    {
        var backup = new Mock<IBackupService>(); backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.PreviewCreateAsync(It.Is<RecoveryPointCreateRequest>(r => r.Format == RecoveryPointFormat.Vhdx), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryOperationPreview("vhd", "Create", null, "Ubuntu", "", _destination, RecoveryPointFormat.Vhdx, false, true, [], 1));
        var vm = new BackupTabViewModel(Instance(), backup.Object, new Mock<IDialogService>().Object, recovery.Object);

        await vm.InitializeAsync(); vm.DestinationPath = _destination;
        await WaitForAsync(() => vm.CanUseVhdx);

        Assert.Contains(RecoveryPointFormat.Vhdx, vm.RecoveryFormats);
    }

    [Fact]
    public async Task UnsupportedVhdxCapability_RemovesFormatAndImportInPlace()
    {
        var backup = new Mock<IBackupService>(); backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Point() with { Manifest = Point().Manifest with { Format = RecoveryPointFormat.Vhdx } }]);
        recovery.Setup(x => x.PreviewCreateAsync(It.IsAny<RecoveryPointCreateRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("unsupported"));
        var vm = new BackupTabViewModel(Instance(), backup.Object, new Mock<IDialogService>().Object, recovery.Object);

        await vm.InitializeAsync(); vm.RecoveryImportInPlace = true; vm.DestinationPath = _destination;
        await WaitForAsync(() => !vm.CanUseVhdx);

        Assert.DoesNotContain(RecoveryPointFormat.Vhdx, vm.RecoveryFormats);
        Assert.False(vm.RecoveryImportInPlace);
        Assert.False(vm.CanImportInPlace);
    }

    [Fact]
    public async Task UnsupportedVhdxCapability_DisablesBothRestoreModesForSelectedVhdxPoint()
    {
        var backup = new Mock<IBackupService>(); backup.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var point = Point() with { Manifest = Point().Manifest with { Format = RecoveryPointFormat.Vhdx } };
        var recovery = new Mock<IRecoveryPointService>();
        recovery.Setup(x => x.GetHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([point]);
        recovery.Setup(x => x.PreviewCreateAsync(It.IsAny<RecoveryPointCreateRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("unsupported"));
        var vm = new BackupTabViewModel(Instance(), backup.Object, new Mock<IDialogService>().Object, recovery.Object);

        await vm.InitializeAsync(); vm.SelectedRecoveryPoint = point; vm.DestinationPath = _destination;
        await WaitForAsync(() => !vm.CanUseVhdx);

        Assert.False(vm.CanRestoreSelectedRecoveryPoint);
        Assert.False(vm.CanImportInPlace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_destination)) Directory.Delete(_destination, true);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        Assert.True(condition(), "History filter did not finish refreshing.");
    }

    private static WslInstanceViewModel Instance() => new(
        new WslInstance { Name = "Ubuntu", State = "Stopped" },
        new Mock<IWslManagerService>().Object,
        new Mock<ILogger>().Object,
        new Mock<IPowerShellModuleClient>().Object,
        new Mock<IBackupService>().Object,
        new Mock<IServiceProvider>().Object);

    private RecoveryPointSummary Point() => new(new RecoveryPointManifest(1, Guid.NewGuid(), "Before", "Ubuntu", 2, RecoveryPointFormat.Tar, DateTimeOffset.UtcNow, "instance.tar", 1, "hash", "2.3.0", [], ""), _destination, RecoveryPointVerification.Verified);
}
