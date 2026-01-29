using DistroNexus.Core.Models;
using Xunit;

namespace DistroNexus.Tests.Models;

/// <summary>
/// Unit tests for PowerShellScriptResult.
/// </summary>
public class PowerShellScriptResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var result = new PowerShellScriptResult();

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(string.Empty, result.Error);
        Assert.True(result.Success);
    }

    [Fact]
    public void Success_WithZeroExitCode_ShouldReturnTrue()
    {
        // Arrange
        var result = new PowerShellScriptResult { ExitCode = 0 };

        // Act
        var success = result.Success;

        // Assert
        Assert.True(success);
    }

    [Fact]
    public void Success_WithNonZeroExitCode_ShouldReturnFalse()
    {
        // Arrange
        var result = new PowerShellScriptResult { ExitCode = 1 };

        // Act
        var success = result.Success;

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var result = new PowerShellScriptResult();

        // Act
        result.ExitCode = 2;
        result.Output = "Test output";
        result.Error = "Test error";

        // Assert
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("Test output", result.Output);
        Assert.Equal("Test error", result.Error);
        Assert.False(result.Success);
    }
}