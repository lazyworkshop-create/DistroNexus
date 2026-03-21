using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for ExportInstanceAsync and ImportInstanceAsync on WslManagerService (E-01).
/// </summary>
public class ExportImportInstanceTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly WslManagerService _service;

    public ExportImportInstanceTests()
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
    public async Task ExportInstanceAsync_Should_Invoke_Export_Cmdlet()
    {
        // Arrange
        const string name = "Ubuntu-22.04";
        const string destination = @"C:\exports\Ubuntu-22.04.tar";

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Export-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Error = string.Empty, UsedModule = true });

        // Act
        await _service.ExportInstanceAsync(name, destination);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Export-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("Name") && (string)d["Name"] == name &&
                    d.ContainsKey("Destination") && (string)d["Destination"] == destination),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportInstanceAsync_Should_Invoke_Import_Cmdlet()
    {
        // Arrange
        const string name = "Ubuntu-Imported";
        const string source = @"C:\exports\backup.tar";
        const string installPath = @"C:\WSL\Ubuntu-Imported";

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Import-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Error = string.Empty, UsedModule = true });

        // Act
        await _service.ImportInstanceAsync(name, source, installPath);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Import-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("Name") && (string)d["Name"] == name &&
                    d.ContainsKey("Source") && (string)d["Source"] == source &&
                    d.ContainsKey("InstallPath") && (string)d["InstallPath"] == installPath),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportInstanceAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.ExportInstanceAsync(null!, @"C:\out.tar"));
    }

    [Fact]
    public async Task ExportInstanceAsync_WithEmptyDestination_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ExportInstanceAsync("Ubuntu", string.Empty));
    }

    [Fact]
    public async Task ImportInstanceAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.ImportInstanceAsync(null!, @"C:\source.tar", @"C:\WSL\New"));
    }

    [Fact]
    public async Task ExportInstanceAsync_WhenFails_Throws_WslExportFailedException()
    {
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Export-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 1, Error = "disk full" });

        await Assert.ThrowsAsync<WslExportFailedException>(
            () => _service.ExportInstanceAsync("Ubuntu", @"C:\out.tar"));
    }

    [Fact]
    public async Task ImportInstanceAsync_WhenFails_Throws_WslImportFailedException()
    {
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Import-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 1, Error = "name taken" });

        await Assert.ThrowsAsync<WslImportFailedException>(
            () => _service.ImportInstanceAsync("Ubuntu", @"C:\src.tar", @"C:\wsl"));
    }
}
