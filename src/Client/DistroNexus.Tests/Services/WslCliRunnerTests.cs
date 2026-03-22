using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for WslCliRunner (E-08).
/// Validates wsl.exe output parsing without spawning a real process.
/// </summary>
public class WslCliRunnerTests
{
    private readonly Mock<ILogger<WslCliRunner>> _mockLogger;

    public WslCliRunnerTests()
    {
        _mockLogger = new Mock<ILogger<WslCliRunner>>();
    }

    // -----------------------------------------------------------------------
    // ParseWslListVerboseOutput — static parser tests (no process needed)
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseWslListVerboseOutput_ParsesRunningInstance()
    {
        var raw = """
              NAME            STATE           VERSION
            * Ubuntu-22.04    Running         2
              Debian          Stopped         2
            """;

        var results = WslCliRunner.ParseWslListVerbose(raw);

        Assert.Equal(2, results.Count);

        var ubuntu = results.First(r => r.Name == "Ubuntu-22.04");
        Assert.Equal("Running", ubuntu.State);
        Assert.Equal(2, ubuntu.Version);
        Assert.True(ubuntu.IsDefault);

        var debian = results.First(r => r.Name == "Debian");
        Assert.Equal("Stopped", debian.State);
        Assert.False(debian.IsDefault);
    }

    [Fact]
    public void ParseWslListVerboseOutput_WithNoInstances_ReturnsEmpty()
    {
        var raw = "  NAME            STATE           VERSION";
        var results = WslCliRunner.ParseWslListVerbose(raw);
        Assert.Empty(results);
    }

    [Fact]
    public void ParseWslListVerboseOutput_WithNullInput_ReturnsEmpty()
    {
        var results = WslCliRunner.ParseWslListVerbose(null);
        Assert.Empty(results);
    }

    [Fact]
    public void ParseWslListVerboseOutput_WithEmptyInput_ReturnsEmpty()
    {
        var results = WslCliRunner.ParseWslListVerbose(string.Empty);
        Assert.Empty(results);
    }

    [Fact]
    public void ParseWslListVerboseOutput_ParsesVersion1Instance()
    {
        var raw = """
              NAME       STATE     VERSION
              OldDistro  Stopped   1
            """;

        var results = WslCliRunner.ParseWslListVerbose(raw);
        Assert.Single(results);
        Assert.Equal(1, results[0].Version);
        Assert.Equal("OldDistro", results[0].Name);
    }

    // -----------------------------------------------------------------------
    // ParseWslListRunning — running-instances parser
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseWslListRunning_ParsesRunningInstances()
    {
        var raw = """
            Windows Subsystem for Linux Distributions:
            Ubuntu-22.04 (Running)
            docker-desktop (Running)
            """;

        var running = WslCliRunner.ParseWslListRunning(raw);
        Assert.Contains("Ubuntu-22.04", running);
        Assert.Contains("docker-desktop", running);
    }

    [Fact]
    public void ParseWslListRunning_WithNoRunning_ReturnsEmpty()
    {
        var raw = "Windows Subsystem for Linux Distributions:";
        var running = WslCliRunner.ParseWslListRunning(raw);
        Assert.Empty(running);
    }

    // -----------------------------------------------------------------------
    // WslCliRunner construction
    // -----------------------------------------------------------------------

    [Fact]
    public void WslCliRunner_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WslCliRunner(null!));
    }

    [Fact]
    public void WslCliRunner_Implements_IWslCliRunner()
    {
        using var runner = new WslCliRunner(_mockLogger.Object);
        Assert.IsAssignableFrom<DistroNexus.Core.Interfaces.IWslCliRunner>(runner);
    }
}
