using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;

namespace DistroNexus.Tests.ViewModels;

public class TemplateOptionsStepTests
{
    [Fact]
    public void ShouldSkip_ReturnsTrue_WhenNoAdvancedOptions()
    {
        var step = new TemplateOptionsStep();
        var context = new WizardContext
        {
            ApplyTemplateAfterInstall = true,
            SelectedTemplate = new Template
            {
                Id = "template-a",
                Name = "Template A"
            }
        };

        var skip = step.ShouldSkip(context);

        Assert.True(skip);
    }

    [Fact]
    public async Task Validate_Fails_WhenRequiredOptionIsMissing()
    {
        var step = new TemplateOptionsStep
        {
            Context = new WizardContext
            {
                ApplyTemplateAfterInstall = true,
                SelectedTemplate = new Template
                {
                    Id = "template-a",
                    Name = "Template A",
                    VersionOptions =
                    [
                        new TemplateVersionOption
                        {
                            Key = "dotnet_version",
                            Label = "Dotnet Version",
                            Required = true,
                            Options =
                            [
                                new TemplateOptionValue { Value = "", Label = "Select" },
                                new TemplateOptionValue { Value = "8.0", Label = "8.0" }
                            ]
                        }
                    ]
                }
            }
        };

        await step.OnEnterAsync();
        step.VersionSelections[0].SelectedValue = string.Empty;

        var isValid = step.Validate();

        Assert.False(isValid);
        Assert.False(string.IsNullOrWhiteSpace(step.ErrorMessage));
    }

    [Fact]
    public async Task OnExitAsync_PersistsSelectedVariables()
    {
        var step = new TemplateOptionsStep
        {
            Context = new WizardContext
            {
                ApplyTemplateAfterInstall = true,
                SelectedTemplate = new Template
                {
                    Id = "template-a",
                    Name = "Template A",
                    VersionOptions =
                    [
                        new TemplateVersionOption
                        {
                            Key = "dotnet_version",
                            Label = "Dotnet Version",
                            DefaultValue = "8.0",
                            Options =
                            [
                                new TemplateOptionValue { Value = "8.0", Label = "8.0" },
                                new TemplateOptionValue { Value = "9.0", Label = "9.0" }
                            ]
                        }
                    ]
                }
            }
        };

        await step.OnEnterAsync();
        step.VersionSelections[0].SelectedValue = "9.0";
        await step.OnExitAsync();

        Assert.True(step.Context.TemplateVariableSelections.ContainsKey("dotnet_version"));
        Assert.Equal("9.0", step.Context.TemplateVariableSelections["dotnet_version"]);
    }
}
