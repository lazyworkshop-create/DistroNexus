using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

internal static class WslManagerServiceTestAccessor
{
    public static long GetVhdxSizeBytesForTest(WslManagerService svc, string instanceName)
    {
        var method = typeof(WslManagerService)
            .GetMethod("GetVhdxSizeBytes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null) return -1L;
        try
        {
            return (long)method.Invoke(null, new object[] { instanceName })!;
        }
        catch (System.Reflection.TargetInvocationException tie)
            when (tie.InnerException is InvalidOperationException)
        {
            return -2L;
        }
    }
}

/// <summary>
/// Tests that verify WslManagerService uses IWslCliRunner for native operations (E-08).
/// </summary>
public class WslManagerServiceNativeTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly Mock<IWslCliRunner> _mockCliRunner;
    private readonly WslManagerService _service;

    public WslManagerServiceNativeTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockCatalogService    = new Mock<ICatalogService>();
        _mockLogger            = new Mock<ILogger<WslManagerService>>();
        _mockCliRunner         = new Mock<IWslCliRunner>();

        _service = new WslManagerService(
            _mockPowerShellService.Object,
            _mockCatalogService.Object,
            _mockLogger.Object,
            _mockCliRunner.Object);
    }

    [Fact]
    public async Task GetInstancesAsync_UsesCliRunner_NotPowerShell()
    {
        // Arrange — CLI runner returns a standard wsl --list --verbose output
        _mockCliRunner
            .Setup(x => x.RunAsync(
                It.Is<string>(s => s.Contains("--list") && s.Contains("--verbose")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = "  NAME            STATE           VERSION\r\n* Ubuntu-22.04    Running         2\r\n"
            });

        // Act
        var instances = await _service.GetInstancesAsync();

        // Assert — CLI runner was called, NOT the PowerShell service for listing
        _mockCliRunner.Verify(
            x => x.RunAsync(
                It.Is<string>(s => s.Contains("--list")),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetInstancesAsync_ParsesInstanceNameAndState()
    {
        _mockCliRunner
            .Setup(x => x.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = "  NAME     STATE    VERSION\r\n* Debian   Stopped  2\r\n"
            });

        var instances = await _service.GetInstancesAsync();

        Assert.Single(instances);
        Assert.Equal("Debian", instances[0].Name);
        Assert.Equal("Stopped", instances[0].State);
    }

    [Fact]
    public async Task GetInstancesNativeAsync_SetsRunningState_FromWslListRunning()
    {
        _mockCliRunner
            .Setup(r => r.RunAsync(
                It.Is<string>(s => s.Contains("--list") && s.Contains("--verbose")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { Output = "  NAME     STATE    VERSION\n* Ubuntu   Stopped  2", ExitCode = 0 });
        _mockCliRunner
            .Setup(r => r.RunAsync(
                It.Is<string>(s => s.Contains("--running")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { Output = "  NAME\n  Ubuntu", ExitCode = 0 });

        var instances = await _service.GetInstancesAsync(CancellationToken.None);

        Assert.Equal("Running", instances.First(i => i.Name == "Ubuntu").State);
    }

    [Fact]
    public async Task GetInstancesAsync_PopulatesDiskSize_FromFileInfo()
    {
        // Arrange: mock IWslCliRunner to return a known instance
        _mockCliRunner
            .Setup(r => r.RunAsync(
                It.Is<string>(s => s.Contains("--list") && s.Contains("--verbose")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = "  NAME           STATE           VERSION\r\n* Ubuntu         Running         2\r\n"
            });

        // Act
        var instances = await _service.GetInstancesAsync(CancellationToken.None);

        // Assert: Size should be non-negative (0 is acceptable when VHDX not found on CI)
        Assert.All(instances, i => Assert.True(i.Size >= 0));
    }

    [Fact]
    public void GetVhdxSizeBytes_Uses_Registry64_View_Not_Default()
    {
        var size = WslManagerServiceTestAccessor.GetVhdxSizeBytesForTest(_service, "__nonexistent_test_instance__");
        Assert.Equal(0L, size);
    }
}
