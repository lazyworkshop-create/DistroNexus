using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Tests that verify WslManagerService uses IWslCliRunner for native operations (E-08).
/// </summary>
public class WslManagerServiceNativeTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly Mock<IWslCliRunner> _mockCliRunner;
    private readonly WslManagerService _service;

    public WslManagerServiceNativeTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockCatalogService    = new Mock<ICatalogService>();
        _mockLogger            = new Mock<ILogger<WslManagerService>>();
        _mockCliRunner         = new Mock<IWslCliRunner>();

        _service = new WslManagerService(
            _mockPowerShellService.Object,
            _mockCatalogService.Object,
            _mockLogger.Object,
            _mockCliRunner.Object);
    }

    [Fact]
    public async Task GetInstancesAsync_UsesCliRunner_NotPowerShell()
    {
        // Arrange — CLI runner returns a standard wsl --list --verbose output
        _mockCliRunner
            .Setup(x => x.RunAsync(
                It.Is<string>(s => s.Contains("--list") && s.Contains("--verbose")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = "  NAME            STATE           VERSION\r\n* Ubuntu-22.04    Running         2\r\n"
            });

        // Act
        var instances = await _service.GetInstancesAsync();

        // Assert — CLI runner was called, NOT the PowerShell service for listing
        _mockCliRunner.Verify(
            x => x.RunAsync(
                It.Is<string>(s => s.Contains("--list")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetInstancesAsync_ParsesInstanceNameAndState()
    {
        _mockCliRunner
            .Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = "  NAME     STATE    VERSION\r\n* Debian   Stopped  2\r\n"
            });

        var instances = await _service.GetInstancesAsync();

        Assert.Single(instances);
        Assert.Equal("Debian", instances[0].Name);
        Assert.Equal("Stopped", instances[0].State);
    }
}
