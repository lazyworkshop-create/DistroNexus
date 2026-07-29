using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class TemplateOptionsStepTests
{
    [Fact]
    public async Task OnEnterAsync_UsesBoundedOptionsFromModuleClient()
    {
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.GetTemplateOptionsAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TemplateOptionDisplay("runtime", "Runtime", "", TemplateOptionType.Select, true, "20", [new TemplateOptionValueDisplay("20", "20", "")])]);
        var step = new TemplateOptionsStep(client.Object) { Context = new WizardContext { ApplyTemplateAfterInstall = true, SelectedTemplate = new Template { Id = "t1" } } };

        await step.OnEnterAsync();

        Assert.Single(step.VersionSelections);
        Assert.Equal("20", step.VersionSelections[0].SelectedValue);
        Assert.True(step.Validate());
    }
}
