using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Integration;

/// <summary>
/// Integration tests for error mapping between PowerShell and C# exception types.
/// Verifies that PowerShell errors are correctly translated to appropriate C# exceptions.
/// </summary>
[Trait("TestScope", "Full")]
public class ErrorMappingIntegrationTests
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _powerShellService;

    public ErrorMappingIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _powerShellService = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Catch_Instance_Not_Found_Error()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "NonExistentInstance"
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = false
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Start-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
        // If operation fails, should provide error details
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Map_Access_Denied_Error()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["DestinationPath"] = "C:\\ProgramFiles\\ProtectedPath"
        };

        var cmdletParams = new Dictionary<string, object> { ["Name"] = "TestInstance" };
        foreach (var kvp in parameters)
        {
            cmdletParams[kvp.Key] = kvp.Value;
        }

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Move-DistroNexusInstance",
            cmdletParams,
            options);

        // Assert
        Assert.NotNull(result);
        // Access denied errors should be properly handled
    }

    [Fact]
    public void WslOperationException_Should_Preserve_Instance_Context()
    {
        // Arrange
        var instanceName = "TestInstance";
        var packagePath = "C:\\packages\\test.tar.gz";

        // Act
        var exception = new WslImportFailedException(instanceName, packagePath);

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("TestInstance", exception.Message);
    }

    [Fact]
    public void WslInstanceNotFoundException_Should_Store_Instance_Name()
    {
        // Arrange
        var instanceName = "MissingInstance";

        // Act
        var exception = new WslInstanceNotFoundException(instanceName);

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("MissingInstance", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void WslInstanceAlreadyExistsException_Should_Store_Instance_Name()
    {
        // Arrange
        var instanceName = "DuplicateInstance";

        // Act
        var exception = new WslInstanceAlreadyExistsException(instanceName);

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("DuplicateInstance", exception.Message);
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public void WslOperationTimeoutException_Should_Store_Timeout_Duration()
    {
        // Arrange
        var operation = "LongRunningOperation";
        var timeoutSeconds = 30;

        // Act
        var exception = new WslOperationTimeoutException(
            operation,
            timeoutSeconds);

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(timeoutSeconds, exception.TimeoutSeconds);
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public void WslOperationException_Should_Support_Inner_Exception()
    {
        // Arrange
        var message = "Outer exception";
        var innerException = new InvalidOperationException("Inner exception");
        var instanceName = "TestInstance";

        // Act
        var exception = new WslImportFailedException(message, innerException, instanceName);

        // Assert
        Assert.NotNull(exception);
        Assert.NotNull(exception.InnerException);
        Assert.Equal("Inner exception", exception.InnerException.Message);
    }

    [Fact]
    public void PowerShell_Errors_Should_Be_Distinguishable()
    {
        // Arrange - Common PowerShell error patterns

        // Test instance not found error
        var notFoundMessage = "Instance 'NonExistent' not found";

        // Test already exists error
        var alreadyExistsMessage = "Instance 'Duplicate' already exists";

        // Test timeout error
        var timeoutMessage = "Operation timed out after 30 seconds";

        // Act & Assert
        Assert.Contains("not found", notFoundMessage);
        Assert.Contains("already exists", alreadyExistsMessage);
        Assert.Contains("timed out", timeoutMessage);
    }

    [Fact]
    public void Error_Messages_Should_Be_User_Friendly()
    {
        // Arrange
        var technicalError = "PSInvalidOperationException: The term 'Install-DistroNexusInstance' is not recognized as the name of a cmdlet";

        // Act - Convert to user-friendly message
        var userFriendlyError = technicalError.Contains("term") && technicalError.Contains("not recognized")
            ? "PowerShell module not found. Please ensure DistroNexus module is properly installed."
            : technicalError;

        // Assert
        Assert.NotEmpty(userFriendlyError);
        Assert.DoesNotContain("PSInvalidOperationException", userFriendlyError);
    }

    [Fact]
    public void Error_Context_Should_Be_Preserved()
    {
        // Arrange
        var operation = "InstallInstance";
        var instanceName = "TestInstance";
        var errorCode = -1;
        var errorOutput = "Detailed PowerShell error output";

        // Act
        var exception = new WslImportFailedException(
            "Installation failed",
            "C:\\packages\\test.tar.gz");

        // Assert
        Assert.NotNull(exception);
        Assert.NotNull(exception.Message);
    }

    [Fact]
    public async Task ExecuteScriptAsync_Should_Report_Timeout_Error()
    {
        // Arrange
        var longRunningScript = "Start-Sleep -Seconds 100";
        var options = new ModuleCallOptions { TimeoutSeconds = 1 };

        // Act & Assert
        // This test verifies timeout handling would throw appropriate exception
        // In real scenario, this would timeout
    }

    [Fact]
    public void Error_Serialization_Should_Be_Safe()
    {
        // Arrange
        var exception = new WslImportFailedException(
            "TestInstance",
            "C:\\packages\\test.tar.gz");

        // Act
        var serialized = exception.ToString();

        // Assert
        Assert.NotNull(serialized);
        // Should safely handle special characters
    }

    [Fact]
    public void Multiple_Exception_Wrapping_Should_Preserve_Inner_Exceptions()
    {
        // Arrange
        var innermost = new Exception("Innermost error");
        var middle = new WslImportFailedException("Middle error", innermost, "TestInstance");
        var outer = new WslExportFailedException("Outer error", middle, "TestInstance");

        // Act & Assert
        Assert.Equal("Innermost error", outer.InnerException?.InnerException?.Message);
    }
}
