using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public class ReviewStepTests
{
    private static ReviewStep CreateStep(string displayName = "Module target")
    {
        var client = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        client.Setup(x => x.PreviewInstallTargetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstallTargetPreviewResult(new string('a', 64), DateTimeOffset.UtcNow.AddMinutes(1), displayName, 100, 1, true, "Install.TargetEligible"));
        return new ReviewStep(client.Object);
    }

    [Fact]
    public async Task OnEnterAsync_WithTemplateEnabled_ExposesTemplateSummary()
    {
        var step = CreateStep();
        step.Context = new WizardContext
        {
            ApplyTemplateAfterInstall = true,
            SelectedTemplate = new Template
            {
                    Id = "template-dev",
                    Name = "Dev Template",
                    Category = "dev",
                    Packages = new List<TemplatePackage>
                    {
                        new() { Name = "git" },
                        new() { Name = "curl" }
                    },
                    Scripts = new List<TemplateScript>
                    {
                        new() { Id = "s1", Name = "script-1" }
                    }
            }
        };

        await step.OnEnterAsync();

        Assert.True(step.IsTemplateEnabled);
        Assert.Equal("Dev Template", step.TemplateNameDisplay);
        Assert.Equal("dev", step.TemplateCategoryDisplay);
        Assert.Contains("2", step.TemplateDescriptorDisplay);
        Assert.Contains("1", step.TemplateDescriptorDisplay);
    }

    [Fact]
    public async Task OnEnterAsync_WithTemplateDisabled_ShowsNoTemplateState()
    {
        var step = CreateStep();
        step.Context = new WizardContext
        {
            ApplyTemplateAfterInstall = false,
            SelectedTemplate = null
        };

        await step.OnEnterAsync();

        Assert.False(step.IsTemplateEnabled);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.LabelNoTemplateSelected, step.TemplateNameDisplay);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.LabelNoTemplateSelected, step.TemplateCategoryDisplay);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.LabelNoTemplateSelected, step.TemplateDescriptorDisplay);
    }

    [Fact]
    public async Task OnEnterAsync_RendersModulePreviewDisplayInsteadOfComposingAPath()
    {
        var step = CreateStep("Configured volume");
        step.Context = new WizardContext { InstallPath = "C:\\candidate", InstanceName = "Ubuntu" };

        await step.OnEnterAsync();

        Assert.Equal("Configured volume", step.FullInstallPath);
    }
}
