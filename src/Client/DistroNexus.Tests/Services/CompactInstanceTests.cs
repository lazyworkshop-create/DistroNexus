using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for the CompactInstanceAsync method on WslManagerService.
/// </summary>
public class CompactInstanceTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly WslManagerService _service;

    public CompactInstanceTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockCatalogService    = new Mock<ICatalogService>();
        _mockLogger            = new Mock<ILogger<WslManagerService>>();

        _service = new WslManagerService(
            _mockPowerShellService.Object,
            _mockCatalogService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CompactInstanceAsync_Should_Invoke_Compress_Cmdlet()
    {
        // Arrange
        const string instanceName = "Ubuntu-22.04";

        var successResult = new PowerShellScriptResult
        {
            ExitCode   = 0,
            Error      = string.Empty,
            UsedModule = true,
            Output     = "[{\"Name\":\"Ubuntu-22.04\",\"SizeBefore\":10737418240,\"SizeAfter\":5368709120,\"SpaceSaved\":5368709120}]"
        };

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        // Act
        await _service.CompactInstanceAsync(instanceName);

        // Assert — correct cmdlet was invoked
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("Name") && (string)d["Name"] == instanceName),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompactInstanceAsync_ShouldReport_ProgressPhases()
    {
        // Arrange
        const string instanceName = "Debian";
        var progressReports = new List<(double Percentage, string Message)>();
        var progress = new TestProgress<(double Percentage, string Message)>(p => progressReports.Add(p));

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, UsedModule = true });

        // Act
        await _service.CompactInstanceAsync(instanceName, progress);

        // Assert — at least one progress report was made
        Assert.NotEmpty(progressReports);
    }

    [Fact]
    public async Task CompactInstanceAsync_WhenCmdletFails_ThrowsWslOperationException()
    {
        // Arrange
        const string instanceName = "Ubuntu-22.04";

        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult
            {
                ExitCode   = 1,
                Error      = "Compaction failed: access denied",
                UsedModule = true
            });

        // Act & Assert
        await Assert.ThrowsAnyAsync<WslOperationException>(
            () => _service.CompactInstanceAsync(instanceName));
    }

    [Fact]
    public async Task CompactInstanceAsync_WhenFails_Throws_WslOperationException()
    {
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 1, Error = "vhd locked" });

        await Assert.ThrowsAnyAsync<WslOperationException>(
            () => _service.CompactInstanceAsync("Ubuntu"));
    }

    [Fact]
    public async Task CompactInstanceAsync_WhatIf_PassesFlagToCmdlet()
    {
        // Arrange
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, UsedModule = true });

        // Act
        await _service.CompactInstanceAsync("Ubuntu-22.04", whatIf: true);

        // Assert — WhatIf switch passed
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("WhatIf") && (bool)d["WhatIf"]),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CompactInstanceAsync_WhenNameIsNullOrEmpty_ThrowsArgumentException(string? name)
    {
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.CompactInstanceAsync(name!));
        Assert.Equal(DistroNexusErrorCode.InstanceNotFound, ex.Code);
    }

    private sealed class TestProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }
}
