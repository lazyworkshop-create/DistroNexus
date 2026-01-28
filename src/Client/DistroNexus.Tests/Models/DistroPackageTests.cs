using DistroNexus.Core.Models;

namespace DistroNexus.Tests.Models;

public class DistroPackageTests
{
    [Fact]
    public void DistroPackage_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var package = new DistroPackage();

        // Assert
        Assert.Equal(string.Empty, package.Id);
        Assert.Equal(string.Empty, package.Name);
        Assert.Equal(string.Empty, package.Description);
        Assert.False(package.IsCached);
    }

    [Fact]
    public void DistroPackage_SetProperties_WorkCorrectly()
    {
        // Arrange
        var package = new DistroPackage
        {
            Id = "ubuntu-22.04",
            Name = "Ubuntu 22.04 LTS",
            Description = "Ubuntu Linux distribution",
            Version = "22.04",
            Category = "Ubuntu",
            DownloadUrl = "https://example.com/ubuntu.tar.gz",
            FileSize = 500 * 1024 * 1024, // 500MB
            IsCached = true
        };

        // Assert
        Assert.Equal("ubuntu-22.04", package.Id);
        Assert.Equal("Ubuntu 22.04 LTS", package.Name);
        Assert.Equal("Ubuntu Linux distribution", package.Description);
        Assert.Equal("22.04", package.Version);
        Assert.Equal("Ubuntu", package.Category);
        Assert.Equal("https://example.com/ubuntu.tar.gz", package.DownloadUrl);
        Assert.Equal(524288000, package.FileSize);
        Assert.True(package.IsCached);
    }

    [Fact]
    public void DistroPackage_MultiplePackages_CanBeCompared()
    {
        // Arrange
        var package1 = new DistroPackage { Id = "ubuntu-22.04", Name = "Ubuntu 22.04" };
        var package2 = new DistroPackage { Id = "ubuntu-22.04", Name = "Ubuntu 22.04" };
        var package3 = new DistroPackage { Id = "debian-12", Name = "Debian 12" };

        // Assert
        Assert.Equal(package1.Id, package2.Id);
        Assert.NotEqual(package1.Id, package3.Id);
    }
}
