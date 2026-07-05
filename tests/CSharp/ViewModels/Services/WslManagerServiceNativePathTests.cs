using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DistroNexus.ViewModelTests.Services;

/// <summary>
/// Unit tests for the native wsl.exe CLI path in <see cref="WslManagerService"/>
/// (E-08 Phase 1 and Phase 2).
/// All tests use a mocked <see cref="IWslCliRunner"/>; no real processes or registry
/// access takes place.
/// </summary>
public sealed class WslManagerServiceNativePathTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static WslManagerService CreateSut(IWslCliRunner? runner)
    {
        var mockPs = new Mock<IPowerShellService>();
        var mockCatalog = new Mock<ICatalogService>();
        return new WslManagerService(
            mockPs.Object,
            mockCatalog.Object,
            NullLogger<WslManagerService>.Instance,
            runner);
    }

    /// <summary>
    /// Builds the text that wsl --list --verbose --all would emit (Unicode text, one header + rows).
    /// </summary>
    private static string BuildVerboseOutput(params (bool isDefault, string name, string state, int version)[] rows)
    {
        var lines = new List<string>
        {
            "  NAME                   STATE           VERSION"
        };
        foreach (var (isDefault, name, state, version) in rows)
        {
            var prefix = isDefault ? "* " : "  ";
            lines.Add($"{prefix}{name,-22} {state,-15} {version}");
        }
        return string.Join("\r\n", lines) + "\r\n";
    }

    // ── GetInstancesAsync — native path selection ─────────────────────────

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_BypassesPowerShellModule()
    {
        // When IWslCliRunner is provided, GetInstancesAsync must NOT call the PS module.
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { ExitCode = 0, Output = BuildVerboseOutput(
                (true, "Ubuntu", "Stopped", 2)) });

        var mockPs = new Mock<IPowerShellService>();
        var mockCatalog = new Mock<ICatalogService>();
        var sut = new WslManagerService(
            mockPs.Object, mockCatalog.Object,
            NullLogger<WslManagerService>.Instance,
            mockRunner.Object);

        await sut.GetInstancesAsync();

        mockPs.Verify(
            p => p.ExecuteModuleCmdletAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "PS module must not be called when IWslCliRunner is injected");
    }

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_ParsesInstanceNames()
    {
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync("--list --verbose --all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = BuildVerboseOutput(
                    (true,  "Ubuntu-22.04", "Stopped", 2),
                    (false, "Debian",       "Stopped", 2),
                    (false, "Alpine",       "Stopped", 1))
            });
        mockRunner
            .Setup(r => r.RunAsync("--list --running", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { ExitCode = 0, Output = string.Empty });

        var sut = CreateSut(mockRunner.Object);
        var instances = await sut.GetInstancesAsync();

        instances.Should().HaveCount(3);
        instances.Select(i => i.Name).Should().BeEquivalentTo(
            new[] { "Ubuntu-22.04", "Debian", "Alpine" });
    }

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_SetsDefaultFlag()
    {
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync("--list --verbose --all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = BuildVerboseOutput(
                    (true,  "Ubuntu", "Stopped", 2),
                    (false, "Debian", "Stopped", 2))
            });
        mockRunner
            .Setup(r => r.RunAsync("--list --running", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { ExitCode = 0, Output = string.Empty });

        var sut = CreateSut(mockRunner.Object);
        var instances = await sut.GetInstancesAsync();

        instances.First(i => i.Name == "Ubuntu").IsDefault.Should().BeTrue();
        instances.First(i => i.Name == "Debian").IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_RunningStateOverride()
    {
        // wsl --list --running reports Ubuntu as running; state in verbose output may say "Running"
        // The native path must mark it Running and Debian as Stopped.
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync("--list --verbose --all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = BuildVerboseOutput(
                    (true,  "Ubuntu", "Running", 2),
                    (false, "Debian", "Stopped", 2))
            });
        mockRunner
            .Setup(r => r.RunAsync("--list --running", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = "  NAME\r\n  Ubuntu\r\n"
            });

        var sut = CreateSut(mockRunner.Object);
        var instances = await sut.GetInstancesAsync();

        instances.First(i => i.Name == "Ubuntu").State.Should().Be("Running");
        instances.First(i => i.Name == "Debian").State.Should().Be("Stopped");
    }

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_EmptyOutput_ReturnsEmptyList()
    {
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { ExitCode = 0, Output = string.Empty });

        var sut = CreateSut(mockRunner.Object);
        var instances = await sut.GetInstancesAsync();

        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_NonZeroExitCode_ReturnsEmptyList()
    {
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync("--list --verbose --all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 1,
                Error    = "WSL is not installed."
            });

        var sut = CreateSut(mockRunner.Object);
        var instances = await sut.GetInstancesAsync();

        instances.Should().BeEmpty("non-zero exit code should yield empty list without throwing");
    }

    [Fact]
    public async Task GetInstancesAsync_WithCliRunner_ParsesWslVersion1()
    {
        var mockRunner = new Mock<IWslCliRunner>();
        mockRunner
            .Setup(r => r.RunAsync("--list --verbose --all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult
            {
                ExitCode = 0,
                Output   = BuildVerboseOutput((false, "LegacyDistro", "Stopped", 1))
            });
        mockRunner
            .Setup(r => r.RunAsync("--list --running", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WslCliResult { ExitCode = 0, Output = string.Empty });

        var sut = CreateSut(mockRunner.Object);
        var instances = await sut.GetInstancesAsync();

        instances.Should().ContainSingle();
        instances[0].Version.Should().Be(1);
    }

    // ── WslCliRunner static parse helpers ─────────────────────────────────

    [Fact]
    public void ParseWslListVerbose_TypicalOutput_ReturnsAllDescriptors()
    {
        var output = BuildVerboseOutput(
            (true,  "Ubuntu",  "Running", 2),
            (false, "Debian",  "Stopped", 2),
            (false, "Alpine",  "Stopped", 1));

        var result = WslCliRunner.ParseWslListVerbose(output);

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Ubuntu");
        result[0].IsDefault.Should().BeTrue();
        result[0].State.Should().Be("Running");
        result[0].Version.Should().Be(2);
        result[2].Version.Should().Be(1);
    }

    [Fact]
    public void ParseWslListVerbose_NullOrEmpty_ReturnsEmpty()
    {
        WslCliRunner.ParseWslListVerbose(null).Should().BeEmpty();
        WslCliRunner.ParseWslListVerbose(string.Empty).Should().BeEmpty();
        WslCliRunner.ParseWslListVerbose("  NAME  STATE  VERSION  ").Should().BeEmpty();
    }

    [Fact]
    public void ParseWslListRunning_NamesExtracted()
    {
        var output = "  NAME\r\n  Ubuntu\r\n  Debian\r\n";
        var result = WslCliRunner.ParseWslListRunning(output);
        result.Should().Contain("Ubuntu").And.Contain("Debian");
    }

    [Fact]
    public void ParseWslListRunning_CaseInsensitiveContains()
    {
        var output = "ubuntu\r\n";
        var result = WslCliRunner.ParseWslListRunning(output);
        result.Contains("UBUNTU").Should().BeTrue("HashSet must be case-insensitive");
    }
}
