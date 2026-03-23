using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.ObjectModel;

namespace DistroNexus.ViewModelTests.Helpers;

/// <summary>
/// Factory helpers to create ViewModels with mocked dependencies for unit tests.
/// </summary>
internal static class TestViewModelFactory
{
    /// <summary>
    /// Creates a <see cref="WslInstance"/> model with sensible defaults for tests.
    /// </summary>
    public static WslInstance CreateInstance(
        string name = "TestDistro",
        int version = 2,
        string state = "Stopped",
        string installPath = @"C:\WSL\TestDistro") =>
        new()
        {
            Name = name,
            Version = version,
            State = state,
            InstallPath = installPath,
            Distribution = "Ubuntu"
        };

    /// <summary>
    /// Creates a <see cref="WslInstanceViewModel"/> with all dependencies mocked.
    /// The <paramref name="wslManager"/> and <paramref name="dialogService"/> mocks are shared
    /// so callers can set them up individually.
    /// </summary>
    public static WslInstanceViewModel CreateWslInstanceViewModel(
        WslInstance? instance = null,
        Mock<IWslManagerService>? wslManager = null,
        Mock<IDialogService>? dialogService = null)
    {
        instance ??= CreateInstance();
        wslManager ??= new Mock<IWslManagerService>();
        dialogService ??= new Mock<IDialogService>();

        var mockTerminal = new Mock<ITerminalService>();
        var mockSettings = new Mock<ISettingsService>();
        var mockLogger = NullLogger.Instance;
        var mockTags = new Mock<ITagService>();
        var mockBackup = new Mock<IBackupService>();

        var mockSp = new Mock<IServiceProvider>();
        mockSp.Setup(x => x.GetService(typeof(IDialogService))).Returns(dialogService.Object);

        return new WslInstanceViewModel(
            instance,
            wslManager.Object,
            mockTerminal.Object,
            mockSettings.Object,
            mockLogger,
            mockTags.Object,
            mockBackup.Object,
            mockSp.Object);
    }

    /// <summary>
    /// Creates a configured <see cref="IServiceProvider"/> mock that resolves
    /// <typeparamref name="IWslManagerService"/> and <typeparamref name="IDialogService"/>.
    /// </summary>
    public static Mock<IServiceProvider> CreateServiceProvider(
        Mock<IWslManagerService>? wslManager = null,
        Mock<IDialogService>? dialogService = null)
    {
        wslManager ??= new Mock<IWslManagerService>();
        dialogService ??= new Mock<IDialogService>();

        var mockSp = new Mock<IServiceProvider>();
        mockSp.Setup(x => x.GetService(typeof(IWslManagerService))).Returns(wslManager.Object);
        mockSp.Setup(x => x.GetService(typeof(IDialogService))).Returns(dialogService.Object);

        return mockSp;
    }

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> with all dependencies mocked.
    /// Returns the mocks so callers can set them up.
    /// </summary>
    public static (MainViewModel Vm,
                   Mock<IWslManagerService> WslManager,
                   Mock<IDialogService> DialogService,
                   Mock<IServiceProvider> ServiceProvider)
        CreateMainViewModel()
    {
        var wslManager = new Mock<IWslManagerService>();
        var dialogService = new Mock<IDialogService>();
        var servicProvider = new Mock<IServiceProvider>();

        servicProvider.Setup(x => x.GetService(typeof(IWslManagerService))).Returns(wslManager.Object);
        servicProvider.Setup(x => x.GetService(typeof(IDialogService))).Returns(dialogService.Object);

        var mockDtm = new Mock<IDownloadTaskManager>();
        mockDtm.Setup(x => x.Tasks).Returns(new ObservableCollection<DownloadTask>());
        // Allow event subscription without special setup (Moq handles this automatically)

        var mockWatcher = new Mock<IWslEventWatcher>();

        var vm = new MainViewModel(
            wslManager.Object,
            new Mock<ISettingsService>().Object,
            new Mock<INavigationService>().Object,
            new Mock<ITerminalService>().Object,
            mockDtm.Object,
            servicProvider.Object,
            NullLogger<MainViewModel>.Instance,
            mockWatcher.Object,
            new Mock<ITagService>().Object,
            new Mock<IBackupService>().Object,
            new Mock<IDockerIntegrationService>().Object,
            dialogService.Object);

        return (vm, wslManager, dialogService, servicProvider);
    }
}
