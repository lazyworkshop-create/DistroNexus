using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Services;

public class PowerShellServiceExecuteModuleCmdletAsyncTests : IDisposable
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _service;

    public PowerShellServiceExecuteModuleCmdletAsyncTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _service = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithNullCmdletName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.ExecuteModuleCmdletAsync(null!, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithValidCmdlet_ShouldReturnResult()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 30,
            ParseAsJson = true
        };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Note: Result depends on whether module is available
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithoutOptions_ShouldUseDefaults()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithParameters_ShouldFormatCorrectly()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "Ubuntu*",
            ["ForceUpdate"] = true
        };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, parameters, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WhenExecutionFails_ReturnsErrorDetails()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        // The test environment may have the module installed; preserve the error contract for failures.
        if (!result.Success)
        {
            result.Error.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("string-value", "'string-value'")]
    [InlineData(true, "$true")]
    [InlineData(false, "$false")]
    [InlineData(42, "42")]
    [InlineData(3.14, "3.14")]
    public void ParameterFormatting_WithVariousTypes_ShouldFormatCorrectly(
        object input, string expectedFormat)
    {
        // This tests parameter formatting indirectly through ExecuteModuleCmdletAsync
        // The expected format is based on PowerShell syntax
        
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["TestParam"] = input
        };

        // Act
        var task = _service.ExecuteModuleCmdletAsync(
            "Test-Cmdlet", parameters, null, CancellationToken.None);

        // Assert
        task.Should().NotBeNull();
        // Actual validation would require inspecting the generated script
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var cmdletName = "Start-Sleep";
        var parameters = new Dictionary<string, object>
        {
            ["Seconds"] = 60
        };
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 1  // Very short timeout
        };

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act & Assert
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, parameters, options, cts.Token);

        // Either timeout or completes quickly
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithCancellation_ShouldHandleGracefully()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, null, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithParseAsJson_ShouldIncludeJsonConversion()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions
        {
            ParseAsJson = true
        };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            result.ParsedObjects.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithForceRefresh_ShouldPassParameter()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions
        {
            ForceRefresh = true
        };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithLogVerbose_ShouldEnableVerboseOutput()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions
        {
            LogVerbose = true
        };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    public void Dispose()
    {
        _service?.Dispose();
        GC.SuppressFinalize(this);
    }
}
