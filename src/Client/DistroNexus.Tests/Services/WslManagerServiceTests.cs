using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Tests for WslManagerService using mocked dependencies.
/// </summary>
public class WslManagerServiceTests
{
    private readonly Mock<IWslManagerService> _mockWslManager;

    public WslManagerServiceTests()
    {
        _mockWslManager = new Mock<IWslManagerService>();
    }

    [Fact]
    public async Task GetInstancesAsync_ReturnsListOfInstances()
    {
        // Arrange
        var expectedInstances = new List<WslInstance>
        {
            new() { Name = "Ubuntu-22.04", State = "Running", Version = 2 },
            new() { Name = "Debian", State = "Stopped", Version = 2 }
        };

        _mockWslManager
            .Setup(x => x.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInstances);

        // Act
        var result = await _mockWslManager.Object.GetInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, i => i.Name == "Ubuntu-22.04");
        Assert.Contains(result, i => i.Name == "Debian");
    }

    [Fact]
    public async Task GetInstancesAsync_WhenNoInstances_ReturnsEmptyList()
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WslInstance>());

        // Act
        var result = await _mockWslManager.Object.GetInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task StartInstanceAsync_WithValidName_ReturnsTrue()
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.StartInstanceAsync("Ubuntu-22.04", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockWslManager.Object.StartInstanceAsync("Ubuntu-22.04");

        // Assert
        Assert.True(result);
        _mockWslManager.Verify(x => x.StartInstanceAsync("Ubuntu-22.04", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartInstanceAsync_WithInvalidName_ReturnsFalse()
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.StartInstanceAsync("NonExistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _mockWslManager.Object.StartInstanceAsync("NonExistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StopInstanceAsync_WithRunningInstance_ReturnsTrue()
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.StopInstanceAsync("Ubuntu-22.04", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockWslManager.Object.StopInstanceAsync("Ubuntu-22.04");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveInstanceAsync_WithValidName_ReturnsTrue()
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.RemoveInstanceAsync("TestInstance", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockWslManager.Object.RemoveInstanceAsync("TestInstance");

        // Assert
        Assert.True(result);
        _mockWslManager.Verify(x => x.RemoveInstanceAsync("TestInstance", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallInstanceAsync_WithValidOptions_CompletesSuccessfully()
    {
        // Arrange
        var options = new InstallOptions
        {
            InstanceName = "TestUbuntu",
            InstallPath = @"D:\WSL\Test",
            Username = "testuser"
        };

        _mockWslManager
            .Setup(x => x.InstallInstanceAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<(double, string)>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        await _mockWslManager.Object.InstallInstanceAsync(options);
        _mockWslManager.Verify(x => x.InstallInstanceAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<(double, string)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Ubuntu-22.04", @"D:\NewPath")]
    [InlineData("Debian", @"E:\Linux\Debian")]
    public async Task MoveInstanceAsync_WithValidParameters_CompletesSuccessfully(string name, string newPath)
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.MoveInstanceAsync(name, newPath, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Should not throw
        await _mockWslManager.Object.MoveInstanceAsync(name, newPath);

        // Assert
        _mockWslManager.Verify(x => x.MoveInstanceAsync(name, newPath, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("OldName", "NewName")]
    public async Task RenameInstanceAsync_WithValidParameters_CompletesSuccessfully(string oldName, string newName)
    {
        // Arrange
        _mockWslManager
            .Setup(x => x.RenameInstanceAsync(oldName, newName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - Should not throw
        await _mockWslManager.Object.RenameInstanceAsync(oldName, newName);

        // Assert
        _mockWslManager.Verify(x => x.RenameInstanceAsync(oldName, newName, It.IsAny<CancellationToken>()), Times.Once);
    }
}
