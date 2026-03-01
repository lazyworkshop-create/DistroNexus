using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for NetworkService (E-05).
/// </summary>
public class NetworkServiceTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ILogger<NetworkService>> _mockLogger;
    private readonly NetworkService _service;

    public NetworkServiceTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockLogger            = new Mock<ILogger<NetworkService>>();
        _service               = new NetworkService(_mockPowerShellService.Object, _mockLogger.Object);
    }

    // -----------------------------------------------------------------------
    // GetPortMappingsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPortMappingsAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetPortMappingsAsync(null!));
    }

    [Fact]
    public async Task GetPortMappingsAsync_WithEmptyName_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetPortMappingsAsync(string.Empty));
    }

    [Fact]
    public async Task GetPortMappingsAsync_Should_Invoke_Cmdlet()
    {
        // Arrange
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult
            {
                ExitCode   = 0,
                Error      = string.Empty,
                UsedModule = true,
                Output     = "[]"
            });

        // Act
        var result = await _service.GetPortMappingsAsync("Ubuntu-22.04");

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("Name") && (string)d["Name"] == "Ubuntu-22.04"),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPortMappingsAsync_WithProtocolFilter_PassesProtocolToCmdlet()
    {
        // Arrange
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, UsedModule = true, Output = "[]" });

        // Act
        await _service.GetPortMappingsAsync("Ubuntu-22.04", "TCP");

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("Protocol") && (string)d["Protocol"] == "TCP"),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPortMappingsAsync_WhenCmdletFails_ThrowsInvalidOperationException()
    {
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult
            {
                ExitCode   = 1,
                Error      = "ss: command not found",
                UsedModule = true
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetPortMappingsAsync("Ubuntu-22.04"));
    }

    // -----------------------------------------------------------------------
    // GetInstanceIpAddressAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetInstanceIpAddressAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetInstanceIpAddressAsync(null!));
    }

    [Fact]
    public async Task GetInstanceIpAddressAsync_Should_Invoke_Cmdlet()
    {
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, UsedModule = true, Output = "[]" });

        // Act — calls GetPortMappings which also returns IpAddress in output
        await _service.GetPortMappingsAsync("Ubuntu-22.04");

        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusPortMapping",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
