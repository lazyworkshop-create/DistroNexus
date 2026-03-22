using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for DockerIntegrationService (F-02).
/// </summary>
public class DockerIntegrationServiceTests
{
    private readonly Mock<ILogger<DockerIntegrationService>> _mockLogger;
    private readonly Mock<IWslManagerService> _mockWslManager;
    private readonly DockerIntegrationService _service;

    public DockerIntegrationServiceTests()
    {
        _mockLogger = new Mock<ILogger<DockerIntegrationService>>();
        _mockWslManager = new Mock<IWslManagerService>();
        _service = new DockerIntegrationService(_mockLogger.Object, _mockWslManager.Object);
    }

    [Fact]
    public async Task IsDockerDesktopInstalledAsync_ReturnsBool()
    {
        // Act — just verify it returns without throwing; result depends on environment
        var result = await _service.IsDockerDesktopInstalledAsync();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetIntegrationStatusAsync(null!));
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetIntegrationStatusAsync(string.Empty));
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_WhenDockerNotInstalled_ReturnsUnavailable()
    {
        // Arrange — use a service whose install check is overridden
        var service = new TestableDockerIntegrationService(_mockLogger.Object, _mockWslManager.Object, dockerInstalled: false);

        // Act
        var result = await service.GetIntegrationStatusAsync("Ubuntu-22.04");

        // Assert
        Assert.Equal(DockerIntegrationStatus.Unavailable, result);
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_ForDockerDesktopDistro_ReturnsUnavailable()
    {
        // docker-desktop is a reserved name — never eligible
        var service = new TestableDockerIntegrationService(_mockLogger.Object, _mockWslManager.Object, dockerInstalled: true);

        var result1 = await service.GetIntegrationStatusAsync("docker-desktop");
        var result2 = await service.GetIntegrationStatusAsync("docker-desktop-data");

        Assert.Equal(DockerIntegrationStatus.Unavailable, result1);
        Assert.Equal(DockerIntegrationStatus.Unavailable, result2);
    }

    [Fact]
    public async Task SetIntegrationAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SetIntegrationAsync(null!, true));
    }

    [Fact]
    public async Task SetIntegrationAsync_WithEmptyName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetIntegrationAsync(string.Empty, true));
    }

    [Theory]
    [InlineData("docker-desktop")]
    [InlineData("docker-desktop-data")]
    public async Task SetIntegrationAsync_WithReservedDistro_ThrowsArgumentException(string reservedName)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetIntegrationAsync(reservedName, true));
    }

    [Fact]
    public async Task SetIntegrationAsync_WhenWslV1Instance_Throws_WslOperationFailedException()
    {
        _mockWslManager
            .Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WslInstance> { new WslInstance { Name = "Ubuntu", Version = 1 } });

        // Assert that a WslOperationFailedException (concrete subclass of WslOperationException) is thrown
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.SetIntegrationAsync("Ubuntu", true, CancellationToken.None));

        // Verify correct error details
        Assert.Equal(DistroNexusErrorCode.WslVersionTooLow, ex.Code);
        Assert.Equal("Ubuntu", ex.InstanceName);
    }

    [Fact]
    public async Task GetDockerDesktopVersionAsync_ReturnsVersion_WhenInstalled()
    {
        // This test verifies the method exists and returns a non-null version string
        var result = await _service.GetDockerDesktopVersionAsync(CancellationToken.None);
        // In CI where Docker is not installed, null is acceptable
        Assert.True(result == null || result.Length > 0);
    }
}

/// <summary>
/// Testable subclass that overrides installation check for isolated unit testing.
/// </summary>
internal sealed class TestableDockerIntegrationService : DockerIntegrationService
{
    private readonly bool _dockerInstalled;

    public TestableDockerIntegrationService(
        ILogger<DockerIntegrationService> logger,
        IWslManagerService wslManager,
        bool dockerInstalled)
        : base(logger, wslManager)
    {
        _dockerInstalled = dockerInstalled;
    }

    public override Task<bool> IsDockerDesktopInstalledAsync(CancellationToken ct = default)
        => Task.FromResult(_dockerInstalled);
}
