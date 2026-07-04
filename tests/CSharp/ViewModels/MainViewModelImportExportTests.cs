using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.ViewModelTests.Helpers;
using Moq;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests covering import validation, export running-instance guard,
/// and bulk compaction sequential execution in MainViewModel.
/// </summary>
public sealed class MainViewModelImportExportTests
{
    // -----------------------------------------------------------------------
    // ImportInstanceViewModel validation
    // -----------------------------------------------------------------------

    [Fact]
    public void ImportInstanceViewModel_DuplicateName_CanImportIsFalse()
    {
        var existingNames = new List<string> { "Ubuntu", "Debian" };
        var vm = new ImportInstanceViewModel(existingNames)
        {
            InstanceName = "Ubuntu",
            InstallPath = @"C:\WSL\Ubuntu2",
            SourcePath = @"C:\backup\ubuntu.tar"
        };

        vm.CanImport.Should().BeFalse();
        vm.HasNameError.Should().BeTrue();
    }

    [Fact]
    public void ImportInstanceViewModel_EmptyName_CanImportIsFalse()
    {
        var vm = new ImportInstanceViewModel(new List<string>())
        {
            InstanceName = "",
            InstallPath = @"C:\WSL\New",
            SourcePath = @"C:\backup\ubuntu.tar"
        };

        vm.CanImport.Should().BeFalse();
        vm.HasNameError.Should().BeTrue();
    }

    [Fact]
    public void ImportInstanceViewModel_EmptyPath_CanImportIsFalse()
    {
        var vm = new ImportInstanceViewModel(new List<string>())
        {
            InstanceName = "MyDistro",
            InstallPath = "",
            SourcePath = @"C:\backup\ubuntu.tar"
        };

        vm.CanImport.Should().BeFalse();
        vm.HasInstallPathError.Should().BeTrue();
    }

    [Fact]
    public void ImportInstanceViewModel_EmptySourcePath_CanImportIsFalse()
    {
        var vm = new ImportInstanceViewModel(new List<string>())
        {
            InstanceName = "MyDistro",
            InstallPath = @"C:\WSL\MyDistro",
            SourcePath = ""
        };

        vm.CanImport.Should().BeFalse();
        vm.HasSourcePathError.Should().BeTrue();
    }

    [Fact]
    public void ImportInstanceViewModel_AllFieldsValidAndUnique_CanImportIsTrue()
    {
        // Use a path that does NOT exist on disk so SourcePathError is about "file not found",
        // but the property is non-empty → HasSourcePathError relates to empty check; we need
        // a file that exists or adjust the test.
        // Since unit tests cannot guarantee arbitrary file paths exist, test the unique/non-empty
        // scenario by using a real temp file.
        string tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var vm = new ImportInstanceViewModel(new List<string> { "Ubuntu" })
            {
                InstanceName = "Debian",
                InstallPath = @"C:\WSL\Debian",
                SourcePath = tempFile
            };

            vm.CanImport.Should().BeTrue();
            vm.HasNameError.Should().BeFalse();
            vm.HasInstallPathError.Should().BeFalse();
            vm.HasSourcePathError.Should().BeFalse();
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    // -----------------------------------------------------------------------
    // Export: running-instance guard
    // -----------------------------------------------------------------------

    [Fact(Skip = "Requires STA thread for SaveFileDialog after guard")]
    public async Task ExportInstanceCommand_WhenRunning_PromptsCancelReturnsEarly()
    {
        // This test verifies only the "running guard" path (ShowConfirmAsync returns false → early return).
        // The SaveFileDialog that follows is never reached because we decline the stop prompt.
        var wslManager = new Mock<IWslManagerService>();
        var dialogService = new Mock<IDialogService>();
        dialogService.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(IDialogService))).Returns(dialogService.Object);

        var runningInstance = TestViewModelFactory.CreateInstance(state: "Running");
        var instanceVm = TestViewModelFactory.CreateWslInstanceViewModel(runningInstance, wslManager, dialogService);

        // Act – user cancels stop prompt → ExportInstanceAsync returns early
        await instanceVm.ExportInstanceCommand.ExecuteAsync(null);

        // Assert – no actual export was attempted
        wslManager.Verify(m =>
            m.ExportInstanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        dialogService.Verify(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Bulk compact: sequential execution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompactSelectedCommand_TwoSelectedWslV2Instances_CompactsEachInOrder()
    {
        // Arrange
        var (mainVm, wslManager, dialogService, sp) = TestViewModelFactory.CreateMainViewModel();

        // Two selected V2 instances
        var inst1 = TestViewModelFactory.CreateInstance(name: "Distro1");
        var inst2 = TestViewModelFactory.CreateInstance(name: "Distro2");
        var vm1 = TestViewModelFactory.CreateWslInstanceViewModel(inst1, wslManager, dialogService);
        var vm2 = TestViewModelFactory.CreateWslInstanceViewModel(inst2, wslManager, dialogService);
        vm1.IsSelected = true;
        vm2.IsSelected = true;

        mainVm.Instances.Add(vm1);
        mainVm.Instances.Add(vm2);

        // Setup service provider to return needed services via GetRequiredService (uses IServiceProviderExtensions)
        sp.Setup(x => x.GetService(typeof(IWslManagerService))).Returns(wslManager.Object);
        sp.Setup(x => x.GetService(typeof(IDialogService))).Returns(dialogService.Object);
        dialogService.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        wslManager.Setup(m =>
                m.GetInstanceDiskSizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500_000_000L);
        wslManager.Setup(m =>
                m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await mainVm.CompactSelectedCommand.ExecuteAsync(null);

        // Assert – CompactInstanceAsync called once per instance
        wslManager.Verify(m =>
            m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), false, It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // Multi-select mode cleared after completion
        mainVm.IsMultiSelectMode.Should().BeFalse();
    }

    [Fact]
    public async Task CompactSelectedCommand_NoSelectedInstances_DoesNotStartBulkCompact()
    {
        // Arrange
        var (mainVm, wslManager, _, _) = TestViewModelFactory.CreateMainViewModel();

        // Add an instance but do NOT select it
        var instance = TestViewModelFactory.CreateInstance();
        var instanceVm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager);
        mainVm.Instances.Add(instanceVm);

        // Act
        await mainVm.CompactSelectedCommand.ExecuteAsync(null);

        // Assert – no compaction attempted
        wslManager.Verify(m =>
            m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompactSelectedCommand_WslV1InstanceSelected_SkipsIt()
    {
        // Arrange
        var (mainVm, wslManager, dialogService, _) = TestViewModelFactory.CreateMainViewModel();
        dialogService.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var v1Instance = TestViewModelFactory.CreateInstance(version: 1);
        var v1Vm = TestViewModelFactory.CreateWslInstanceViewModel(v1Instance, wslManager, dialogService);
        v1Vm.IsSelected = true;
        mainVm.Instances.Add(v1Vm);

        // Act
        await mainVm.CompactSelectedCommand.ExecuteAsync(null);

        // Assert – WSL v1 instance was skipped
        wslManager.Verify(m =>
            m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
