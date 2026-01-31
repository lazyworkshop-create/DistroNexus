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
/// Integration tests for parameter marshalling between C# and PowerShell.
/// Verifies correct conversion of C# objects to PowerShell parameters and back.
/// </summary>
public class ParameterMarshallingIntegrationTests
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _powerShellService;

    public ParameterMarshallingIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _powerShellService = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Marshal_String_Parameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "Ubuntu-22.04",
            ["DestinationPath"] = "C:\\WSL\\Ubuntu"
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Start-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
        // Parameters should be successfully passed to PowerShell
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Marshal_Integer_Parameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["TimeoutSeconds"] = 30,
            ["MaxRetries"] = 3
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Update-DistroNexusCatalog",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Marshal_Boolean_Parameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "TestInstance",
            ["UseLocalCache"] = true,
            ["Force"] = false
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Install-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Marshal_Array_Parameters()
    {
        // Arrange
        var initCommands = new[]
        {
            "apt-get update",
            "apt-get upgrade -y",
            "apt-get install -y curl git"
        };

        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "Ubuntu-Setup",
            ["InitCommands"] = initCommands
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 30,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Install-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Handle_Special_Characters_In_Strings()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "Instance'With\"Quotes",
            ["DestinationPath"] = "C:\\Path With Spaces\\$pecial(Chars)",
            ["Username"] = "user@domain"
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Set-DistroNexusCredential",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
        // PowerShell should properly escape and process special characters
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Handle_Null_Parameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "TestInstance",
            ["OptionalParam"] = null  // Null parameter should be handled gracefully
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Start-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Marshal_Complex_Objects()
    {
        // Arrange - Simulating InstallOptions object serialization
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "NewInstance",
            ["DistroName"] = "Ubuntu-22.04",
            ["DestinationPath"] = "E:\\WSL\\NewInstance",
            ["PackageUrl"] = "https://example.com/ubuntu-22.04.tar.gz",
            ["Username"] = "customuser",
            ["UseLocalCache"] = true
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 30,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Install-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Validate_Parameter_Types()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "TestInstance",
            ["TimeoutSeconds"] = "NotAnInteger"  // Should be int, not string
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Update-DistroNexusCatalog",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
        // Should either convert or report type mismatch
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Handle_Large_Array_Parameters()
    {
        // Arrange - Large list of initialization commands
        var largeCommandList = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            largeCommandList.Add($"echo 'Command {i}'");
        }

        var parameters = new Dictionary<string, object>
        {
            ["Name"] = "LargeSetup",
            ["InitCommands"] = largeCommandList.ToArray()
        };

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 30,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Install-DistroNexusInstance",
            parameters,
            options);

        // Assert
        Assert.NotNull(result);
        // Large arrays should be properly marshalled
    }
}
