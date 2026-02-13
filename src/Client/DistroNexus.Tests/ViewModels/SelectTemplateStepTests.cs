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
}
