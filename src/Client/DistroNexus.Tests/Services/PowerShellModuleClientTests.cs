using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class PowerShellModuleClientTests
{
    [Fact]
    public async Task GetInstanceTagsAsync_UsesTheRegisteredCmdletAndDeserializesAnArray()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell
            .Setup(service => service.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstanceTag",
                It.Is<Dictionary<string, object>>(parameters => (string)parameters["Name"] == "Ubuntu"),
                It.Is<ModuleCallOptions>(options => options.ParseAsJson),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult
            {
                ExitCode = 0,
                Output = "[{\"name\":\"Ubuntu\",\"tags\":[\"dev\",\"test\"]}]"
            });
        var client = new PowerShellModuleClient(powerShell.Object);

        var results = await client.GetInstanceTagsAsync("Ubuntu");

        var tag = Assert.Single(results);
        Assert.Equal("Ubuntu", tag.Name);
        Assert.Equal(["dev", "test"], tag.Tags);
    }

    [Fact]
    public async Task GetInstanceTagsAsync_DeserializesASingleModuleObject()
    {
        var powerShell = CreateServiceReturning("{\"Name\":\"Ubuntu\",\"Tags\":[\"dev\"]}");
        var client = new PowerShellModuleClient(powerShell.Object);

        var results = await client.GetInstanceTagsAsync();

        Assert.Equal("Ubuntu", Assert.Single(results).Name);
        powerShell.Verify(service => service.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstanceTag",
            null,
            It.IsAny<ModuleCallOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInstanceTagsAsync_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell
            .Setup(service => service.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstanceTag",
                null,
                It.IsAny<ModuleCallOptions>(),
                cancellation.Token))
            .Returns(Task.FromCanceled<PowerShellScriptResult>(cancellation.Token));
        var client = new PowerShellModuleClient(powerShell.Object);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetInstanceTagsAsync(cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData("Add-DistroNexusInstanceTag", "AddInstanceTagAsync")]
    [InlineData("Set-DistroNexusInstanceTag", "SetInstanceTagsAsync")]
    [InlineData("Remove-DistroNexusInstanceTag", "RemoveInstanceTagAsync")]
    [InlineData("Rename-DistroNexusInstanceTags", "RenameInstanceTagsAsync")]
    public async Task TagMutation_UsesItsFixedRegisteredCmdlet(string command, string method)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell
            .Setup(service => service.ExecuteModuleCmdletAsync(
                command,
                It.IsAny<Dictionary<string, object>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0 });
        var client = new PowerShellModuleClient(powerShell.Object);

        switch (method)
        {
            case "AddInstanceTagAsync":
                await client.AddInstanceTagAsync("Ubuntu", "dev");
                powerShell.Verify(service => service.ExecuteModuleCmdletAsync(command,
                    It.Is<Dictionary<string, object>>(parameters =>
                        (string)parameters["Name"] == "Ubuntu" && (string)parameters["Tag"] == "dev"),
                    null, It.IsAny<CancellationToken>()), Times.Once);
                break;
            case "SetInstanceTagsAsync":
                await client.SetInstanceTagsAsync("Ubuntu", ["dev", "test"]);
                powerShell.Verify(service => service.ExecuteModuleCmdletAsync(command,
                    It.Is<Dictionary<string, object>>(parameters =>
                        (string)parameters["Name"] == "Ubuntu" &&
                        ((string[])parameters["Tags"]).SequenceEqual(new[] { "dev", "test" })),
                    null, It.IsAny<CancellationToken>()), Times.Once);
                break;
            case "RemoveInstanceTagAsync":
                await client.RemoveInstanceTagAsync("Ubuntu", "dev");
                powerShell.Verify(service => service.ExecuteModuleCmdletAsync(command,
                    It.Is<Dictionary<string, object>>(parameters =>
                        (string)parameters["Name"] == "Ubuntu" && (string)parameters["Tag"] == "dev"),
                    null, It.IsAny<CancellationToken>()), Times.Once);
                break;
            case "RenameInstanceTagsAsync":
                await client.RenameInstanceTagsAsync("Ubuntu", "Ubuntu-Dev");
                powerShell.Verify(service => service.ExecuteModuleCmdletAsync(command,
                    It.Is<Dictionary<string, object>>(parameters =>
                        (string)parameters["OldName"] == "Ubuntu" && (string)parameters["NewName"] == "Ubuntu-Dev"),
                    null, It.IsAny<CancellationToken>()), Times.Once);
                break;
        }
    }

    [Fact]
    public async Task TagMutation_PropagatesModuleFailure()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell
            .Setup(service => service.ExecuteModuleCmdletAsync(
                "Remove-DistroNexusInstanceTag",
                It.IsAny<Dictionary<string, object>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 1, Error = "module failure" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RemoveInstanceTagAsync("Ubuntu", "dev"));

        Assert.Equal("module failure", exception.Message);
    }

    [Fact]
    public void Interface_ExposesOnlyTheRegisteredTypedTagOperations()
    {
        var methods = typeof(IPowerShellModuleClient).GetMethods();

        Assert.Equal(
            [
                nameof(IPowerShellModuleClient.AddInstanceTagAsync),
                nameof(IPowerShellModuleClient.GetInstanceTagsAsync),
                nameof(IPowerShellModuleClient.RemoveInstanceTagAsync),
                nameof(IPowerShellModuleClient.RenameInstanceTagsAsync),
                nameof(IPowerShellModuleClient.SetInstanceTagsAsync)
            ],
            methods.Select(method => method.Name).Order());
        Assert.DoesNotContain(methods, method => method.Name.Contains("Script", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.Name.Contains("Command", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.Name.Contains("Operation", StringComparison.OrdinalIgnoreCase));
    }

    private static Mock<IPowerShellService> CreateServiceReturning(string output)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell
            .Setup(service => service.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstanceTag",
                null,
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = output });
        return powerShell;
    }
}
