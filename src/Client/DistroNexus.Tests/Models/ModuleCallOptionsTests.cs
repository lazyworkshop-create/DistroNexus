using DistroNexus.Core.Models;
using FluentAssertions;
using Xunit;

namespace DistroNexus.Tests.Models;

public class ModuleCallOptionsTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var options = new ModuleCallOptions();

        // Assert
        options.Should().NotBeNull();
        options.TimeoutSeconds.Should().Be(300);
        options.ParseAsJson.Should().BeTrue();
        options.ForceRefresh.Should().BeFalse();
        options.UseModuleFallback.Should().BeFalse();
        options.LogVerbose.Should().BeFalse();
    }

    [Fact]
    public void TimeoutSeconds_ShouldBeSettable()
    {
        // Arrange
        var options = new ModuleCallOptions();

        // Act
        options.TimeoutSeconds = 60;

        // Assert
        options.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void ParseAsJson_ShouldBeSettable()
    {
        // Arrange
        var options = new ModuleCallOptions();

        // Act
        options.ParseAsJson = true;

        // Assert
        options.ParseAsJson.Should().BeTrue();
    }

    [Fact]
    public void ForceRefresh_ShouldBeSettable()
    {
        // Arrange
        var options = new ModuleCallOptions();

        // Act
        options.ForceRefresh = true;

        // Assert
        options.ForceRefresh.Should().BeTrue();
    }

    [Fact]
    public void UseModuleFallback_ShouldBeSettable()
    {
        // Arrange
        var options = new ModuleCallOptions();

        // Act
        options.UseModuleFallback = false;

        // Assert
        options.UseModuleFallback.Should().BeFalse();
    }

    [Fact]
    public void LogVerbose_ShouldBeSettable()
    {
        // Arrange
        var options = new ModuleCallOptions();

        // Act
        options.LogVerbose = true;

        // Assert
        options.LogVerbose.Should().BeTrue();
    }

    [Fact]
    public void ObjectInitializer_ShouldWorkCorrectly()
    {
        // Act
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 45,
            ParseAsJson = true,
            ForceRefresh = true,
            UseModuleFallback = false,
            LogVerbose = true
        };

        // Assert
        options.TimeoutSeconds.Should().Be(45);
        options.ParseAsJson.Should().BeTrue();
        options.ForceRefresh.Should().BeTrue();
        options.UseModuleFallback.Should().BeFalse();
        options.LogVerbose.Should().BeTrue();
    }
}
