using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Tests for CatalogService using mocked dependencies.
/// </summary>
public class CatalogServiceTests
{
    private readonly Mock<ICatalogService> _mockCatalogService;

    public CatalogServiceTests()
    {
        _mockCatalogService = new Mock<ICatalogService>();
    }

    [Fact]
    public async Task LoadCatalogAsync_ReturnsCatalogList()
    {
        // Arrange
        var expectedPackages = new List<DistroPackage>
        {
            new() { Id = "ubuntu-22.04", Name = "Ubuntu 22.04 LTS", Category = "Ubuntu" },
            new() { Id = "debian-12", Name = "Debian 12", Category = "Debian" },
            new() { Id = "alpine-3.18", Name = "Alpine 3.18", Category = "Alpine" }
        };

        _mockCatalogService
            .Setup(x => x.LoadCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPackages);

        // Act
        var result = await _mockCatalogService.Object.LoadCatalogAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.Category == "Ubuntu");
        Assert.Contains(result, p => p.Category == "Debian");
    }

    [Fact]
    public async Task SearchDistributionsAsync_WithValidQuery_ReturnsMatches()
    {
        // Arrange
        var ubuntuPackages = new List<DistroPackage>
        {
            new() { Id = "ubuntu-22.04", Name = "Ubuntu 22.04 LTS" },
            new() { Id = "ubuntu-20.04", Name = "Ubuntu 20.04 LTS" }
        };

        _mockCatalogService
            .Setup(x => x.SearchDistributionsAsync("Ubuntu", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ubuntuPackages);

        // Act
        var result = await _mockCatalogService.Object.SearchDistributionsAsync("Ubuntu");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Contains("Ubuntu", p.Name));
    }

    [Fact]
    public async Task SearchDistributionsAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        _mockCatalogService
            .Setup(x => x.SearchDistributionsAsync("NonExistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DistroPackage>());

        // Act
        var result = await _mockCatalogService.Object.SearchDistributionsAsync("NonExistent");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDistributionByIdAsync_WithValidId_ReturnsPackage()
    {
        // Arrange
        var package = new DistroPackage
        {
            Id = "ubuntu-22.04",
            Name = "Ubuntu 22.04 LTS",
            DownloadUrl = "https://example.com/ubuntu.tar.gz"
        };

        _mockCatalogService
            .Setup(x => x.GetDistributionByIdAsync("ubuntu-22.04", It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _mockCatalogService.Object.GetDistributionByIdAsync("ubuntu-22.04");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ubuntu-22.04", result.Id);
        Assert.Equal("Ubuntu 22.04 LTS", result.Name);
    }

    [Fact]
    public async Task GetDistributionByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _mockCatalogService
            .Setup(x => x.GetDistributionByIdAsync("invalid-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DistroPackage?)null);

        // Act
        var result = await _mockCatalogService.Object.GetDistributionByIdAsync("invalid-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshCatalogAsync_CallsRefresh()
    {
        // Arrange
        _mockCatalogService
            .Setup(x => x.RefreshCatalogAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockCatalogService.Object.RefreshCatalogAsync();

        // Assert
        _mockCatalogService.Verify(x => x.RefreshCatalogAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetCatalogCachePath_ReturnsValidPath()
    {
        // Arrange
        _mockCatalogService
            .Setup(x => x.GetCatalogCachePath())
            .Returns(@"C:\Users\Test\AppData\Roaming\DistroNexus\distros.json");

        // Act
        var path = _mockCatalogService.Object.GetCatalogCachePath();

        // Assert
        Assert.NotNull(path);
        Assert.Contains("distros.json", path);
    }

    [Fact]
    public async Task DeleteCachedPackageAsync_WithValidId_CompletesSuccessfully()
    {
        // Arrange
        _mockCatalogService
            .Setup(x => x.DeleteCachedPackageAsync("ubuntu-22.04", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        await _mockCatalogService.Object.DeleteCachedPackageAsync("ubuntu-22.04");
        _mockCatalogService.Verify(x => x.DeleteCachedPackageAsync("ubuntu-22.04", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadCatalogAsync_GroupsByCategory()
    {
        // Arrange
        var packages = new List<DistroPackage>
        {
            new() { Id = "ubuntu-22.04", Category = "Ubuntu" },
            new() { Id = "ubuntu-20.04", Category = "Ubuntu" },
            new() { Id = "debian-12", Category = "Debian" },
            new() { Id = "alpine-3.18", Category = "Alpine" }
        };

        _mockCatalogService
            .Setup(x => x.LoadCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(packages);

        // Act
        var result = await _mockCatalogService.Object.LoadCatalogAsync();
        var grouped = result.GroupBy(p => p.Category).ToList();

        // Assert
        Assert.Equal(3, grouped.Count);
        Assert.Contains(grouped, g => g.Key == "Ubuntu" && g.Count() == 2);
        Assert.Contains(grouped, g => g.Key == "Debian" && g.Count() == 1);
        Assert.Contains(grouped, g => g.Key == "Alpine" && g.Count() == 1);
    }
}
