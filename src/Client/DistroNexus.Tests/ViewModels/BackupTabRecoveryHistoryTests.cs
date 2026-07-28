using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class BackupTabRecoveryHistoryTests
{
    [Fact]
    public async Task Initialize_UsesOnlyTypedModuleReadRoutes()
    {
        var history = new[]
        {
            new RecoveryHistoryEntry("scheduled", "Ubuntu", DateTimeOffset.UtcNow, "ScheduledBackup", "Configured", "destination"),
            new RecoveryHistoryEntry("recovery", "Ubuntu", DateTimeOffset.UtcNow, "RecoveryPoint", "Completed", "point")
        };
        var client = NewClient();
        client.Setup(x => x.GetBackupSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new BackupSchedule { Name = "Ubuntu", Destination = "destination", Frequency = "Daily", RetentionCount = 2, Time = TimeSpan.Zero }]);
        client.Setup(x => x.GetRecoveryHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(history);

        var viewModel = NewViewModel(client.Object);
        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasSchedule);
        Assert.Contains(viewModel.BackupHistory, x => x.Kind == "ScheduledBackup");
        Assert.Contains(viewModel.BackupHistory, x => x.Kind == "RecoveryPoint");
        client.Verify(x => x.GetBackupSchedulesAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.GetRecoveryHistoryAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.GetRecoveryPointsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Restore_UsesTypedPreviewAndExecutionRoutes()
    {
        var point = Point();
        var preview = new RecoveryOperationPreview("restore-token", "Restore", point.Manifest.Id, "Ubuntu", "Clone", "target", RecoveryPointFormat.Tar, false, false, ["distinct"], 1);
        var client = NewClient(points: [point]);
        client.Setup(x => x.GetRecoveryRestorePreviewAsync(It.Is<RecoveryRestoreRequest>(r => r.TargetInstance == "Clone"), It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        client.Setup(x => x.RestoreRecoveryPointAsync(preview, It.Is<RecoveryRestoreRequest>(r => r.TargetInstance == "Clone"), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dialogs = Dialogs(confirm: true);
        var vm = NewViewModel(client.Object, dialogs.Object);
        await vm.InitializeAsync();
        vm.SelectedRecoveryPoint = point;
        vm.RecoveryTargetInstance = "Clone";
        vm.RecoveryTargetDirectory = "target";

        await vm.RestoreRecoveryPointCommand.ExecuteAsync(null);

        client.Verify(x => x.GetRecoveryRestorePreviewAsync(It.IsAny<RecoveryRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.RestoreRecoveryPointAsync(preview, It.IsAny<RecoveryRestoreRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MetadataAndRetention_UseTypedModuleRoutes()
    {
        var point = Point();
        var client = NewClient(points: [point]);
        var notes = new RecoveryOperationPreview(new string('a', 32), "Notes", point.Manifest.Id, "Ubuntu", "", "point", RecoveryPointFormat.Tar, false, false, ["Update recovery point metadata."], 1);
        client.Setup(x => x.PreviewRecoveryPointNotesAsync(point.Manifest.Id, "note", It.Is<IReadOnlyList<string>>(x => x.Count == 2), true, It.IsAny<CancellationToken>())).ReturnsAsync(notes);
        client.Setup(x => x.ExecuteRecoveryPointNotesAsync(notes.Token, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var retention = new RecoveryRetentionPreview("retention-token", "Ubuntu", 2, null, 0, "fingerprint");
        client.Setup(x => x.GetRecoveryRetentionPreviewAsync("Ubuntu", 2, It.IsAny<CancellationToken>())).ReturnsAsync(retention);
        client.Setup(x => x.SetRecoveryRetentionAsync(retention, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var vm = NewViewModel(client.Object, Dialogs(confirm: true).Object);
        await vm.InitializeAsync();
        vm.SelectedRecoveryPoint = point;
        vm.RecoveryDescription = "note";
        vm.RecoveryTags = "safe, local";
        vm.RecoveryPinned = true;
        vm.RecoveryRetention = 2;

        await vm.SaveRecoveryNotesCommand.ExecuteAsync(null);
        await vm.ApplyRecoveryRetentionCommand.ExecuteAsync(null);

        client.Verify(x => x.PreviewRecoveryPointNotesAsync(point.Manifest.Id, "note", It.Is<IReadOnlyList<string>>(x => x.Count == 2 && x[0] == "safe" && x[1] == "local"), true, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.ExecuteRecoveryPointNotesAsync(notes.Token, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.SetRecoveryRetentionAsync(retention, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_UsesTypedPreviewAndExecutionRoutes()
    {
        var point = Point();
        var preview = new RecoveryOperationPreview("delete-token", "Delete", point.Manifest.Id, "Ubuntu", "", "point", RecoveryPointFormat.Tar, false, false, ["permanent"], 1);
        var client = NewClient(points: [point]);
        client.Setup(x => x.GetRecoveryRemovePreviewAsync(point.Manifest.Id, It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        client.Setup(x => x.RemoveRecoveryPointAsync(preview, point.Manifest.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var vm = NewViewModel(client.Object, Dialogs(confirm: true).Object);
        await vm.InitializeAsync();
        vm.SelectedRecoveryPoint = point;

        await vm.DeleteRecoveryPointCommand.ExecuteAsync(null);

        client.Verify(x => x.GetRecoveryRemovePreviewAsync(point.Manifest.Id, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.RemoveRecoveryPointAsync(preview, point.Manifest.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IPowerShellModuleClient> NewClient(IReadOnlyList<RecoveryPointSummary>? points = null)
    {
        var client = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        client.Setup(x => x.GetBackupSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        client.Setup(x => x.GetRecoveryHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        client.Setup(x => x.GetRecoveryPointsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(points ?? []);
        client.Setup(x => x.GetRecoveryRetentionAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        return client;
    }

    private static Mock<IDialogService> Dialogs(bool confirm = false)
    {
        var dialogs = new Mock<IDialogService>(MockBehavior.Loose);
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirm);
        return dialogs;
    }

    private static BackupTabViewModel NewViewModel(IPowerShellModuleClient client, IDialogService? dialogs = null) => new(Instance(), dialogs ?? Dialogs().Object, client);

    private static WslInstanceViewModel Instance() => new(new WslInstance { Name = "Ubuntu", State = "Stopped" }, Mock.Of<IWslManagerService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IServiceProvider>());

    private static RecoveryPointSummary Point() => new(new RecoveryPointManifest(1, Guid.NewGuid(), "Before", "Ubuntu", 2, RecoveryPointFormat.Tar, DateTimeOffset.UtcNow, "instance.tar", 1, "hash", "2.3.0", [], ""), "point", RecoveryPointVerification.Verified);
}
