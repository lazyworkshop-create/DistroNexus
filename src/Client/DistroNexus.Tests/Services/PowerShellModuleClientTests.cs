using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class PowerShellModuleClientTests
{
    [Fact]
    public async Task PackageQueries_UseClosedCmdletParametersAndDeserializeResults()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", It.Is<Dictionary<string, object>>(p => p.ContainsKey("Family") && Equals(p["Family"], "Ubuntu")), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[{\"Id\":\"ubuntu\",\"Name\":\"Ubuntu\"}]" });
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", It.Is<Dictionary<string, object>>(p => p.ContainsKey("Id") && Equals(p["Id"], "ubuntu")), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Id\":\"ubuntu\",\"Name\":\"Ubuntu\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        Assert.Equal("ubuntu", Assert.Single(await client.GetPackagesAsync("Ubuntu")).Id);
        Assert.Equal("ubuntu", (await client.GetPackageAsync("ubuntu"))!.Id);
    }

    [Fact]
    public async Task PackageList_DefaultForceAndSearch_UseTheirModeledParameters()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", null, It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[]" });
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", It.Is<Dictionary<string, object>>(p => p != null && p.ContainsKey("ForceReload")), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[]" });
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", It.Is<Dictionary<string, object>>(p => p != null && p.ContainsKey("Query") && Equals(p["Query"], "ubuntu")), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[{\"Id\":\"ubuntu\"}]" });
        var client = new PowerShellModuleClient(powerShell.Object);
        await client.GetPackagesAsync(); await client.GetPackagesAsync(forceReload: true);
        Assert.Equal("ubuntu", Assert.Single(await client.SearchPackagesAsync("ubuntu")).Id);
    }

    [Fact]
    public async Task PackageQuery_PreservesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", It.IsAny<Dictionary<string, object>?>(), It.IsAny<ModuleCallOptions>(), cancellation.Token)).Returns(Task.FromCanceled<PowerShellScriptResult>(cancellation.Token));
        await Assert.ThrowsAsync<TaskCanceledException>(() => new PowerShellModuleClient(powerShell.Object).SearchPackagesAsync("ubuntu", cancellation.Token));
    }

    [Fact]
    public async Task RefreshCatalog_UsesTheFixedCmdletAndOptionalSourceUrl()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Update-DistroNexusCatalog", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"SourceId\":\"legacy\",\"CacheState\":\"Updated\",\"DiagnosticCode\":\"Catalog.RefreshUpdated\"}" });
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Update-DistroNexusCatalog", It.Is<Dictionary<string, object>>(p => p != null && Equals(p["SourceUrl"], "https://example.test/catalog.json")), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"SourceId\":\"override\",\"CacheState\":\"Updated\",\"DiagnosticCode\":\"Catalog.RefreshUpdated\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        Assert.Equal("legacy", (await client.RefreshCatalogAsync()).SourceId);
        Assert.Equal("override", (await client.RefreshCatalogAsync("https://example.test/catalog.json")).SourceId);
    }

    [Fact]
    public async Task PackageQueries_PropagateFailureAndCancellation()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusPackage", It.IsAny<Dictionary<string, object>?>(), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 1, Error = "failed" });
        var client = new PowerShellModuleClient(powerShell.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchPackagesAsync("ubuntu"));
    }

    [Fact]
    public async Task GetInstancesAsync_UsesTheRegisteredCmdletAndMapsModuleResults()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstance", null, It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[{\"Name\":\"Ubuntu\",\"State\":\"Running\",\"Version\":2,\"BasePath\":\"C:\\\\WSL\\\\Ubuntu\",\"DiskSize\":123,\"Distribution\":\"Ubuntu\"}]" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var instance = Assert.Single(await client.GetInstancesAsync());

        Assert.Equal("Ubuntu", instance.Name);
        Assert.Equal("Running", instance.State);
        Assert.Equal(2, instance.Version);
        Assert.Equal("C:\\WSL\\Ubuntu", instance.InstallPath);
        Assert.Equal(123, instance.Size);
        powerShell.Verify(service => service.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance", null, It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Start-DistroNexusInstance", "StartInstanceAsync")]
    [InlineData("Stop-DistroNexusInstance", "StopInstanceAsync")]
    public async Task InstanceMutation_UsesItsFixedRegisteredCmdlet(string command, string method)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(command,
                It.Is<Dictionary<string, object>>(parameters => (string)parameters["Name"] == "Ubuntu"), null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "True" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var result = method == "StartInstanceAsync"
            ? await client.StartInstanceAsync("Ubuntu")
            : await client.StopInstanceAsync("Ubuntu");

        Assert.True(result);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task GetInstancesAsync_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusInstance", null,
                It.IsAny<ModuleCallOptions>(), cancellation.Token))
            .Returns(Task.FromCanceled<PowerShellScriptResult>(cancellation.Token));
        var client = new PowerShellModuleClient(powerShell.Object);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetInstancesAsync(cancellation.Token));
    }

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
    public async Task GetSettingsAsync_UsesTheFixedCmdletAndDeserializesModeledSettings()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Get-DistroNexusSettings", null, It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Theme\":\"Dark\",\"DefaultWslVersion\":1}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var settings = await client.GetSettingsAsync();

        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(1, settings.DefaultWslVersion);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task SaveSettingsAsync_SendsOnlyExplicitTypedFieldsToTheFixedCmdlet()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Set-DistroNexusSettings",
                It.Is<Dictionary<string, object>>(parameters => parameters.Count == 2 &&
                    (string)parameters["Theme"] == "Light" && (bool)parameters["ShowConfirmationDialogs"] == false),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0 });
        var client = new PowerShellModuleClient(powerShell.Object);

        await client.SaveSettingsAsync(new DistroNexusSettingsUpdate(Theme: "Light", ShowConfirmationDialogs: false));

        powerShell.VerifyAll();
    }

    [Fact]
    public async Task SaveSettingsAsync_CanExplicitlyClearTheModulePath()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Set-DistroNexusSettings",
                It.Is<Dictionary<string, object>>(parameters => parameters.Count == 1 &&
                    parameters.ContainsKey("PowerShellModulePath") && parameters["PowerShellModulePath"] == null),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0 });
        var client = new PowerShellModuleClient(powerShell.Object);

        await client.SaveSettingsAsync(new DistroNexusSettingsUpdate(PowerShellModulePath: null, UpdatePowerShellModulePath: true));

        powerShell.VerifyAll();
    }

    [Fact]
    public async Task ResetSettingsAsync_UsesTheFixedCmdletAndPreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Reset-DistroNexusSettings", null, null, cancellation.Token))
            .Returns(Task.FromCanceled<PowerShellScriptResult>(cancellation.Token));
        var client = new PowerShellModuleClient(powerShell.Object);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.ResetSettingsAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("[{\"Id\":\"one\",\"Name\":\"Official\",\"Url\":\"https://example.test/catalog\",\"Priority\":1}]")]
    [InlineData("{\"Id\":\"one\",\"Name\":\"Official\",\"Url\":\"https://example.test/catalog\",\"Priority\":1}")]
    public async Task GetCatalogSourcesAsync_UsesTheFixedCmdletAndHandlesArrayOrSingletonResults(string output)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Get-DistroNexusCatalogSource", null, It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = output });
        var client = new PowerShellModuleClient(powerShell.Object);

        var source = Assert.Single(await client.GetCatalogSourcesAsync());

        Assert.Equal("one", source.Id);
        Assert.Equal("Official", source.Name);
        powerShell.VerifyAll();
    }

    [Theory]
    [InlineData("Add-DistroNexusCatalogSource", "Add")]
    [InlineData("Set-DistroNexusCatalogSource", "Update")]
    public async Task CatalogSourceWrite_UsesFixedCmdletParametersAndDeserializesResult(string command, string operation)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                command,
                It.Is<Dictionary<string, object>>(parameters =>
                    (string)parameters["Name"] == "Official" &&
                    (string)parameters["Url"] == "https://example.test/catalog" &&
                    (string)parameters["Description"] == "Catalog" &&
                    (bool)parameters["IsActive"] &&
                    (operation != "Update" || (string)parameters["SourceId"] == "source-1")),
                It.Is<ModuleCallOptions>(options => options.ParseAsJson),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Id\":\"source-1\",\"Name\":\"Official\",\"Url\":\"https://example.test/catalog\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var source = operation == "Add"
            ? await client.AddCatalogSourceAsync(new DistroNexusCatalogSourceCreateRequest("Official", "https://example.test/catalog", "Catalog"))
            : await client.UpdateCatalogSourceAsync(new DistroNexusCatalogSourceUpdateRequest("source-1", "Official", "https://example.test/catalog", "Catalog"));

        Assert.Equal("source-1", source.Id);
        powerShell.VerifyAll();
    }

    [Theory]
    [InlineData("Remove-DistroNexusCatalogSource", "Remove")]
    [InlineData("Test-DistroNexusCatalogSource", "Test")]
    [InlineData("Set-DistroNexusCatalogSourceActive", "Active")]
    [InlineData("Set-DistroNexusCatalogSourceOrder", "Order")]
    [InlineData("Reset-DistroNexusCatalogSource", "Reset")]
    public async Task CatalogSourceBooleanOperation_UsesFixedCmdletAndReturnsBoolean(string command, string operation)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                command,
                It.Is<Dictionary<string, object>?>(parameters => MatchesCatalogBooleanParameters(operation, parameters)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "False" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var value = operation switch
        {
            "Remove" => await client.RemoveCatalogSourceAsync("source-1"),
            "Test" => await client.TestCatalogSourceAsync("https://example.test/catalog"),
            "Active" => await client.SetCatalogSourceActiveAsync("source-1", false),
            "Order" => await client.ReorderCatalogSourcesAsync(["source-1", "source-2"]),
            "Reset" => await client.ResetCatalogSourcesAsync(),
            _ => throw new InvalidOperationException()
        };

        Assert.False(value);
        powerShell.VerifyAll();
    }

    [Theory]
    [InlineData("Remove-DistroNexusCatalogSource")]
    [InlineData("Test-DistroNexusCatalogSource")]
    [InlineData("Set-DistroNexusCatalogSourceActive")]
    [InlineData("Set-DistroNexusCatalogSourceOrder")]
    [InlineData("Reset-DistroNexusCatalogSource")]
    public async Task CatalogSourceBooleanOperation_RejectsInvalidResultAndPropagatesModuleFailures(string command)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.SetupSequence(service => service.ExecuteModuleCmdletAsync(command, It.IsAny<Dictionary<string, object>?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "not-a-boolean" })
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 1, Error = "module failure" });
        var client = new PowerShellModuleClient(powerShell.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeCatalogBooleanOperation(client, command));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeCatalogBooleanOperation(client, command));

        Assert.Equal("module failure", exception.Message);
    }

    [Fact]
    public async Task CatalogSourceBooleanOperation_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync(
                "Remove-DistroNexusCatalogSource", It.IsAny<Dictionary<string, object>>(), null, cancellation.Token))
            .Returns(Task.FromCanceled<PowerShellScriptResult>(cancellation.Token));
        var client = new PowerShellModuleClient(powerShell.Object);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.RemoveCatalogSourceAsync("source-1", cancellation.Token));
    }

    [Fact]
    public void Interface_ExposesOnlyTheRegisteredTypedOperations()
    {
        var methods = typeof(IPowerShellModuleClient).GetMethods();

        Assert.Equal(
            [
                nameof(IPowerShellModuleClient.AddCatalogSourceAsync),
                nameof(IPowerShellModuleClient.AddInstanceTagAsync),
                nameof(IPowerShellModuleClient.ClearPackageCacheAsync),
                nameof(IPowerShellModuleClient.DeletePackageCacheEntryAsync),
                nameof(IPowerShellModuleClient.DiscoverWslgApplicationsAsync),
                nameof(IPowerShellModuleClient.GetCatalogSourcesAsync),
                nameof(IPowerShellModuleClient.GetContainerRuntimeStatusAsync),
                nameof(IPowerShellModuleClient.GetDockerIntegrationAsync),
                nameof(IPowerShellModuleClient.GetDockerIntegrationPreviewAsync),
                nameof(IPowerShellModuleClient.GetHostCapabilitiesAsync),
                nameof(IPowerShellModuleClient.GetInstanceCapabilitiesAsync),
                nameof(IPowerShellModuleClient.GetInstancesAsync),
                nameof(IPowerShellModuleClient.GetInstanceTagsAsync),
                nameof(IPowerShellModuleClient.GetMonitoringProcessActionPreviewAsync),
                nameof(IPowerShellModuleClient.GetMonitoringSnapshotAsync),
                nameof(IPowerShellModuleClient.GetPackageAsync),
                nameof(IPowerShellModuleClient.GetPackageCacheLocationAsync),
                nameof(IPowerShellModuleClient.GetPackageCacheUsageAsync),
                nameof(IPowerShellModuleClient.GetPackagesAsync),
                nameof(IPowerShellModuleClient.GetPodmanConnectionPreviewAsync),
                nameof(IPowerShellModuleClient.GetPodmanUserUnitPreviewAsync),
                nameof(IPowerShellModuleClient.GetSettingsAsync),
                nameof(IPowerShellModuleClient.GetTerminalStatusAsync),
                nameof(IPowerShellModuleClient.GetWslgStatusAsync),
                nameof(IPowerShellModuleClient.InvokeMonitoringProcessActionAsync),
                nameof(IPowerShellModuleClient.InvokePodmanConnectionAsync),
                nameof(IPowerShellModuleClient.InvokePodmanUserUnitAsync),
                nameof(IPowerShellModuleClient.LaunchWslgApplicationAsync),
                nameof(IPowerShellModuleClient.OpenPackageCacheFolderAsync),
                nameof(IPowerShellModuleClient.OpenRecoveryPointFolderAsync),
                nameof(IPowerShellModuleClient.OpenWslConfigFileAsync),
                nameof(IPowerShellModuleClient.RefreshCatalogAsync),
                nameof(IPowerShellModuleClient.RemoveCatalogSourceAsync),
                nameof(IPowerShellModuleClient.RemoveInstanceTagAsync),
                nameof(IPowerShellModuleClient.RenameInstanceTagsAsync),
                nameof(IPowerShellModuleClient.ReorderCatalogSourcesAsync),
                nameof(IPowerShellModuleClient.ResetCatalogSourcesAsync),
                nameof(IPowerShellModuleClient.ResetSettingsAsync),
                nameof(IPowerShellModuleClient.RevealWslgApplicationAsync),
                nameof(IPowerShellModuleClient.SaveSettingsAsync),
                nameof(IPowerShellModuleClient.SearchPackagesAsync),
                nameof(IPowerShellModuleClient.SetCatalogSourceActiveAsync),
                nameof(IPowerShellModuleClient.SetDockerIntegrationAsync),
                nameof(IPowerShellModuleClient.SetInstanceTagsAsync),
                nameof(IPowerShellModuleClient.SetWslgApplicationPinAsync),
                nameof(IPowerShellModuleClient.StartInstanceAsync),
                nameof(IPowerShellModuleClient.StartTerminalAsync),
                nameof(IPowerShellModuleClient.StopInstanceAsync),
                nameof(IPowerShellModuleClient.TestCatalogSourceAsync),
                nameof(IPowerShellModuleClient.UpdateCatalogSourceAsync)
            ],
            methods.Select(method => method.Name).Order());
        Assert.DoesNotContain(methods, method => method.Name.Contains("Script", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.Name.Contains("Command", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.Name.Equals("ExecuteOperationAsync", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExplorerOperations_UseOnlyFixedCmdletsAndAnExactRecoveryId()
    {
        var id = Guid.NewGuid();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Open-DistroNexusWslConfigFile", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"OutcomeCode\":\"Opened\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Open-DistroNexusRecoveryPointFolder", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (Guid)p["Id"] == id), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"OutcomeCode\":\"Opened\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        Assert.True((await client.OpenWslConfigFileAsync()).Succeeded);
        Assert.True((await client.OpenRecoveryPointFolderAsync(id)).Succeeded);
        await Assert.ThrowsAsync<ArgumentException>(() => client.OpenRecoveryPointFolderAsync(Guid.Empty));
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task ContainerAndCapabilityQueries_UseOnlyTheirFixedCmdlets()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusContainerRuntimeStatus", It.Is<Dictionary<string, object>>(p => (string)p["Name"] == "Ubuntu"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Runtimes\":[],\"Containers\":{},\"Images\":{},\"Projects\":{},\"Failures\":{}}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusCapability", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["Name"] == "Ubuntu"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Instance\":{\"Name\":\"Ubuntu\"},\"Capabilities\":{},\"RefreshedAt\":\"2026-01-01T00:00:00+00:00\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusCapability", It.Is<Dictionary<string, object>>(p => p.Count == 1 && p.ContainsKey("Host") && (bool)p["Host"]), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Host\":{},\"Capabilities\":{},\"RefreshedAt\":\"2026-01-01T00:00:00+00:00\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        Assert.Empty((await client.GetContainerRuntimeStatusAsync("Ubuntu")).Runtimes);
        Assert.Empty((await client.GetInstanceCapabilitiesAsync("Ubuntu")).Capabilities);
        Assert.Empty((await client.GetHostCapabilitiesAsync()).Capabilities);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task WslgOperations_UseOnlyFixedCmdletsAndAuthorityFreeParameters()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusWslgStatus", It.Is<Dictionary<string, object>>(p => p.Count == 1 && p.ContainsKey("Name") && (string)p["Name"] == "Ubuntu"), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode=0, Output="{\"IsAvailable\":true,\"Reason\":\"ok\",\"Guidance\":[]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusWslgApplication", It.Is<Dictionary<string, object>>(p => p.Count == 1 && p.ContainsKey("Name") && (string)p["Name"] == "Ubuntu"), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode=0, Output="{\"Status\":{\"IsAvailable\":true,\"Reason\":\"ok\",\"Guidance\":[]},\"DiscoveryToken\":\"" + new string('a',64) + "\",\"Applications\":[]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync(It.Is<string>(c => c == "Start-DistroNexusWslgApplication" || c == "Show-DistroNexusWslgApplicationEntry"), It.Is<Dictionary<string, object>>(p => p.Count == 2 && p.ContainsKey("ApplicationId") && p.ContainsKey("DiscoveryToken") && (string)p["ApplicationId"] == "app"), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode=0, Output="{\"Succeeded\":true,\"Diagnostic\":\"ok\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Set-DistroNexusWslgApplicationPin", It.Is<Dictionary<string, object>>(p => p.Count == 3 && p.ContainsKey("ApplicationId") && p.ContainsKey("DiscoveryToken") && p.ContainsKey("Pinned") && (bool)p["Pinned"]), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode=0, Output="{\"Succeeded\":true,\"Diagnostic\":\"ok\"}" });
        var client=new PowerShellModuleClient(powerShell.Object); var token=new string('a',64);
        await client.GetWslgStatusAsync("Ubuntu"); await client.DiscoverWslgApplicationsAsync("Ubuntu"); await client.LaunchWslgApplicationAsync(token,"app"); await client.RevealWslgApplicationAsync(token,"app"); await client.SetWslgApplicationPinAsync(token,"app",true);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task PodmanOperations_UseFixedCmdletsAndScalarExecuteParameters()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPodmanUserUnitPreview", It.Is<Dictionary<string, object>>(p => (string)p["Name"] == "Ubuntu" && (string)p["Unit"] == "Socket" && (string)p["Action"] == "Start"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Token\":\"unit-token\",\"InstanceName\":\"Ubuntu\",\"Unit\":\"Socket\",\"Action\":\"Start\",\"Effects\":[\"start\"]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Invoke-DistroNexusPodmanUserUnit", It.Is<Dictionary<string, object>>(p => (string)p["PreviewToken"] == "unit-token" && (string)p["InstanceName"] == "Ubuntu" && (string)p["Unit"] == "Socket" && (string)p["Action"] == "Start"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"OutcomeCode\":\"Succeeded\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPodmanConnectionPreview", It.Is<Dictionary<string, object>>(p => (string)p["Name"] == "Ubuntu" && (string)p["ConnectionName"] == "local"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Token\":\"connection-token\",\"InstanceName\":\"Ubuntu\",\"Name\":\"local\",\"Endpoint\":\"unix:///run/user/1000/podman/podman.sock\",\"Operation\":\"Create\",\"Effects\":[\"configure\"]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Invoke-DistroNexusPodmanConnection", It.Is<Dictionary<string, object>>(p => (string)p["PreviewToken"] == "connection-token" && (string)p["ConnectionName"] == "local"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"OutcomeCode\":\"Succeeded\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var unitPreview = await client.GetPodmanUserUnitPreviewAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Start);
        Assert.True((await client.InvokePodmanUserUnitAsync(unitPreview)).Succeeded);
        var connectionPreview = await client.GetPodmanConnectionPreviewAsync("Ubuntu", new PodmanConnectionRequest("local", new Uri("unix:///run/user/1000/podman/podman.sock")));
        Assert.True((await client.InvokePodmanConnectionAsync(connectionPreview)).Succeeded);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task PodmanOperations_RejectInvalidInputBeforeModuleInvocation()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        var client = new PowerShellModuleClient(powerShell.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetContainerRuntimeStatusAsync("bad\nname"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetPodmanUserUnitPreviewAsync("Ubuntu", PodmanUserUnit.Socket, SystemdAction.Restart));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPodmanConnectionPreviewAsync("Ubuntu", new PodmanConnectionRequest("local", new Uri("https://example.test"))));
    }

    [Fact]
    public async Task ContainerRuntimeQuery_PreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusContainerRuntimeStatus", It.IsAny<Dictionary<string, object>>(), It.IsAny<ModuleCallOptions>(), cancellation.Token))
            .Returns(Task.FromCanceled<PowerShellScriptResult>(cancellation.Token));

        await Assert.ThrowsAsync<TaskCanceledException>(() => new PowerShellModuleClient(powerShell.Object).GetContainerRuntimeStatusAsync("Ubuntu", cancellation.Token));
    }

    [Fact]
    public async Task PackageCacheOperations_UseOnlyFixedCmdletsAndTypedResults()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPackageCacheLocation", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"CachePath\":\"C:\\\\cache\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPackageCacheUsage", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"CachePath\":\"C:\\\\cache\",\"PackageCount\":1,\"CachedPackages\":[]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Remove-DistroNexusPackage", It.Is<Dictionary<string, object>>(p => (string)p["CacheEntryId"] == "token"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Deleted\":true,\"DiagnosticCode\":\"PackageCache.Deleted\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Clear-DistroNexusPackageCache", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"DeletedCount\":1,\"FailedCount\":0,\"DiagnosticCode\":\"PackageCache.Cleared\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        Assert.Equal("C:\\cache", (await client.GetPackageCacheLocationAsync()).CachePath);
        Assert.Equal(1, (await client.GetPackageCacheUsageAsync()).PackageCount);
        Assert.True((await client.DeletePackageCacheEntryAsync("token")).Deleted);
        Assert.Equal(1, (await client.ClearPackageCacheAsync()).DeletedCount);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task TerminalOperations_UseOnlyFixedCmdletsAndRejectUnsafePaths()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusTerminalStatus", It.Is<Dictionary<string, object>>(p => p.Count == 0), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"WindowsTerminalAvailable\":true,\"CommandPromptAvailable\":true,\"DefaultKind\":\"WindowsTerminal\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Start-DistroNexusTerminal", It.Is<Dictionary<string, object>>(p => (string)p["Name"] == "Ubuntu" && (string)p["StartPath"] == "/home/user" && (string)p["TerminalKind"] == "WindowsTerminal"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"SelectedKind\":\"WindowsTerminal\",\"OutcomeCode\":\"Terminal.Launched\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Open-DistroNexusPackageCacheFolder", It.Is<Dictionary<string, object>>(p => p.Count == 0), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"SelectedKind\":\"Auto\",\"OutcomeCode\":\"PackageCache.Opened\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        Assert.Equal(TerminalKind.WindowsTerminal, (await client.GetTerminalStatusAsync()).DefaultKind);
        Assert.True((await client.StartTerminalAsync("Ubuntu", "/home/user", TerminalKind.WindowsTerminal)).Succeeded);
        Assert.True((await client.OpenPackageCacheFolderAsync()).Succeeded);
        await Assert.ThrowsAsync<ArgumentException>(() => client.StartTerminalAsync("Ubuntu", "C:\\outside"));
        powerShell.VerifyAll();
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

    private static Task<bool> InvokeCatalogBooleanOperation(PowerShellModuleClient client, string command) => command switch
    {
        "Remove-DistroNexusCatalogSource" => client.RemoveCatalogSourceAsync("source-1"),
        "Test-DistroNexusCatalogSource" => client.TestCatalogSourceAsync("https://example.test/catalog"),
        "Set-DistroNexusCatalogSourceActive" => client.SetCatalogSourceActiveAsync("source-1", true),
        "Set-DistroNexusCatalogSourceOrder" => client.ReorderCatalogSourcesAsync(["source-1"]),
        "Reset-DistroNexusCatalogSource" => client.ResetCatalogSourcesAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private static bool MatchesCatalogBooleanParameters(string operation, Dictionary<string, object>? parameters)
    {
        return operation switch
        {
            "Remove" => parameters is not null && (string)parameters["SourceId"] == "source-1",
            "Test" => parameters is not null && (string)parameters["Url"] == "https://example.test/catalog",
            "Active" => parameters is not null && (string)parameters["SourceId"] == "source-1" && !(bool)parameters["IsActive"],
            "Order" => parameters is not null && ((string[])parameters["SourceId"]).SequenceEqual(new[] { "source-1", "source-2" }),
            "Reset" => parameters is null,
            _ => false
        };
    }
}
