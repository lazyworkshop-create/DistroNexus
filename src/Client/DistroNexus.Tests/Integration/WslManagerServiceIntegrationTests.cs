using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Integration;

/// <summary>
/// Integration tests for WslManagerService with PowerShell module integration.
/// Verifies end-to-end workflows through the service layer.
/// </summary>
public class WslManagerServiceIntegrationTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly WslManagerService _service;

    public WslManagerServiceIntegrationTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockCatalogService = new Mock<ICatalogService>();
        _mockLogger = new Mock<ILogger<WslManagerService>>();
        
        _service = new WslManagerService(
            _mockPowerShellService.Object,
            _mockCatalogService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetInstancesAsync_Should_Use_PowerShell_Module()
    {
        // Arrange
        var mockModuleResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true,
            ParsedObjects = new System.Collections.Generic.List<System.Text.Json.JsonElement>()
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockModuleResult);

        // Act
        var result = await _service.GetInstancesAsync();

        // Assert
        Assert.NotNull(result);
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartInstanceAsync_Should_Call_PowerShell_Module()
    {
        // Arrange
        var instanceName = "Ubuntu-22.04";
        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _service.StartInstanceAsync(instanceName);

        // Assert
        Assert.True(result);
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Start-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d => d["Name"].Equals(instanceName)),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopInstanceAsync_Should_Call_PowerShell_Module()
    {
        // Arrange
        var instanceName = "Ubuntu-22.04";
        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _service.StopInstanceAsync(instanceName);

        // Assert
        Assert.True(result);
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Stop-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d => d["Name"].Equals(instanceName)),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveInstanceAsync_Should_Call_PowerShell_Module()
    {
        // Arrange
        var instanceName = "Ubuntu-22.04";
        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _service.RemoveInstanceAsync(instanceName);

        // Assert
        Assert.True(result);
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Remove-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d => d["Name"].Equals(instanceName)),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MoveInstanceAsync_Should_Call_PowerShell_Module()
    {
        // Arrange
        var instanceName = "Ubuntu-22.04";
        var newPath = "E:\\WSL\\Ubuntu";
        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        await _service.MoveInstanceAsync(instanceName, newPath);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Move-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(
                    d => d["Name"].Equals(instanceName) && d["DestinationPath"].Equals(newPath)),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenameInstanceAsync_Should_Call_PowerShell_Module()
    {
        // Arrange
        var oldName = "Ubuntu-22.04";
        var newName = "Ubuntu-Custom";
        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        await _service.RenameInstanceAsync(oldName, newName);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Rename-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(
                    d => d["OldName"].Equals(oldName) && d["NewName"].Equals(newName)),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetCredentialsAsync_Should_Call_PowerShell_Module()
    {
        // Arrange
        var instanceName = "Ubuntu-22.04";
        var username = "customuser";
        var password = "password123";
        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        await _service.SetCredentialsAsync(instanceName, username, password);

        // Assert
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Set-DistroNexusCredential",
                It.Is<Dictionary<string, object>>(
                    d => d["Name"].Equals(instanceName) &&
                         d["Username"].Equals(username) &&
                         d["Password"].Equals(password)),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallInstanceAsync_Should_Use_Progress_Reporting()
    {
        // Arrange
        var options = new InstallOptions
        {
            InstanceName = "NewInstance",
            InstallPath = "C:\\WSL\\NewInstance",
            Package = new DistroPackage
            {
                Name = "Ubuntu-22.04",
                DownloadUrl = "https://example.com/ubuntu-22.04.tar.gz"
            }
        };

        var mockResult = new PowerShellScriptResult
        {
            ExitCode = 0,
            Error = string.Empty,
            UsedModule = true
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockResult);

        var progressReports = new List<(double, string)>();
        var progress = new Progress<(double Percentage, string Message)>(
            p => progressReports.Add(p));

        // Act
        await _service.InstallInstanceAsync(options, progress);

        // Assert
        // Should have reported progress at various stages
        // At minimum: 5%, 10%, 15%, 30%, 100%
        Assert.NotEmpty(progressReports);
    }

    [Fact]
    public async Task Service_Should_Handle_Module_Failure_With_Fallback()
    {
        // Arrange - Module call fails, fallback should be used
        var mockFailedResult = new PowerShellScriptResult
        {
            ExitCode = 1,
            Error = "Module not found",
            UsedModule = false,
            Exception = new Exception("Module not found")
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(mockFailedResult);

        // Mock fallback script execution
        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(
                It.IsAny<string>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync("success");

        // Act
        var result = await _service.StartInstanceAsync("TestInstance");

        // Assert
        // Should still return successfully (using fallback)
        Assert.True(result);
    }

    [Fact]
    public async Task Service_Should_Validate_Parameters_Before_Calling_Module()
    {
        // Arrange
        var invalidInstanceName = string.Empty;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.StartInstanceAsync(invalidInstanceName));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public async Task Service_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.GetInstancesAsync(cancellationTokenSource.Token));
    }
}
