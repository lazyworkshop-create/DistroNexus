using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public class SelectTemplateStepTests
{
    private readonly Mock<ITemplateService> _mockTemplateService;
    private readonly Mock<ILogger<SelectTemplateStep>> _mockLogger;

    public SelectTemplateStepTests()
    {
        _mockTemplateService = new Mock<ITemplateService>();
        _mockLogger = new Mock<ILogger<SelectTemplateStep>>();
    }

    [Fact]
    public async Task OnEnterAsync_LoadsTemplates()
    {
        // Arrange
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        step.Context = new WizardContext
        {
            SelectedDistribution = new DistroPackage { Name = "Ubuntu" }
        };
        var expectedTemplates = new List<Template>
        {
            new() { Id = "t1", Name = "Template 1" }
        };

        _mockTemplateService
            .Setup(x => x.LoadTemplatesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplates);

        // Act
        await step.OnEnterAsync();

        // Assert
        Assert.Single(step.Templates);
        Assert.Equal("t1", step.Templates[0].Id);
    }

    [Fact]
    public void SelectingTemplate_UpdatesContext()
    {
        // Arrange
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        var context = new WizardContext();
        step.Context = context;
        
        var template = new Template { Id = "t1", Name = "Template 1" };

        // Act
        step.SelectedTemplate = template;

        // Assert
        Assert.NotNull(context.SelectedTemplate);
        Assert.Equal("t1", context.SelectedTemplate.Id);
    }

    [Fact]
    public async Task OnEnterAsync_FiltersIncompatibleTemplates()
    {
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        step.Context = new WizardContext
        {
            SelectedDistribution = new DistroPackage { Name = "Ubuntu-24.04" }
        };

        var expectedTemplates = new List<Template>
        {
            new() { Id = "ubuntu", Name = "Ubuntu Template", CompatibleDistros = ["Ubuntu"] },
            new() { Id = "fedora", Name = "Fedora Template", CompatibleDistros = ["Fedora"] }
        };

        _mockTemplateService
            .Setup(x => x.LoadTemplatesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplates);

        await step.OnEnterAsync();

        Assert.Single(step.Templates);
        Assert.Equal("ubuntu", step.Templates[0].Id);
    }

    [Fact]
    public void Validate_RequiresSelectionWhenUseTemplateEnabled()
    {
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        step.Context = new WizardContext
        {
            ApplyTemplateAfterInstall = true,
            SelectedDistribution = new DistroPackage { Name = "Ubuntu" }
        };

        step.UseTemplate = true;

        var isValid = step.Validate();

        Assert.False(isValid);
        Assert.False(string.IsNullOrWhiteSpace(step.ErrorMessage));
    }

    [Fact]
    public void Validate_SucceedsWhenUseTemplateDisabled()
    {
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        step.Context = new WizardContext
        {
            ApplyTemplateAfterInstall = true,
            SelectedDistribution = new DistroPackage { Name = "Ubuntu" }
        };

        step.UseTemplate = false;

        var isValid = step.Validate();

        Assert.True(isValid);
        Assert.False(step.Context.ApplyTemplateAfterInstall);
        Assert.Null(step.Context.SelectedTemplate);
    }

    [Fact]
    public void Validate_DoesNotAssignTemplateVariablesInSelectionStep()
    {
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        step.Context = new WizardContext
        {
            ApplyTemplateAfterInstall = true,
            SelectedDistribution = new DistroPackage { Name = "Ubuntu" },
            TemplateVariableSelections = new Dictionary<string, string>
            {
                ["node_version"] = "20"
            }
        };

        step.SelectedTemplate = new Template
        {
            Id = "tmpl",
            Name = "Template"
        };

        var isValid = step.Validate();

        Assert.True(isValid);
        Assert.True(step.Context.TemplateVariableSelections.ContainsKey("node_version"));
        Assert.Equal("20", step.Context.TemplateVariableSelections["node_version"]);
    }

    [Fact]
    public async Task OnEnterAsync_FiltersByScenarioTag()
    {
        var step = new SelectTemplateStep(_mockTemplateService.Object, _mockLogger.Object);
        step.Context = new WizardContext
        {
            SelectedDistribution = new DistroPackage { Name = "Ubuntu" }
        };

        var expectedTemplates = new List<Template>
        {
            new() { Id = "k8s", Name = "Kubernetes", ScenarioTags = ["k8s"] },
            new() { Id = "db", Name = "Database", ScenarioTags = ["database"] }
        };

        _mockTemplateService
            .Setup(x => x.LoadTemplatesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplates);

        await step.OnEnterAsync();
        step.SelectedScenarioTag = "k8s";

        Assert.Single(step.Templates);
        Assert.Equal("k8s", step.Templates[0].Id);
    }
}
