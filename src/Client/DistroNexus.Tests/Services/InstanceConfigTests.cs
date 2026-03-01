using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for GetInstanceConfigAsync and SetSparseModeAsync on WslManagerService (E-03).
/// </summary>
public class InstanceConfigTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly WslManagerService _service;

    public InstanceConfigTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockCatalogService    = new Mock<ICatalogService>();
        _mockLogger            = new Mock<ILogger<WslManagerService>>();

        _service = new WslManagerService(
            _mockPowerShellService.Object,
            _mockCatalogService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetInstanceConfigAsync_Should_Invoke_GetInstanceConfig_Cmdlet()
    {
        // Arrange
        const string name = "Ubuntu-22.04";

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstanceConfig",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Error = string.Empty, UsedModule = true,
                Output = "{\"Name\":\"Ubuntu-22.04\",\"SparseMode\":false}" });

        // Act
        var result = await _service.GetInstanceConfigAsync(name);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstanceConfig",
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("Name") && (string)d["Name"] == name),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetSparseModeAsync_Should_Invoke_SetSparseMode_Cmdlet()
    {
        // Arrange
        const string name = "Ubuntu-22.04";

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Set-DistroNexusInstanceSparseMode",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Error = string.Empty, UsedModule = true });

        // Act
        await _service.SetSparseModeAsync(name, enabled: true);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Set-DistroNexusInstanceSparseMode",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("Name") && (string)d["Name"] == name &&
                    d.ContainsKey("Enabled") && (bool)d["Enabled"] == true),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetInstanceConfigAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetInstanceConfigAsync(null!));
    }

    [Fact]
    public async Task SetSparseModeAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SetSparseModeAsync(null!, true));
    }
}
