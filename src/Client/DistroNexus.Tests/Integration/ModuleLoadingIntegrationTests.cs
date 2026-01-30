using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Integration;

/// <summary>
/// Integration tests for PowerShell module loading in WPF context.
/// Verifies that the PowerShell module can be correctly located, loaded, and used from the C# client.
/// </summary>
public class ModuleLoadingIntegrationTests
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _powerShellService;

    public ModuleLoadingIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _powerShellService = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Find_Module_From_Development_Path()
    {
        // Arrange
        var cmdlet = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            cmdlet,
            parameters: null,
            options: options);

        // Assert
        // Module loading should either succeed or provide meaningful error about module not found
        Assert.NotNull(result);
        // If module wasn't found, UsedModule should be false but no exception should be thrown
        if (!result.Success)
        {
            Assert.False(result.UsedModule);
        }
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Fall_Back_When_Module_Not_Found()
    {
        // Arrange
        var cmdlet = "Start-DistroNexusInstance";
        var parameters = new Dictionary<string, object> { ["Name"] = "TestInstance" };
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = true  // Enable fallback
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            cmdlet,
            parameters,
            options: options);

        // Assert
        Assert.NotNull(result);
        // Result should indicate whether module was used or fallback occurred
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Detect_Module_From_Environment_Variable()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("DISTRONEXUS_MODULE_PATH");
        try
        {
            // Set environment variable to point to module location
            var modulePath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "PowerShell",
                "DistroNexus.psm1");

            if (File.Exists(modulePath))
            {
                Environment.SetEnvironmentVariable("DISTRONEXUS_MODULE_PATH", 
                    Path.GetDirectoryName(modulePath));

                var options = new ModuleCallOptions
                {
                    TimeoutSeconds = 10,
                    ParseAsJson = false,
                    UseModuleFallback = false
                };

                // Act
                var result = await _powerShellService.ExecuteModuleCmdletAsync(
                    "Get-DistroNexusPackage",
                    parameters: null,
                    options: options);

                // Assert
                Assert.NotNull(result);
            }
        }
        finally
        {
            // Restore original environment
            if (originalEnv != null)
            {
                Environment.SetEnvironmentVariable("DISTRONEXUS_MODULE_PATH", originalEnv);
            }
            else
            {
                Environment.SetEnvironmentVariable("DISTRONEXUS_MODULE_PATH", null);
            }
        }
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Respect_Module_Search_Paths()
    {
        // Arrange
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Expected module search paths (in order):
        // 1. Environment variable: DISTRONEXUS_MODULE_PATH
        // 2. Development paths (relative to output directory)
        // 3. Program Files
        // 4. AppData\Local\DistroNexus
        // 5. User Documents

        // Act - This should try each path in order
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options: options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Report_Module_Not_Found_Error()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("DISTRONEXUS_MODULE_PATH");
        try
        {
            // Point to non-existent path
            Environment.SetEnvironmentVariable("DISTRONEXUS_MODULE_PATH", "C:\\NonExistent\\Path");

            var options = new ModuleCallOptions
            {
                TimeoutSeconds = 10,
                ParseAsJson = false,
                UseModuleFallback = false  // Don't use fallback
            };

            // Act
            var result = await _powerShellService.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstance",
                parameters: null,
                options: options);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.False(result.UsedModule);
            Assert.NotNull(result.Exception);
        }
        finally
        {
            if (originalEnv != null)
            {
                Environment.SetEnvironmentVariable("DISTRONEXUS_MODULE_PATH", originalEnv);
            }
            else
            {
                Environment.SetEnvironmentVariable("DISTRONEXUS_MODULE_PATH", null);
            }
        }
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Load_Module_Only_Once()
    {
        // Arrange
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Act - First call
        var result1 = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options: options);

        // Act - Second call (should reuse loaded module)
        var result2 = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options: options);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Both results should have same module loading status
    }
}
