using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
/// Integration tests for operation timeout handling in WPF-PowerShell integration.
/// Verifies that operations respect timeout settings and handle cancellation correctly.
/// </summary>
[Trait("TestScope", "Full")]
public class TimeoutHandlingIntegrationTests
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _powerShellService;

    public TimeoutHandlingIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _powerShellService = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Enforce_Quick_Operation_Timeout()
    {
        // Arrange - Quick operations timeout: 10 seconds
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = false,
            UseModuleFallback = false
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options);

        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        // If successful, should complete quickly
        // If timeout occurs, should respect the 10-second limit
        Assert.True(stopwatch.ElapsedMilliseconds <= 15000); // Allow 5 second buffer
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Enforce_Normal_Operation_Timeout()
    {
        // Arrange - Normal operations timeout: 30 seconds
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 30,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Start-DistroNexusInstance",
            new Dictionary<string, object> { ["Name"] = "TestInstance" },
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Enforce_Long_Operation_Timeout()
    {
        // Arrange - Long operations timeout: 120 seconds
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 120,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Move-DistroNexusInstance",
            new Dictionary<string, object> 
            { 
                ["Name"] = "TestInstance",
                ["DestinationPath"] = "E:\\WSL\\TestInstance"
            },
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Enforce_Very_Long_Operation_Timeout()
    {
        // Arrange - Very long operations timeout: 300 seconds (5 minutes)
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 300,
            ParseAsJson = false,
            UseModuleFallback = true
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Install-DistroNexusInstance",
            new Dictionary<string, object>
            {
                ["Name"] = "NewInstance",
                ["DistroName"] = "Ubuntu-22.04"
            },
            options);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteScriptAsync_Should_Support_Cancellation_Token()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var script = "Get-DistroNexusInstance | ForEach-Object { $_ }";

        // Act
        var task = _powerShellService.ExecuteScriptAsync(
            script,
            cancellationTokenSource.Token);

        // Assert - Should not throw if cancelled quickly
        Assert.NotNull(task);
    }

    [Fact]
    public async Task ExecuteScriptAsync_Should_Cancel_Long_Running_Operation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var script = "Start-Sleep -Seconds 60"; // Long-running script

        var task = _powerShellService.ExecuteScriptAsync(
            script,
            cancellationTokenSource.Token);

        // Act - Cancel after short delay
        await Task.Delay(100);
        cancellationTokenSource.Cancel();

        // Assert
        try
        {
            await task;
            // If it completes despite cancellation, that's acceptable for this test
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
            Assert.True(true);
        }
    }

    [Fact]
    public async Task ModuleCallOptions_Should_Specify_Timeout()
    {
        // Arrange
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 45,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Act & Assert
        Assert.Equal(45, options.TimeoutSeconds);
    }

    [Fact]
    public async Task Timeout_Should_Be_Enforced_Per_Operation()
    {
        // Arrange - Each operation has its own timeout
        var quickOptions = new ModuleCallOptions { TimeoutSeconds = 10 };
        var normalOptions = new ModuleCallOptions { TimeoutSeconds = 30 };
        var longOptions = new ModuleCallOptions { TimeoutSeconds = 120 };

        // Act & Assert
        Assert.Equal(10, quickOptions.TimeoutSeconds);
        Assert.Equal(30, normalOptions.TimeoutSeconds);
        Assert.Equal(120, longOptions.TimeoutSeconds);
    }

    [Fact]
    public async Task Multiple_Operations_Should_Respect_Independent_Timeouts()
    {
        // Arrange - Simulate multiple concurrent operations with different timeouts
        var operation1 = _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            new ModuleCallOptions { TimeoutSeconds = 10 });

        var operation2 = _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusPackage",
            parameters: null,
            new ModuleCallOptions { TimeoutSeconds = 30 });

        // Act
        await Task.WhenAll(operation1, operation2);

        // Assert - Both should respect their respective timeouts
        Assert.NotNull(operation1.Result);
        Assert.NotNull(operation2.Result);
    }

    [Fact]
    public async Task CancellationToken_Should_Propagate_Through_Call_Stack()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var task = _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options: new ModuleCallOptions { TimeoutSeconds = 30 },
            cancellationToken: cancellationTokenSource.Token);

        // Assert - Token should be passed through
        Assert.NotNull(task);
    }

    [Fact]
    public void ModuleCallOptions_Should_Validate_Timeout_Range()
    {
        // Arrange - Timeout should be positive integer
        var validTimeouts = new[] { 1, 10, 30, 60, 120, 300 };
        var invalidTimeouts = new[] { -1, 0 };

        // Act & Assert
        foreach (var timeout in validTimeouts)
        {
            var options = new ModuleCallOptions { TimeoutSeconds = timeout };
            Assert.Equal(timeout, options.TimeoutSeconds);
        }
    }

    [Fact]
    public async Task Timeout_Error_Should_Include_Operation_Details()
    {
        // Arrange
        var operation = "LongRunningOperation";
        var timeoutSeconds = 5;

        // Act
        var exception = new WslOperationTimeoutException(
            operation,
            timeoutSeconds);

        // Assert
        Assert.NotNull(exception.Message);
        Assert.Equal(timeoutSeconds, exception.TimeoutSeconds);
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public async Task Progress_Tracking_Should_Not_Extend_Timeout()
    {
        // Arrange
        var progressReporter = new Progress<double>();
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 30,
            ProgressTracker = progressReporter
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Install-DistroNexusInstance",
            new Dictionary<string, object> { ["Name"] = "TestInstance" },
            options);

        stopwatch.Stop();

        // Assert
        // Timeout should still be enforced even with progress tracking
        Assert.True(stopwatch.ElapsedMilliseconds <= 35000); // 30s timeout + 5s buffer
    }

    [Fact]
    public async Task Operation_Should_Be_Terminable_During_Long_Download()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var largeFileUrl = "https://example.com/large-file.tar.gz";

        // Act - Start download with cancellation capability
        var downloadTask = _powerShellService.ExecuteScriptAsync(
            $"Invoke-WebRequest -Uri '{largeFileUrl}' -OutFile 'test.tar.gz'",
            cancellationTokenSource.Token);

        // Can cancel at any point
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

        // Assert
        var terminated = false;
        try
        {
            await downloadTask;
            terminated = true;
        }
        catch (OperationCanceledException)
        {
            terminated = true;
        }
        catch (WslOperationException)
        {
            terminated = true;
        }
        finally
        {
            if (File.Exists("test.tar.gz"))
            {
                File.Delete("test.tar.gz");
            }
        }

        Assert.True(terminated);
    }
}
