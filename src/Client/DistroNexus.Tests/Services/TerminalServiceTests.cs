using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for TerminalService.
/// </summary>
public class TerminalServiceTests
{
    private readonly Mock<IPowerShellService> _mockPowerShell;
    private readonly Mock<ILogger<TerminalService>> _mockLogger;
    private readonly TerminalService _terminalService;

    public TerminalServiceTests()
    {
        _mockPowerShell = new Mock<IPowerShellService>();
        _mockLogger = new Mock<ILogger<TerminalService>>();
        _terminalService = new TerminalService(_mockPowerShell.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task OpenTerminalAsync_WithValidInstance_ShouldReturnTrue()
    {
        // Arrange
        var instanceName = "Ubuntu";
        var expectedResult = new PowerShellScriptResult { ExitCode = 0, Output = "Success" };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.OpenTerminalAsync(instanceName);

        // Assert
        Assert.True(result);
        _mockPowerShell.Verify(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenTerminalAsync_WithFailedScript_ShouldReturnFalse()
    {
        // Arrange
        var instanceName = "Ubuntu";
        var expectedResult = new PowerShellScriptResult { ExitCode = 1, Error = "Failed" };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.OpenTerminalAsync(instanceName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task OpenTerminalAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var instanceName = "Ubuntu";
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _terminalService.OpenTerminalAsync(instanceName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task OpenTerminalInDirectoryAsync_WithValidParameters_ShouldReturnTrue()
    {
        // Arrange
        var instanceName = "Ubuntu";
        var workingDirectory = "/home/user";
        var expectedResult = new PowerShellScriptResult { ExitCode = 0, Output = "Success" };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.OpenTerminalInDirectoryAsync(instanceName, workingDirectory);

        // Assert
        Assert.True(result);
        _mockPowerShell.Verify(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenFileExplorerAsync_WithValidPath_ShouldReturnTrue()
    {
        // Arrange
        var folderPath = "C:\\Users";
        var expectedResult = new PowerShellScriptResult { ExitCode = 0, Output = "Success" };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.OpenFileExplorerAsync(folderPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task OpenFileExplorerAsync_WithInvalidPath_ShouldReturnFalse()
    {
        // Arrange
        var folderPath = "C:\\InvalidPath";
        var expectedResult = new PowerShellScriptResult { ExitCode = 1, Error = "Path does not exist" };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.OpenFileExplorerAsync(folderPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAvailableTerminalsAsync_WithSuccessfulScript_ShouldReturnTerminals()
    {
        // Arrange
        var terminalsJson = "[\"Windows Terminal\", \"Command Prompt\", \"PowerShell\"]";
        var expectedResult = new PowerShellScriptResult { ExitCode = 0, Output = terminalsJson };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.GetAvailableTerminalsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("Windows Terminal", result);
        Assert.Contains("Command Prompt", result);
        Assert.Contains("PowerShell", result);
    }

    [Fact]
    public async Task GetAvailableTerminalsAsync_WithNoTerminals_ShouldReturnEmptyList()
    {
        // Arrange
        var expectedResult = new PowerShellScriptResult { ExitCode = 0, Output = "" };
        
        _mockPowerShell.Setup(x => x.ExecuteScriptWithResultAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _terminalService.GetAvailableTerminalsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}