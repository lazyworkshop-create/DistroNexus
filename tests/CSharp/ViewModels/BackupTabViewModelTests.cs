using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.ViewModelTests.Helpers;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="BackupTabViewModel"/> (D-01).
/// </summary>
public sealed class BackupTabViewModelTests
{
    private static (Mock<IBackupService>, Mock<IDialogService>) CreateMocks()
    {
        var backup = new Mock<IBackupService>();
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);
        return (backup, dialog);
    }

    private static BackupTabViewModel CreateSut(
        WslInstanceViewModel vm,
        Mock<IBackupService> backup,
        Mock<IDialogService> dialog)
        => new(vm, backup.Object, dialog.Object);

    // ── InitializeAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenNoSchedule_LeavesFormEmpty()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync([]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "Ubuntu"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        await sut.InitializeAsync();

        sut.HasSchedule.Should().BeFalse();
        sut.DestinationPath.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WithDailySchedule_PopulatesFormFields()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync([
                  new BackupSchedule
                  {
                      Name           = "Ubuntu",
                      Destination    = @"C:\Backups",
                      Frequency      = "Daily",
                      RetentionCount = 5,
                      Time           = new TimeSpan(3, 30, 0)
                  }
              ]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "Ubuntu"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        await sut.InitializeAsync();

        sut.HasSchedule.Should().BeTrue();
        sut.Frequency.Should().Be(BackupFrequency.Daily);
        sut.DestinationPath.Should().Be(@"C:\Backups");
        sut.RetentionCount.Should().Be(5);
        sut.BackupTimeText.Should().Be("03:30");
    }

    [Fact]
    public async Task InitializeAsync_WithWeeklySchedule_SetsFrequencyAndDay()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync([
                  new BackupSchedule
                  {
                      Name      = "Ubuntu",
                      Destination = @"C:\Backups",
                      Frequency = "Weekly:Friday",
                      RetentionCount = 7,
                      Time      = new TimeSpan(2, 0, 0)
                  }
              ]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "Ubuntu"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        await sut.InitializeAsync();

        sut.Frequency.Should().Be(BackupFrequency.Weekly);
        sut.SelectedDayOfWeek.Should().Be("Friday");
        sut.ShowDayOfWeekPicker.Should().BeTrue();
        sut.ShowDayOfMonthPicker.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithMonthlySchedule_SetsFrequencyAndDay()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync([
                  new BackupSchedule
                  {
                      Name      = "Ubuntu",
                      Destination = @"C:\Backups",
                      Frequency = "Monthly:15",
                      RetentionCount = 3,
                      Time      = new TimeSpan(1, 0, 0)
                  }
              ]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "Ubuntu"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        await sut.InitializeAsync();

        sut.Frequency.Should().Be(BackupFrequency.Monthly);
        sut.SelectedDayOfMonth.Should().Be(15);
        sut.ShowDayOfWeekPicker.Should().BeFalse();
        sut.ShowDayOfMonthPicker.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_OnlyQueryOnce()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(null, wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        await sut.InitializeAsync();
        await sut.InitializeAsync();

        backup.Verify(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SaveSchedule ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveScheduleCommand_CallsServiceWithEncodedFrequency()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        backup.Setup(b => b.SaveScheduleAsync(It.IsAny<BackupSchedule>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "TestDistro"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        sut.DestinationPath = @"C:\Backups";
        sut.Frequency = BackupFrequency.Weekly;
        sut.SelectedDayOfWeek = "Monday";
        sut.RetentionCount = 7;

        await sut.SaveScheduleCommand.ExecuteAsync(null);

        backup.Verify(b => b.SaveScheduleAsync(
            It.Is<BackupSchedule>(s =>
                s.Name == "TestDistro" &&
                s.Frequency == "Weekly:Monday" &&
                s.Destination == @"C:\Backups"),
            It.IsAny<CancellationToken>()), Times.Once);
        sut.HasSchedule.Should().BeTrue();
    }

    [Fact]
    public async Task SaveScheduleCommand_WhenNoDestination_ShowsError()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(null, wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        sut.DestinationPath = string.Empty;

        await sut.SaveScheduleCommand.ExecuteAsync(null);

        backup.Verify(b => b.SaveScheduleAsync(It.IsAny<BackupSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
        dialog.Verify(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── RemoveSchedule ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveScheduleCommand_WhenConfirmed_CallsService()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        backup.Setup(b => b.RemoveScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "TestDistro"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        sut.HasSchedule = true;

        await sut.RemoveScheduleCommand.ExecuteAsync(null);

        backup.Verify(b => b.RemoveScheduleAsync("TestDistro", It.IsAny<CancellationToken>()), Times.Once);
        sut.HasSchedule.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveScheduleCommand_WhenNotConfirmed_DoesNotCallService()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(false);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(null, wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        await sut.RemoveScheduleCommand.ExecuteAsync(null);

        backup.Verify(b => b.RemoveScheduleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── BackupNow ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BackupNowCommand_CallsInvokeBackupAndRefreshesHistory()
    {
        var (backup, dialog) = CreateMocks();
        backup.Setup(b => b.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        backup.Setup(b => b.InvokeBackupAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: "TestDistro"), wslManager, dialogSvc);

        var sut = CreateSut(vm, backup, dialog);
        sut.DestinationPath = @"C:\Backups";
        sut.RetentionCount = 5;

        await sut.BackupNowCommand.ExecuteAsync(null);

        backup.Verify(b => b.InvokeBackupAsync("TestDistro", @"C:\Backups", 5, It.IsAny<CancellationToken>()), Times.Once);
    }
}
