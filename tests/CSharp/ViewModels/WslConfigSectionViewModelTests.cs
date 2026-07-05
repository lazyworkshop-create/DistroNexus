using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="WslConfigSectionViewModel"/>.
/// </summary>
public sealed class WslConfigSectionViewModelTests
{
    private static (Mock<IWslConfigService>, Mock<IWslManagerService>, Mock<IDialogService>) CreateMocks()
    {
        var wslConfig = new Mock<IWslConfigService>();
        var wslManager = new Mock<IWslManagerService>();
        var dialog = new Mock<IDialogService>();

        // sensible defaults
        wslConfig.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync((8192L, 8));
        wslConfig.Setup(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new WslConfig());
        wslConfig.Setup(s => s.SetWslConfigAsync(It.IsAny<WslConfig>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        dialog.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(false);

        return (wslConfig, wslManager, dialog);
    }

    private static WslConfigSectionViewModel CreateSut(
        Mock<IWslConfigService> wslConfig,
        Mock<IWslManagerService> wslManager,
        Mock<IDialogService> dialog)
        => new(wslConfig.Object, wslManager.Object, dialog.Object);

    // ── LoadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesFormFieldsFromService()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        wslConfig.Setup(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new WslConfig
                 {
                     Memory = "4GB",
                     Processors = 4,
                     Swap = "2GB",
                     LocalhostForwarding = false,
                     NetworkingMode = "mirrored"
                 });

        var sut = CreateSut(wslConfig, wslManager, dialog);
        await sut.LoadAsync();

        sut.Memory.Should().Be("4GB");
        sut.Processors.Should().Be("4");
        sut.Swap.Should().Be("2GB");
        sut.LocalhostForwarding.Should().BeFalse();
        sut.NetworkingMode.Should().Be("mirrored");
    }

    [Fact]
    public async Task LoadAsync_SetsHostInfo_WhenServiceReturnsSpecs()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        wslConfig.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync((16384L, 12));

        var sut = CreateSut(wslConfig, wslManager, dialog);
        await sut.LoadAsync();

        sut.HostInfo.Should().Contain("16384").And.Contain("12");
    }

    [Fact]
    public async Task LoadAsync_CalledTwice_OnlyCallsServiceOnce()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        var sut = CreateSut(wslConfig, wslManager, dialog);

        await sut.LoadAsync();
        await sut.LoadAsync();

        wslConfig.Verify(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("invalid")]
    [InlineData("GB")]
    [InlineData("-1GB")]
    public void Memory_InvalidInput_HasMemoryError(string bad)
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        var sut = CreateSut(wslConfig, wslManager, dialog);

        sut.Memory = bad;

        sut.HasMemoryError.Should().BeTrue();
        sut.MemoryError.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("4GB")]
    [InlineData("512MB")]
    [InlineData("4096")]
    [InlineData("")]
    public void Memory_ValidInput_NoMemoryError(string good)
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        var sut = CreateSut(wslConfig, wslManager, dialog);

        sut.Memory = good;

        sut.HasMemoryError.Should().BeFalse();
    }

    [Fact]
    public async Task ShowHighMemoryWarning_WhenOver80Percent_IsTrue()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        wslConfig.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync((8192L, 8)); // 8 GB host

        var sut = CreateSut(wslConfig, wslManager, dialog);
        await sut.LoadAsync();

        sut.Memory = "8GB"; // 8192 MB > 80% of 8192 MB

        sut.ShowHighMemoryWarning.Should().BeTrue();
    }

    [Fact]
    public async Task ShowHighMemoryWarning_WhenUnder80Percent_IsFalse()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        wslConfig.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync((8192L, 8));

        var sut = CreateSut(wslConfig, wslManager, dialog);
        await sut.LoadAsync();

        sut.Memory = "4GB"; // 4096 MB < 80% of 8192 MB

        sut.ShowHighMemoryWarning.Should().BeFalse();
    }

    // ── SaveAndRestart ────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndRestartCommand_CallsSetWslConfigAsync()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        var sut = CreateSut(wslConfig, wslManager, dialog);
        sut.Memory = "4GB";

        await sut.SaveAndRestartCommand.ExecuteAsync(null);

        wslConfig.Verify(
            s => s.SetWslConfigAsync(It.IsAny<WslConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAndRestartCommand_WhenRestartConfirmed_CallsShutdownWsl()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);
        wslManager.Setup(m => m.ShutdownWslAsync(It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var sut = CreateSut(wslConfig, wslManager, dialog);
        await sut.SaveAndRestartCommand.ExecuteAsync(null);

        wslManager.Verify(m => m.ShutdownWslAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAndRestartCommand_WhenRestartDeclined_DoesNotCallShutdownWsl()
    {
        var (wslConfig, wslManager, dialog) = CreateMocks();
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(false);

        var sut = CreateSut(wslConfig, wslManager, dialog);
        await sut.SaveAndRestartCommand.ExecuteAsync(null);

        wslManager.Verify(m => m.ShutdownWslAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
