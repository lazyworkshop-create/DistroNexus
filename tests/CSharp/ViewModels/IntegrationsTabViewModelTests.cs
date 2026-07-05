using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using DistroNexus.ViewModelTests.Helpers;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="IntegrationsTabViewModel"/> (C-01).
/// </summary>
public sealed class IntegrationsTabViewModelTests
{
    private static (Mock<IDockerIntegrationService>, Mock<IDialogService>) CreateMocks()
    {
        var docker = new Mock<IDockerIntegrationService>();
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
        return (docker, dialog);
    }

    private static IntegrationsTabViewModel CreateSut(
        WslInstanceViewModel instanceVm,
        Mock<IDockerIntegrationService> docker,
        Mock<IDialogService> dialog)
        => new(instanceVm, docker.Object, dialog.Object);

    // ── IsTabVisible ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("docker-desktop")]
    [InlineData("docker-desktop-data")]
    [InlineData("DOCKER-DESKTOP")]
    public void IsTabVisible_ForDockerSystemInstance_IsFalse(string name)
    {
        var (docker, dialog) = CreateMocks();
        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: name);
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);

        sut.IsTabVisible.Should().BeFalse();
    }

    [Fact]
    public void IsTabVisible_ForRegularInstance_IsTrue()
    {
        var (docker, dialog) = CreateMocks();
        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);

        sut.IsTabVisible.Should().BeTrue();
    }

    // ── InitializeAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenDockerInstalled_QueriesStatus()
    {
        var (docker, dialog) = CreateMocks();
        docker.Setup(d => d.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        docker.Setup(d => d.GetIntegrationStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(DockerIntegrationStatus.Enabled);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu", version: 2);
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);
        await sut.InitializeAsync();

        sut.IsDockerInstalled.Should().BeTrue();
        sut.IsDockerEnabled.Should().BeTrue();
        sut.DockerStatus.Should().Be(DockerIntegrationStatus.Enabled);
    }

    [Fact]
    public async Task InitializeAsync_WhenDockerNotInstalled_StatusIsUnavailable()
    {
        var (docker, dialog) = CreateMocks();
        docker.Setup(d => d.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu", version: 2);
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);
        await sut.InitializeAsync();

        sut.IsDockerInstalled.Should().BeFalse();
        sut.DockerStatus.Should().Be(DockerIntegrationStatus.Unavailable);
        docker.Verify(d => d.GetIntegrationStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_ForWslV1Instance_DoesNotQueryStatus()
    {
        var (docker, dialog) = CreateMocks();
        docker.Setup(d => d.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu", version: 1);
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);
        await sut.InitializeAsync();

        sut.ShowWslV1Message.Should().BeTrue();
        docker.Verify(d => d.GetIntegrationStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ToggleDocker ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleDockerCommand_EnablesDockerAndShowsRestartBanner()
    {
        var (docker, dialog) = CreateMocks();
        docker.Setup(d => d.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        docker.Setup(d => d.GetIntegrationStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(DockerIntegrationStatus.Disabled);
        docker.Setup(d => d.SetIntegrationAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu", version: 2);
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);
        await sut.InitializeAsync();
        await sut.ToggleDockerCommand.ExecuteAsync(null);

        docker.Verify(d => d.SetIntegrationAsync("Ubuntu", true, It.IsAny<CancellationToken>()), Times.Once);
        sut.IsDockerEnabled.Should().BeTrue();
        sut.ShowRestartBanner.Should().BeTrue();
    }

    [Fact]
    public void DismissRestartBannerCommand_HidesBanner()
    {
        var (docker, dialog) = CreateMocks();
        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);
        sut.ShowRestartBanner = true;
        sut.DismissRestartBannerCommand.Execute(null);

        sut.ShowRestartBanner.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_OnlyQueriesOnce()
    {
        var (docker, dialog) = CreateMocks();
        docker.Setup(d => d.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        docker.Setup(d => d.GetIntegrationStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(DockerIntegrationStatus.Disabled);

        var wslManager = new Mock<IWslManagerService>();
        var dialogSvc = new Mock<IDialogService>();
        var instance = TestViewModelFactory.CreateInstance(name: "Ubuntu");
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(instance, wslManager, dialogSvc);

        var sut = CreateSut(vm, docker, dialog);
        await sut.InitializeAsync();
        await sut.InitializeAsync();

        docker.Verify(d => d.IsDockerDesktopInstalledAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
