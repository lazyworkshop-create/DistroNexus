using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.ViewModelTests.Helpers;
using Moq;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="ResourcesTabViewModel"/>.
/// </summary>
public sealed class ResourcesTabViewModelTests
{
    private static (ResourcesTabViewModel Sut,
                    Mock<IWslManagerService> WslManager,
                    Mock<IWslConfigService> WslConfigService,
                    Mock<IDialogService> DialogService)
        CreateSut(int version = 2, string instanceName = "TestDistro")
    {
        var instance = TestViewModelFactory.CreateInstance(name: instanceName, version: version);
        var wslManager = new Mock<IWslManagerService>();
        var wslConfigService = new Mock<IWslConfigService>();
        var dialogService = new Mock<IDialogService>();

        var instanceVm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogService);

        // Default config service response
        wslConfigService.Setup(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslConfig { Memory = "4GB", Processors = 4, Swap = "2GB" });

        var sut = new ResourcesTabViewModel(instanceVm, wslManager.Object, wslConfigService.Object, dialogService.Object);
        return (sut, wslManager, wslConfigService, dialogService);
    }

    [Fact]
    public async Task InitializeAsync_WslV1Instance_DoesNotCallServices()
    {
        // Arrange
        var (sut, wslManager, wslConfigService, _) = CreateSut(version: 1);

        // Act
        await sut.InitializeAsync();

        // Assert – no service calls because WSL v1 instances are unsupported
        wslManager.Verify(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        wslConfigService.Verify(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>()), Times.Never);
        sut.IsWslV1.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_ConfigReturnsBoolTrue_SetsSparseModeEnabled()
    {
        // Arrange
        var (sut, wslManager, _, _) = CreateSut();
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)true);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.SparseMode.Should().BeTrue();
        sut.SparseModeIndeterminate.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ConfigReturnsBoolFalse_SetsSparseModeDisabled()
    {
        // Arrange
        var (sut, wslManager, _, _) = CreateSut();
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)false);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.SparseMode.Should().BeFalse();
        sut.SparseModeIndeterminate.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ConfigReturnsDict_ReadsSparseKey()
    {
        // Arrange
        var (sut, wslManager, _, _) = CreateSut();
        var dict = new Dictionary<string, object?> { ["sparse"] = (object?)true };
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)dict);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.SparseMode.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_ConfigReturnsNull_SetsIndeterminate()
    {
        // Arrange
        var (sut, wslManager, _, _) = CreateSut();
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.SparseMode.Should().BeNull();
        sut.SparseModeIndeterminate.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_PopulatesGlobalConfigDisplayValues()
    {
        // Arrange
        var (sut, wslManager, wslConfigService, _) = CreateSut();
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)false);
        wslConfigService.Setup(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslConfig { Memory = "8GB", Processors = 4, Swap = "4GB" });

        // Act
        await sut.InitializeAsync();

        // Assert
        sut.MemoryDisplay.Should().Be("8GB");
        sut.CpuDisplay.Should().Be("4");
        sut.SwapDisplay.Should().Be("4GB");
    }

    [Fact]
    public async Task ToggleSparseModeCommand_TogglesFromTrueToFalse()
    {
        // Arrange
        var (sut, wslManager, _, _) = CreateSut();
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)true);
        wslManager.Setup(m => m.SetSparseModeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await sut.InitializeAsync();
        sut.SparseMode.Should().BeTrue();

        // Act
        await sut.ToggleSparseModeCommand.ExecuteAsync(null);

        // Assert
        sut.SparseMode.Should().BeFalse();
        sut.SparseModeIndeterminate.Should().BeFalse();
        wslManager.Verify(m =>
            m.SetSparseModeAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleSparseModeCommand_WhenIndeterminate_DoesNothing()
    {
        // Arrange
        var (sut, wslManager, _, _) = CreateSut();
        wslManager.Setup(m => m.GetInstanceConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);
        await sut.InitializeAsync();
        sut.SparseMode.Should().BeNull();

        // Act
        await sut.ToggleSparseModeCommand.ExecuteAsync(null);

        // Assert – no service call when mode is indeterminate
        wslManager.Verify(m =>
            m.SetSparseModeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
