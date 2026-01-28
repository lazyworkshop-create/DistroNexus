using DistroNexus.Core.Models;

namespace DistroNexus.Tests.Models;

public class WslInstanceTests
{
    [Fact]
    public void WslInstance_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var instance = new WslInstance();

        // Assert
        Assert.Equal(string.Empty, instance.Name);
        Assert.Equal(string.Empty, instance.State);
        Assert.Equal(0, instance.Version);
        Assert.Equal(string.Empty, instance.InstallPath);
        Assert.False(instance.IsDefault);
        Assert.Equal(0, instance.Size);
    }

    [Fact]
    public void WslInstance_SetProperties_WorkCorrectly()
    {
        // Arrange
        var instance = new WslInstance
        {
            Name = "Ubuntu-22.04",
            State = "Running",
            Version = 2,
            InstallPath = @"C:\WSL\Ubuntu",
            IsDefault = true,
            Size = 1024 * 1024 * 500, // 500MB
            Distribution = "Ubuntu"
        };

        // Assert
        Assert.Equal("Ubuntu-22.04", instance.Name);
        Assert.Equal("Running", instance.State);
        Assert.Equal(2, instance.Version);
        Assert.Equal(@"C:\WSL\Ubuntu", instance.InstallPath);
        Assert.True(instance.IsDefault);
        Assert.Equal(524288000, instance.Size);
        Assert.Equal("Ubuntu", instance.Distribution);
    }

    [Theory]
    [InlineData("Running", true)]
    [InlineData("Stopped", false)]
    [InlineData("Installing", false)]
    [InlineData("", false)]
    public void WslInstance_IsRunning_ReturnsCorrectValue(string state, bool expectedIsRunning)
    {
        // Arrange
        var instance = new WslInstance { State = state };

        // Act
        var isRunning = instance.State == "Running";

        // Assert
        Assert.Equal(expectedIsRunning, isRunning);
    }
}
