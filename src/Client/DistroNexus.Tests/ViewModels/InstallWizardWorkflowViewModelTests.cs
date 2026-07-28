using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public class InstallWizardWorkflowViewModelTests
{
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<IPowerShellModuleClient> _mockModuleClient;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ITemplateService> _mockTemplateService;
    private readonly Mock<ILogger<InstallWizardWorkflowViewModel>> _mockLogger;

    public InstallWizardWorkflowViewModelTests()
    {
        _mockCatalogService = new Mock<ICatalogService>();
        _mockModuleClient = new Mock<IPowerShellModuleClient>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockTemplateService = new Mock<ITemplateService>();
        _mockLogger = new Mock<ILogger<InstallWizardWorkflowViewModel>>();

        _mockSettingsService
            .Setup(service => service.LoadSettings())
            .Returns(new GlobalSettings());

        _mockCatalogService
            .Setup(service => service.LoadCatalogAsync())
            .ReturnsAsync(new List<DistroPackage>());
    }

    [Fact]
    public async Task Initialize_WithValidTemplatePayload_PreseletsTemplateAndApplyFlag()
    {
        var expectedTemplate = new Template
        {
            Id = "template-a",
            Name = "Template A",
            Category = "dotnet"
        };

        _mockTemplateService
            .Setup(service => service.GetTemplateByIdAsync("template-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplate);

        var viewModel = CreateViewModel();
        viewModel.SetStartupRequest(new InstallWizardStartupRequest { TemplateId = "template-a" });

        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.Workflow.Context.SelectedTemplate);
        Assert.Equal("template-a", viewModel.Workflow.Context.SelectedTemplate!.Id);
        Assert.True(viewModel.Workflow.Context.ApplyTemplateAfterInstall);
        Assert.True(string.IsNullOrWhiteSpace(viewModel.Workflow.Context.StartupWarningMessage));
    }

    [Fact]
    public async Task Initialize_WithInvalidTemplatePayload_FallsBackToGenericWithWarning()
    {
        _mockTemplateService
            .Setup(service => service.GetTemplateByIdAsync("missing-template", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var viewModel = CreateViewModel();
        viewModel.SetStartupRequest(new InstallWizardStartupRequest { TemplateId = "missing-template" });

        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workflow.Context.SelectedTemplate);
        Assert.False(viewModel.Workflow.Context.ApplyTemplateAfterInstall);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Workflow.Context.StartupWarningMessage));
    }

    [Fact]
    public async Task Initialize_WithDistributionPayload_ResolvesDistributionAndSkipsSelectionStep()
    {
        var expectedDistro = new DistroPackage
        {
            Id = "ubuntu-24-04",
            Name = "Ubuntu 24.04",
            DownloadUrl = "https://example.com/ubuntu.tar.gz"
        };

        _mockModuleClient
            .Setup(client => client.GetPackagesAsync(null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DistroPackage> { expectedDistro });

        var viewModel = CreateViewModel();
        viewModel.SetStartupRequest(new InstallWizardStartupRequest { SelectedDistributionId = "ubuntu-24-04" });

        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.Workflow.Context.SelectedDistribution);
        Assert.Equal("ubuntu-24-04", viewModel.Workflow.Context.SelectedDistribution!.Id);
        Assert.Equal("install-path", viewModel.Workflow.CurrentStep?.StepId);
    }

    [Fact]
    public async Task Initialize_WithoutTemplatePayload_DefaultsTemplateApplyFlagToFalse()
    {
        var viewModel = CreateViewModel();

        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.False(viewModel.Workflow.Context.ApplyTemplateAfterInstall);
        Assert.Null(viewModel.Workflow.Context.SelectedTemplate);
    }

    [Fact]
    public async Task InstallPathStep_UsesTheModuleClientToChooseAUniqueQuickInstallName()
    {
        _mockSettingsService.Setup(service => service.LoadSettings()).Returns(new GlobalSettings { DefaultInstallPath = "C:\\WSL" });
        _mockModuleClient.Setup(client => client.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WslInstance { Name = "Ubuntu" }, new WslInstance { Name = "Ubuntu-1" }]);
        var step = new InstallPathStep(_mockSettingsService.Object, _mockModuleClient.Object, Mock.Of<ILogger>())
        {
            Context = new WizardContext { SelectedDistribution = new DistroPackage { Name = "Ubuntu" } }
        };

        await step.ApplyQuickInstallDefaultsAsync();

        Assert.Equal("Ubuntu-2", step.Context.InstanceName);
        _mockModuleClient.Verify(client => client.GetInstancesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private InstallWizardWorkflowViewModel CreateViewModel()
    {
        return new InstallWizardWorkflowViewModel(
            _mockModuleClient.Object,
            _mockSettingsService.Object,
            _mockTemplateService.Object,
            _mockLogger.Object);
    }
}
