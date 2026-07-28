using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class SelectTemplateStepTests
{
    [Fact]
    public async Task OnEnterAsync_LoadsSafeTemplatesThroughModuleClient()
    {
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.GetTemplatesAsync(false, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TemplateDisplay("t1", "Template", "", "dev", "1", "author", [], ["Ubuntu"], 1, 1, true, false, TemplateTrustState.Untrusted, [])]);
        var step = new SelectTemplateStep(client.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger>()) { Context = new WizardContext { SelectedDistribution = new DistroPackage { Name = "Ubuntu" } } };

        await step.OnEnterAsync();

        Assert.Single(step.Templates);
        Assert.Equal("t1", step.Templates[0].Id);
        client.Verify(x => x.GetTemplatesAsync(false, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validate_RequiresSelectedTemplateWhenEnabled()
    {
        var step = new SelectTemplateStep(Mock.Of<IPowerShellModuleClient>(), Mock.Of<Microsoft.Extensions.Logging.ILogger>()) { Context = new WizardContext { ApplyTemplateAfterInstall = true } };
        step.UseTemplate = true;
        Assert.False(step.Validate());
    }
}
