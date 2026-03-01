using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for WslConfigService (E-02).
/// </summary>
public class WslConfigServiceTests
{
    private readonly Mock<ILogger<WslConfigService>> _mockLogger;

    public WslConfigServiceTests()
    {
        _mockLogger = new Mock<ILogger<WslConfigService>>();
    }

    private WslConfigService CreateService(string tempDir)
        => new WslConfigService(_mockLogger.Object, tempDir);

    [Fact]
    public async Task GetWslConfigAsync_WhenFileAbsent_ReturnsEmptyConfig()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var service = CreateService(dir);

            // Act
            var config = await service.GetWslConfigAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Null(config.Memory);
            Assert.Null(config.Processors);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task GetWslConfigAsync_WithValidFile_ParsesValues()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var wslconfigPath = Path.Combine(dir, ".wslconfig");
            await File.WriteAllTextAsync(wslconfigPath, "[wsl2]\nmemory=4GB\nprocessors=2\n");

            var service = CreateService(dir);
            var config = await service.GetWslConfigAsync();

            Assert.Equal("4GB", config.Memory);
            Assert.Equal(2, config.Processors);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SetWslConfigAsync_WritesValuesToFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var service = CreateService(dir);
            var cfg = new WslConfig { Memory = "4GB", Processors = 2 };

            await service.SetWslConfigAsync(cfg);

            var wslconfigPath = Path.Combine(dir, ".wslconfig");
            Assert.True(File.Exists(wslconfigPath));
            var content = await File.ReadAllTextAsync(wslconfigPath);
            Assert.Contains("memory=4GB", content);
            Assert.Contains("processors=2", content);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task SetWslConfigAsync_PreservesUnknownKeys()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var wslconfigPath = Path.Combine(dir, ".wslconfig");
            await File.WriteAllTextAsync(wslconfigPath, "[wsl2]\nmemory=2GB\nprocessors=4\nswap=1GB\n");

            var service = CreateService(dir);
            var cfg = new WslConfig { Memory = "8GB" };  // only change memory
            await service.SetWslConfigAsync(cfg);

            var content = await File.ReadAllTextAsync(wslconfigPath);
            Assert.Contains("memory=8GB", content);
            Assert.Contains("processors=4", content);
            Assert.Contains("swap=1GB", content);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task GetHostSpecsAsync_ReturnsPositiveValues()
    {
        var service = CreateService(Path.GetTempPath());
        var (ram, cpus) = await service.GetHostSpecsAsync();
        Assert.True(ram > 0, "Expected positive RAM MB count");
        Assert.True(cpus > 0, "Expected positive CPU count");
    }
}
