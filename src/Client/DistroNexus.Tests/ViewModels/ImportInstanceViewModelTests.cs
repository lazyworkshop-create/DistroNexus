using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class ImportInstanceViewModelTests
{
    [Fact]
    public async Task Confirm_ObtainsTypedLifecyclePreviewBeforeClosing()
    {
        var client = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        var preview = new LifecycleOperationPreview(new string('a', 64), LifecyclePathOperation.Import, "Ubuntu", DateTimeOffset.UtcNow.AddMinutes(1));
        client.Setup(x => x.PreviewImportInstanceAsync("Ubuntu", "C:\\picked.tar", "C:\\instances", It.IsAny<CancellationToken>())).ReturnsAsync(preview);
        var viewModel = new ImportInstanceViewModel([], client.Object)
        {
            InstanceName = "Ubuntu",
            SourcePath = "C:\\picked.tar",
            InstallPath = "C:\\instances"
        };
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(viewModel.Confirmed);
        Assert.True(closeRequested);
        Assert.Equal(preview, viewModel.ImportPreview);
        client.VerifyAll();
    }

    [Fact]
    public async Task Confirm_WhenPreviewRejectsSource_DoesNotCloseOrIssueAGrant()
    {
        var client = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        client.Setup(x => x.PreviewImportInstanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Import source is unavailable."));
        var viewModel = new ImportInstanceViewModel([], client.Object)
        {
            InstanceName = "Ubuntu",
            SourcePath = "C:\\missing.tar",
            InstallPath = "C:\\instances"
        };
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.False(viewModel.Confirmed);
        Assert.False(closeRequested);
        Assert.Null(viewModel.ImportPreview);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.Import_SourceNotFound, viewModel.SourcePathError);
    }

    [Fact]
    public async Task Confirm_WhenPreviewFailureContainsAPath_RendersOnlyLocalizedSourceError()
    {
        const string sensitivePath = "C:\\Users\\example\\private\\missing.tar";
        var client = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        client.Setup(x => x.PreviewImportInstanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"Cannot open {sensitivePath}"));
        var viewModel = new ImportInstanceViewModel([], client.Object)
        {
            InstanceName = "Ubuntu",
            SourcePath = sensitivePath,
            InstallPath = "C:\\instances"
        };

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(DistroNexus.Desktop.Properties.Resources.Import_SourceNotFound, viewModel.SourcePathError);
        Assert.DoesNotContain(sensitivePath, viewModel.SourcePathError, StringComparison.Ordinal);
    }
}
