using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.ViewModelTests.Helpers;
using Moq;
using static DistroNexus.Core.Exceptions.DistroNexusErrorCode;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="DiskTabViewModel.RunCompactionAsync"/>.
/// </summary>
public sealed class DiskTabViewModelTests
{
    private static DiskTabViewModel CreateSut(
        Mock<IWslManagerService> wslManager,
        Mock<IDialogService> dialogService,
        int version = 2,
        string instanceName = "TestDistro")
    {
        var instance = TestViewModelFactory.CreateInstance(
            name: instanceName, version: version);
        var instanceVm = TestViewModelFactory.CreateWslInstanceViewModel(
            instance, wslManager, dialogService);

        return new DiskTabViewModel(instanceVm, wslManager.Object, dialogService.Object);
    }

    [Fact]
    public async Task RunCompactionAsync_SuccessPath_SetsShowResultAndSizes()
    {
        // Arrange
        const long beforeBytes = 2_000_000_000L;
        const long afterBytes  = 1_500_000_000L;

        var wslManager = new Mock<IWslManagerService>();
        wslManager.SetupSequence(m =>
                m.GetInstanceDiskSizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(beforeBytes)
            .ReturnsAsync(afterBytes);
        wslManager.Setup(m =>
                m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dialogService = new Mock<IDialogService>();
        var sut = CreateSut(wslManager, dialogService);

        // Act
        await sut.RunCompactionAsync();

        // Assert
        sut.ShowResult.Should().BeTrue();
        sut.BeforeSizeDisplay.Should().NotBeNullOrEmpty();
        sut.AfterSizeDisplay.Should().NotBeNullOrEmpty();
        sut.SavedSizeDisplay.Should().NotBeNullOrEmpty();
        sut.IsCompacting.Should().BeFalse();

        wslManager.Verify(m =>
            m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCompactionAsync_WslOperationException_ShowsAlertAndNoResult()
    {
        // Arrange
        var wslManager = new Mock<IWslManagerService>();
        wslManager.Setup(m =>
                m.GetInstanceDiskSizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1_000_000L);
        wslManager.Setup(m =>
                m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("Compact failed", CompactionFailed, "compact"));

        var dialogService = new Mock<IDialogService>();
        dialogService.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(wslManager, dialogService);

        // Act
        await sut.RunCompactionAsync();

        // Assert
        sut.ShowResult.Should().BeFalse();
        sut.IsCompacting.Should().BeFalse();
        dialogService.Verify(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RunCompactionAsync_OperationCancelled_CompactsGracefullyNoAlert()
    {
        // Arrange
        var wslManager = new Mock<IWslManagerService>();
        wslManager.Setup(m =>
                m.GetInstanceDiskSizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1_000_000L);
        wslManager.Setup(m =>
                m.CompactInstanceAsync(It.IsAny<string>(), It.IsAny<IProgress<(double, string)>?>(), false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var dialogService = new Mock<IDialogService>();
        var sut = CreateSut(wslManager, dialogService);

        // Act – should not throw
        await sut.RunCompactionAsync();

        // Assert – no alert, no result, clean state
        sut.ShowResult.Should().BeFalse();
        sut.IsCompacting.Should().BeFalse();
        dialogService.Verify(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
