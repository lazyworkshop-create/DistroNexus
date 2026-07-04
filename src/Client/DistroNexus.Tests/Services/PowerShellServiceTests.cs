using System;
using System.Threading.Tasks;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for PowerShellService.
/// </summary>
public class PowerShellServiceTests : IDisposable
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _powerShellService;

    public PowerShellServiceTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _powerShellService = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PowerShellService(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullCmdlet_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _powerShellService.ExecuteAsync<object>(null!));
    }

    [Fact]
    public async Task ExecuteAsync_WithStringResult_ShouldReturnString()
    {
        // Arrange
        var cmdlet = "Get-Date";
        
        // Act
        var result = await _powerShellService.ExecuteAsync<string>(cmdlet);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ExecuteScriptWithResultAsync_WithNullScript_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _powerShellService.ExecuteScriptWithResultAsync(null!));
    }

    [Fact]
    public async Task ExecuteScriptWithResultAsync_WithValidScript_ShouldReturnSuccessResult()
    {
        // Arrange
        var script = "Get-Date";

        // Act
        var result = await _powerShellService.ExecuteScriptWithResultAsync(script);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Output);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteScriptWithResultAsync_WithInvalidScript_ShouldReturnFailureResult()
    {
        // Arrange
        var script = "Get-InvalidCommand";

        // Act
        var result = await _powerShellService.ExecuteScriptWithResultAsync(script);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteScriptAsync_WithNullScript_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _powerShellService.ExecuteScriptAsync(null!));
    }

    [Fact]
    public async Task ExecuteScriptAsync_WithValidScript_ShouldReturnOutput()
    {
        // Arrange
        var script = "Get-Date";

        // Act
        var result = await _powerShellService.ExecuteScriptAsync(script);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ExecuteScriptAsync_WithClixmlNoise_ShouldThrowSanitizedError()
    {
        // Arrange
        var script = "[Console]::Error.WriteLine('#< CLIXML version=\"1.0\"?><Objs></Objs>'); " +
                     "[Console]::Error.WriteLine('WARNING: The names of some imported commands include unapproved verbs.'); " +
                     "[Console]::Error.WriteLine('Actual failure from script'); exit 1";

        // Act
        var exception = await Assert.ThrowsAsync<WslOperationFailedException>(() => _powerShellService.ExecuteScriptAsync(script));

        // Assert
        Assert.Equal(DistroNexusErrorCode.PowerShellModuleUnavailable, exception.Code);
        Assert.Contains("Actual failure from script", exception.Message);
        Assert.DoesNotContain("CLIXML", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unapproved verbs", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportModuleAsync_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _powerShellService.ImportModuleAsync(null!));
    }

    [Fact]
    public async Task ImportModuleAsync_WithInvalidPath_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var invalidPath = "C:\\Invalid\\Module.psm1";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _powerShellService.ImportModuleAsync(invalidPath));
    }

    [Fact]
    public async Task IsModuleLoadedAsync_ShouldReturnBoolean()
    {
        // Act
        var result = await _powerShellService.IsModuleLoadedAsync();

        // Assert
        Assert.IsType<bool>(result);
    }

    public void Dispose()
    {
        _powerShellService?.Dispose();
    }
}
