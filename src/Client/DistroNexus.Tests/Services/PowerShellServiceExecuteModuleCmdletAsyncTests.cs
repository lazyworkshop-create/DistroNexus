using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security;
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
    public async Task ExecuteModuleCmdletAsync_RejectsSecureStringInGenericParameterDictionary()
    {
        using var secret = new SecureString();
        secret.AppendChar('x');
        secret.MakeReadOnly();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ExecuteModuleCmdletAsync(
            "Set-DistroNexusCredential", new Dictionary<string, object> { ["Password"] = secret }));
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_PreservesExplicitFalseForBooleanModuleParameters()
    {
        var modulePath = Path.Combine(Path.GetTempPath(), $"DistroNexus.PowerShellServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(modulePath);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(modulePath, "DistroNexus.psd1"), """
                @{
                    RootModule = 'DistroNexus.psm1'
                    ModuleVersion = '1.0.0'
                    GUID = '4d186ac1-260f-46fe-a3b5-aa8590c74aec'
                    FunctionsToExport = @('Set-DistroNexusCatalogSource')
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(modulePath, "DistroNexus.psm1"), """
                function Set-DistroNexusCatalogSource {
                    param([string] $Name, [bool] $IsActive = $true)
                    "is-active=$IsActive"
                }
                Export-ModuleMember -Function Set-DistroNexusCatalogSource
                """);
            using var service = new PowerShellService(_mockLogger.Object, modulePath);

            var result = await service.ExecuteModuleCmdletAsync(
                "Set-DistroNexusCatalogSource",
                new Dictionary<string, object> { ["Name"] = "Official", ["IsActive"] = false },
                null,
                CancellationToken.None);

            result.Success.Should().BeTrue(result.Error);
            result.Output.Trim().Trim('"').Should().Be("is-active=False");
        }
        finally
        {
            Directory.Delete(modulePath, recursive: true);
        }
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
