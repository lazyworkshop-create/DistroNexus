using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;

namespace DistroNexus.Tests.ViewModels;

public class ReviewStepTests
{
    [Fact]
    public async Task OnEnterAsync_WithTemplateEnabled_ExposesTemplateSummary()
    {
        var step = new ReviewStep
        {
            Context = new WizardContext
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
        var step = new ReviewStep
        {
            Context = new WizardContext
            {
                ApplyTemplateAfterInstall = false,
                SelectedTemplate = null
            }
        };

        await step.OnEnterAsync();

        Assert.False(step.IsTemplateEnabled);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.LabelNoTemplateSelected, step.TemplateNameDisplay);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.LabelNoTemplateSelected, step.TemplateCategoryDisplay);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.LabelNoTemplateSelected, step.TemplateDescriptorDisplay);
    }
}
