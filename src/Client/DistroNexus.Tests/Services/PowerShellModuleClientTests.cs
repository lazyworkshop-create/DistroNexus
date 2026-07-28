using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;
using System.Security;
using System.Text.Json;

namespace DistroNexus.Tests.Services;

public sealed class PowerShellModuleClientTests
{
    [Fact]
    public async Task UsbReads_UseClosedCommandsAndRejectUnexpectedDeviceFields()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusUsbStatus", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"IsInstalled\":true,\"ServiceState\":\"Running\",\"Version\":\"5.1\",\"SupportsActions\":false,\"Reason\":null,\"OutcomeCode\":\"Usb.Ready\"}" });
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusUsbDevice", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Devices\":[{\"BusId\":\"1-2\",\"Description\":\"Fixture\",\"Availability\":\"Shared\",\"SharedState\":true,\"AttachedState\":false,\"IsStorage\":false,\"Distribution\":null,\"Guidance\":null}],\"OutcomeCode\":\"Usb.Ready\"}" });
        var client = new PowerShellModuleClient(service.Object);
        Assert.Equal("Running", (await client.GetUsbStatusAsync()).ServiceState);
        Assert.Equal("1-2", Assert.Single((await client.GetUsbDevicesAsync()).Devices).BusId);
    }

    [Fact]
    public async Task UsbDeviceRead_RejectsRawOrUnknownFields()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusUsbDevice", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Devices\":[{\"BusId\":\"1-2\",\"Description\":\"Fixture\",\"Availability\":\"Shared\",\"SharedState\":true,\"AttachedState\":false,\"IsStorage\":false,\"Distribution\":null,\"Guidance\":null,\"RawPath\":\"C:\\\\secret\"}],\"OutcomeCode\":\"Usb.Ready\"}" });
        await Assert.ThrowsAsync<JsonException>(() => new PowerShellModuleClient(service.Object).GetUsbDevicesAsync());
    }

    [Theory]
    [InlineData("Get-DistroNexusUsbStatus")]
    [InlineData("Get-DistroNexusUsbDevice")]
    public async Task UsbReads_RejectOversizedUtf8EnvelopeBeforeParsing(string command)
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync(command, null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = new string('x', 64 * 1024 + 1) });
        var client = new PowerShellModuleClient(service.Object);
        if (command == "Get-DistroNexusUsbStatus") await Assert.ThrowsAsync<JsonException>(() => client.GetUsbStatusAsync());
        else await Assert.ThrowsAsync<JsonException>(() => client.GetUsbDevicesAsync());
    }

    [Fact]
    public async Task UsbStatusRead_RejectsActionCapability()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusUsbStatus", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"IsInstalled\":true,\"ServiceState\":\"Running\",\"Version\":\"5.1\",\"SupportsActions\":true,\"Reason\":null,\"OutcomeCode\":\"Usb.Ready\"}" });
        await Assert.ThrowsAsync<JsonException>(() => new PowerShellModuleClient(service.Object).GetUsbStatusAsync());
    }

    [Fact]
    public async Task UsbDeviceRead_RejectsContradictoryAvailabilityState()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusUsbDevice", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Devices\":[{\"BusId\":\"1-2\",\"Description\":\"Fixture\",\"Availability\":\"Available\",\"SharedState\":true,\"AttachedState\":false,\"IsStorage\":false,\"Distribution\":null,\"Guidance\":null}],\"OutcomeCode\":\"Usb.Ready\"}" });
        await Assert.ThrowsAsync<JsonException>(() => new PowerShellModuleClient(service.Object).GetUsbDevicesAsync());
    }

    [Fact]
    public async Task SetCredential_UsesDedicatedSecureParameterBinding()
    {
        using var secret = new SecureString(); secret.AppendChar('x'); secret.MakeReadOnly();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletWithSecureStringAsync(
                "Set-DistroNexusCredential",
                It.Is<IReadOnlyDictionary<string, object>>(parameters => parameters.Count == 3 && (string)parameters["Name"] == "Ubuntu" && (string)parameters["Username"] == "developer" && !(bool)parameters["Confirm"] && !parameters.ContainsKey("Password")),
                "Password", secret, It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"InstanceName\":\"Ubuntu\",\"OutcomeCode\":\"Lifecycle.CredentialSucceeded\"}" });

        var result = await new PowerShellModuleClient(powerShell.Object).SetCredentialAsync("Ubuntu", "developer", secret);

        Assert.True(result.Succeeded);
        Assert.Equal("Ubuntu", result.InstanceName);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task SetCredential_PreservesStableCredentialOutcomeCode()
    {
        using var secret = new SecureString(); secret.AppendChar('x'); secret.MakeReadOnly();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletWithSecureStringAsync(
                "Set-DistroNexusCredential", It.IsAny<IReadOnlyDictionary<string, object>>(), "Password", secret,
                It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":false,\"InstanceName\":\"Ubuntu\",\"OutcomeCode\":\"Lifecycle.CredentialGrantExpired\"}" });

        var result = await new PowerShellModuleClient(powerShell.Object).SetCredentialAsync("Ubuntu", "developer", secret);

        Assert.False(result.Succeeded);
        Assert.Equal("Lifecycle.CredentialGrantExpired", result.OutcomeCode);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task VerifiedInstallOperations_UseOnlyRegisteredCommandsAndDirectSecureBinding()
    {
        const string token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using var secret = new SecureString(); secret.AppendChar('x'); secret.MakeReadOnly();
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusInstallSource", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["PackageId"] == "ubuntu"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"PackageId\":\"ubuntu\",\"CacheState\":\"Missing\",\"DownloadAvailable\":true,\"ExpectedSha256\":\"hash\",\"ExpectedSizeBytes\":1,\"SourceProvenance\":\"Catalog\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPackageAcquisitionPreview", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["PackageId"] == "ubuntu"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = $$"""{"PreviewToken":"{{token}}","PackageId":"ubuntu","ExpiresAt":"2030-01-01T00:00:00+00:00"}""" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Invoke-DistroNexusPackageAcquisition", It.Is<Dictionary<string, object>>(p => p.Count == 2 && (string)p["PreviewToken"] == token && !(bool)p["Confirm"]), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = $$"""{"PackageReference":"{{token}}","PackageId":"ubuntu","Sha256":"hash","SizeBytes":1,"ExpiresAt":"2030-01-01T00:00:00+00:00","OutcomeCode":"Lifecycle.Acquired"}""" });
        powerShell.Setup(x => x.ExecuteModuleCmdletWithSecureStringAsync("Install-DistroNexusInstance", It.Is<IReadOnlyDictionary<string, object>>(p => p.Count == 7 && (string)p["PackageReference"] == token && (string)p["Name"] == "Ubuntu" && !p.ContainsKey("Password") && !(bool)p["Confirm"]), "Password", secret, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"Operation\":\"Install\",\"InstanceName\":\"Ubuntu\",\"OutcomeCode\":\"Lifecycle.Succeeded\",\"RecoveryAction\":\"None\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        Assert.Equal("Missing", (await client.ResolveInstallSourceAsync("ubuntu")).CacheState);
        Assert.Equal(token, (await client.PreviewPackageAcquisitionAsync("ubuntu")).PreviewToken);
        Assert.Equal(token, (await client.AcquirePackageAsync(token)).PackageReference);
        Assert.True((await client.InstallVerifiedInstanceAsync(token, "Ubuntu", "D:\\WSL", "developer", "bash", null, false, secret)).Succeeded);
        powerShell.VerifyAll();
    }

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
                It.Is<Dictionary<string, object>>(parameters => (string)parameters["Name"] == "Ubuntu"), It.Is<ModuleCallOptions>(options => options.ParseAsJson),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = command.StartsWith("Start", StringComparison.Ordinal) ? "{\"Succeeded\":true,\"Started\":true,\"KeepAliveEstablished\":false}" : "{\"Succeeded\":true}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var result = method == "StartInstanceAsync"
            ? await client.StartInstanceAsync("Ubuntu")
            : await client.StopInstanceAsync("Ubuntu");

        Assert.True(result);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task StartInstanceWithResultAsync_UsesKeepAliveAndDeserializesSanitizedResult()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Start-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(parameters => (string)parameters["Name"] == "Ubuntu" && (bool)parameters["KeepAlive"]), It.Is<ModuleCallOptions>(options => options.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"Started\":true,\"KeepAliveEstablished\":true}" });
        var client = new PowerShellModuleClient(powerShell.Object);

        var result = await client.StartInstanceWithResultAsync("Ubuntu", keepAlive: true);

        Assert.Equal(new InstanceStartResult(true, true, true), result);
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
    public void SettingsUpdate_DoesNotExposeAModulePathSelector()
    {
        Assert.DoesNotContain(typeof(DistroNexusSettingsUpdate).GetProperties(), property => property.Name == "PowerShellModulePath");
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

    [Theory]
    [InlineData("C:\\\\secret")]
    [InlineData("\\\\server\\share")]
    [InlineData("\\\\?\\C:\\device")]
    [InlineData("/var/lib/distronexus")]
    public async Task DiagnosticSnapshot_UsesClosedCommandAndRejectsUnsafeNotice(string unsafeMessage)
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusDiagnosticSnapshot", null, It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"ModuleState\":\"Ready\",\"WslState\":\"Ready\",\"BridgeState\":\"Ready\",\"Notices\":[],\"OutcomeCode\":\"Diagnostic.Ready\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        var snapshot = await client.GetDiagnosticSnapshotAsync();
        Assert.Equal("Diagnostic.Ready", snapshot.OutcomeCode);

        powerShell.Reset();
        powerShell.Setup(service => service.ExecuteModuleCmdletAsync("Get-DistroNexusDiagnosticSnapshot", null, It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = JsonSerializer.Serialize(new { ModuleState = "Ready", WslState = "Ready", BridgeState = "Ready", Notices = new[] { new { Code = "WSL.Path", Severity = "Error", Message = unsafeMessage } }, OutcomeCode = "Diagnostic.Ready" }) });
        await Assert.ThrowsAsync<JsonException>(() => client.GetDiagnosticSnapshotAsync());
    }

    [Fact]
    public void Interface_ExposesOnlyTheRegisteredTypedOperations()
    {
        var methods = typeof(IPowerShellModuleClient).GetMethods();

        Assert.Equal(
            new[]
            {
                nameof(IPowerShellModuleClient.AddCatalogSourceAsync),
                nameof(IPowerShellModuleClient.AcquirePackageAsync),
                nameof(IPowerShellModuleClient.AddInstanceTagAsync),
                nameof(IPowerShellModuleClient.ClearPackageCacheAsync),
                nameof(IPowerShellModuleClient.CancelTemplateApplyAsync),
                nameof(IPowerShellModuleClient.CloneRecoveryPointAsync),
                nameof(IPowerShellModuleClient.CompactInstanceAsync),
                nameof(IPowerShellModuleClient.ConsumeBackupNotificationsAsync),
                nameof(IPowerShellModuleClient.CreateFirewallRuleAsync),
                nameof(IPowerShellModuleClient.CreateRecoveryPointAsync),
                nameof(IPowerShellModuleClient.DeletePackageCacheEntryAsync),
                nameof(IPowerShellModuleClient.DiscoverWslgApplicationsAsync),
                nameof(IPowerShellModuleClient.ExecuteLifecycleOperationAsync),
                nameof(IPowerShellModuleClient.ExecutePackageDownloadJobActionAsync),
                nameof(IPowerShellModuleClient.ExecuteRecoveryPointNotesAsync),
                nameof(IPowerShellModuleClient.ExportDiagnosticReportAsync),
                nameof(IPowerShellModuleClient.GetBackupSchedulesAsync),
                nameof(IPowerShellModuleClient.GetBootstrapSettingsAsync),
                nameof(IPowerShellModuleClient.GetCatalogSourcesAsync),
                nameof(IPowerShellModuleClient.GetContainerRuntimeStatusAsync),
                nameof(IPowerShellModuleClient.GetDockerIntegrationAsync),
                nameof(IPowerShellModuleClient.GetDockerIntegrationPreviewAsync),
                nameof(IPowerShellModuleClient.GetDiagnosticLogOptionsAsync),
                nameof(IPowerShellModuleClient.GetDiagnosticReportPreviewAsync),
                nameof(IPowerShellModuleClient.GetDiagnosticSnapshotAsync),
                nameof(IPowerShellModuleClient.GetFirewallCreatePreviewAsync),
                nameof(IPowerShellModuleClient.GetFirewallRemovePreviewAsync),
                nameof(IPowerShellModuleClient.GetFirewallRulesAsync),
                nameof(IPowerShellModuleClient.GetGlobalConfigurationAsync),
                nameof(IPowerShellModuleClient.GetGlobalConfigurationPreviewAsync),
                nameof(IPowerShellModuleClient.GetHostCapabilitiesAsync),
                nameof(IPowerShellModuleClient.GetHealthHistoryAsync),
                nameof(IPowerShellModuleClient.GetHealthRepairPreviewAsync),
                nameof(IPowerShellModuleClient.GetInstanceCapabilitiesAsync),
                nameof(IPowerShellModuleClient.GetInstanceConfigurationAsync),
                nameof(IPowerShellModuleClient.GetInstanceConfigurationRecoveryOfferAsync),
                nameof(IPowerShellModuleClient.GetInstanceIpAddressAsync),
                nameof(IPowerShellModuleClient.GetInstanceResourcesAsync),
                nameof(IPowerShellModuleClient.GetInstancesAsync),
                nameof(IPowerShellModuleClient.GetInstancesAsync),
                nameof(IPowerShellModuleClient.GetInstanceSparsePreviewAsync),
                nameof(IPowerShellModuleClient.GetInstanceTagsAsync),
                nameof(IPowerShellModuleClient.GetMonitoringProcessActionPreviewAsync),
                nameof(IPowerShellModuleClient.GetMonitoringSnapshotAsync),
                nameof(IPowerShellModuleClient.GetNetworkModeAsync),
                nameof(IPowerShellModuleClient.GetNetworkModePreviewAsync),
                nameof(IPowerShellModuleClient.GetNetworkSettingsAsync),
                nameof(IPowerShellModuleClient.GetNetworkSettingsPreviewAsync),
                nameof(IPowerShellModuleClient.GetNetworkStatusAsync),
                nameof(IPowerShellModuleClient.GetPackageAsync),
                nameof(IPowerShellModuleClient.GetPackageDownloadJobsAsync),
                nameof(IPowerShellModuleClient.PreviewPackageAcquisitionAsync),
                nameof(IPowerShellModuleClient.PreviewPackageDownloadJobActionAsync),
                nameof(IPowerShellModuleClient.PreviewPackageDownloadJobStartAsync),
                nameof(IPowerShellModuleClient.GetPackageCacheLocationAsync),
                nameof(IPowerShellModuleClient.GetPackageCacheUsageAsync),
                nameof(IPowerShellModuleClient.GetPackagesAsync),
                nameof(IPowerShellModuleClient.GetPodmanConnectionPreviewAsync),
                nameof(IPowerShellModuleClient.GetPodmanUserUnitPreviewAsync),
                nameof(IPowerShellModuleClient.GetPortMappingsAsync),
                nameof(IPowerShellModuleClient.GetRecoveryClonePreviewAsync),
                nameof(IPowerShellModuleClient.GetRecoveryCreatePreviewAsync),
                nameof(IPowerShellModuleClient.GetRecoveryHistoryAsync),
                nameof(IPowerShellModuleClient.GetRecoveryPointsAsync),
                nameof(IPowerShellModuleClient.GetRecoveryRemovePreviewAsync),
                nameof(IPowerShellModuleClient.GetRecoveryRestorePreviewAsync),
                nameof(IPowerShellModuleClient.GetRecoveryRetentionAsync),
                nameof(IPowerShellModuleClient.GetRecoveryRetentionPreviewAsync),
                nameof(IPowerShellModuleClient.GetSettingsAsync),
                nameof(IPowerShellModuleClient.GetStoreComplianceStatusAsync),
                nameof(IPowerShellModuleClient.GetSystemdServiceDetailsAsync),
                nameof(IPowerShellModuleClient.GetSystemdServiceJournalAsync),
                nameof(IPowerShellModuleClient.GetSystemdServicePreviewAsync),
                nameof(IPowerShellModuleClient.GetSystemdServicesAsync),
                nameof(IPowerShellModuleClient.GetTerminalStatusAsync),
                nameof(IPowerShellModuleClient.GetUpdateStatusAsync),
                nameof(IPowerShellModuleClient.GetUsbDevicesAsync),
                nameof(IPowerShellModuleClient.GetUsbStatusAsync),
                nameof(IPowerShellModuleClient.GetWslgStatusAsync),
                nameof(IPowerShellModuleClient.InvokeBackupAsync),
                nameof(IPowerShellModuleClient.InvokeMonitoringProcessActionAsync),
                nameof(IPowerShellModuleClient.InvokePodmanConnectionAsync),
                nameof(IPowerShellModuleClient.InvokePodmanUserUnitAsync),
                nameof(IPowerShellModuleClient.InvokeSystemdServiceAsync),
                nameof(IPowerShellModuleClient.LaunchWslgApplicationAsync),
                nameof(IPowerShellModuleClient.OpenNetworkLoopbackAsync),
                nameof(IPowerShellModuleClient.OpenPackageCacheFolderAsync),
                nameof(IPowerShellModuleClient.OpenRecoveryPointFolderAsync),
                nameof(IPowerShellModuleClient.OpenWslConfigFileAsync),
                nameof(IPowerShellModuleClient.PreviewExportInstanceAsync),
                nameof(IPowerShellModuleClient.PreviewImportInstanceAsync),
                nameof(IPowerShellModuleClient.PreviewMoveInstanceAsync),
                nameof(IPowerShellModuleClient.PreviewRemoveInstanceAsync),
                nameof(IPowerShellModuleClient.PreviewRenameInstanceAsync),
                nameof(IPowerShellModuleClient.PreviewRecoveryPointNotesAsync),
                nameof(IPowerShellModuleClient.ProbeNetworkAsync),
                nameof(IPowerShellModuleClient.RefreshCatalogAsync),
                nameof(IPowerShellModuleClient.RemoveCatalogSourceAsync),
                nameof(IPowerShellModuleClient.RemoveBackupScheduleAsync),
                nameof(IPowerShellModuleClient.RemoveFirewallRuleAsync),
                nameof(IPowerShellModuleClient.RemoveInstanceTagAsync),
                nameof(IPowerShellModuleClient.RemoveRecoveryPointAsync),
                nameof(IPowerShellModuleClient.RepairHealthAsync),
                nameof(IPowerShellModuleClient.ResolveInstallSourceAsync),
                nameof(IPowerShellModuleClient.RenameInstanceTagsAsync),
                nameof(IPowerShellModuleClient.ReorderCatalogSourcesAsync),
                nameof(IPowerShellModuleClient.ResetCatalogSourcesAsync),
                nameof(IPowerShellModuleClient.ResetSettingsAsync),
                nameof(IPowerShellModuleClient.RevealWslgApplicationAsync),
                nameof(IPowerShellModuleClient.RestoreRecoveryPointAsync),
                nameof(IPowerShellModuleClient.SaveBackupScheduleAsync),
                nameof(IPowerShellModuleClient.SaveSettingsAsync),
                nameof(IPowerShellModuleClient.SearchPackagesAsync),
                nameof(IPowerShellModuleClient.ScanHealthAsync),
                nameof(IPowerShellModuleClient.SetCatalogSourceActiveAsync),
                nameof(IPowerShellModuleClient.SetCredentialAsync),
                nameof(IPowerShellModuleClient.SetDockerIntegrationAsync),
                nameof(IPowerShellModuleClient.SetInstanceSparseModeAsync),
                nameof(IPowerShellModuleClient.SetGlobalConfigurationAsync),
                nameof(IPowerShellModuleClient.SetInstanceTagsAsync),
                nameof(IPowerShellModuleClient.SetNetworkModeAsync),
                nameof(IPowerShellModuleClient.SetNetworkSettingsAsync),
                nameof(IPowerShellModuleClient.SetRecoveryRetentionAsync),
                nameof(IPowerShellModuleClient.SetWslgApplicationPinAsync),
                nameof(IPowerShellModuleClient.StartInstanceAsync),
                nameof(IPowerShellModuleClient.StartInstanceWithResultAsync),
                nameof(IPowerShellModuleClient.StartPackageDownloadJobAsync),
                nameof(IPowerShellModuleClient.StartTemplateApplyAsync),
                nameof(IPowerShellModuleClient.StartTerminalAsync),
                nameof(IPowerShellModuleClient.InstallVerifiedInstanceAsync),
                nameof(IPowerShellModuleClient.InstallVerifiedInstanceWithTargetAsync),
                nameof(IPowerShellModuleClient.PreviewInstallTargetAsync),
                nameof(IPowerShellModuleClient.PreviewInstanceConfigurationAsync),
                nameof(IPowerShellModuleClient.SaveInstanceConfigurationAsync),
                nameof(IPowerShellModuleClient.StopInstanceAsync),
                nameof(IPowerShellModuleClient.TestCatalogSourceAsync),
                nameof(IPowerShellModuleClient.UpdateCatalogSourceAsync),
                nameof(IPowerShellModuleClient.GetWorkspacesAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceSaveAsync),
                nameof(IPowerShellModuleClient.SaveWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceDuplicateAsync),
                nameof(IPowerShellModuleClient.DuplicateWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceRemoveAsync),
                nameof(IPowerShellModuleClient.RemoveWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceImportAsync),
                nameof(IPowerShellModuleClient.ImportWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceExportAsync),
                nameof(IPowerShellModuleClient.ExportWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceTrustAsync),
                nameof(IPowerShellModuleClient.ApproveWorkspaceTrustAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceLaunchAsync),
                nameof(IPowerShellModuleClient.LaunchWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceRetryAsync),
                nameof(IPowerShellModuleClient.RetryWorkspaceAsync),
                nameof(IPowerShellModuleClient.PreviewWorkspaceCloseAsync),
                nameof(IPowerShellModuleClient.CloseWorkspaceAsync),
                nameof(IPowerShellModuleClient.GetWorkspaceOperationStatusAsync),
                nameof(IPowerShellModuleClient.StopWorkspaceOperationAsync),
                nameof(IPowerShellModuleClient.GetTemplatesAsync),
                nameof(IPowerShellModuleClient.GetTemplateAsync),
                nameof(IPowerShellModuleClient.GetTemplateOptionsAsync),
                nameof(IPowerShellModuleClient.GetTemplateApplyOperationStatusAsync),
                nameof(IPowerShellModuleClient.TestTemplateCompatibilityAsync),
                nameof(IPowerShellModuleClient.PreviewTemplateImportAsync),
                nameof(IPowerShellModuleClient.PreviewTemplateApplyAsync),
                nameof(IPowerShellModuleClient.ImportTemplateAsync),
                nameof(IPowerShellModuleClient.PreviewTemplateExportAsync),
                nameof(IPowerShellModuleClient.ExportTemplateAsync),
                nameof(IPowerShellModuleClient.PreviewTemplateRemoveAsync),
                nameof(IPowerShellModuleClient.RemoveTemplateAsync),
                nameof(IPowerShellModuleClient.GetTemplateSourcesAsync),
                nameof(IPowerShellModuleClient.GetTemplateMarketplaceEntriesAsync),
                nameof(IPowerShellModuleClient.GetTemplateMarketplaceStatusAsync),
                nameof(IPowerShellModuleClient.AddTemplateSourceAsync),
                nameof(IPowerShellModuleClient.SetTemplateSourceEnabledAsync),
                nameof(IPowerShellModuleClient.RemoveTemplateSourceAsync),
                nameof(IPowerShellModuleClient.ReviewTemplateMarketplaceCandidateAsync),
                nameof(IPowerShellModuleClient.ApproveTemplateMarketplaceCandidateAsync),
                nameof(IPowerShellModuleClient.DownloadTemplateMarketplaceArtifactAsync),
                nameof(IPowerShellModuleClient.GetTemplateMarketplaceHistoryAsync),
                nameof(IPowerShellModuleClient.RollbackTemplateMarketplaceArtifactAsync),
                nameof(IPowerShellModuleClient.VerifyRecoveryPointAsync)
            }.Order(),
            methods.Select(method => method.Name).Order());
        Assert.DoesNotContain(methods, method => method.Name.Contains("Script", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.Name.Contains("Command", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.Name.Equals("ExecuteOperationAsync", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InstanceConfigurationClient_RejectsUnknownAndOversizedClosedResults()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.SetupSequence(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusInstanceConfiguration", It.IsAny<Dictionary<string, object>>(), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Name\":\"Ubuntu\",\"SchemaRevision\":1,\"Document\":{},\"Fingerprint\":\"" + new string('a', 64) + "\",\"OutcomeCode\":\"Instance.ConfigRead\",\"Unexpected\":true}" })
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Name\":\"Ubuntu\",\"SchemaRevision\":1,\"Document\":{},\"Fingerprint\":\"" + new string('a', 64) + "\",\"OutcomeCode\":\"" + new string('x', 129) + "\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        await Assert.ThrowsAsync<JsonException>(() => client.GetInstanceConfigurationAsync("Ubuntu"));
        await Assert.ThrowsAsync<JsonException>(() => client.GetInstanceConfigurationAsync("Ubuntu"));
    }

    [Fact]
    public async Task TemplateClient_UsesOnlyTypedFixedCommandsAndSafeParameters()
    {
        const string digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusTemplate", It.Is<Dictionary<string, object>>(p => (bool)p["ForceRefresh"] && (string)p["Query"] == "dev" && (string)p["Category"] == "Development"), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Templates\":[{\"Id\":\"demo\",\"Name\":\"Demo\",\"Description\":\"safe\",\"Category\":\"Dev\",\"Version\":\"1\",\"Author\":\"test\",\"Tags\":[],\"CompatibleDistros\":[],\"EstimatedDurationMinutes\":0,\"EstimatedDiskSpaceMB\":0,\"IsOfficial\":true,\"IsCustom\":false,\"TrustState\":\"BuiltIn\",\"Capabilities\":[]}]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusTemplateMarketplaceStatus", It.Is<Dictionary<string, object>>(p => (string)p["SourceId"] == "source" && (string)p["TemplateId"] == "demo" && (string)p["ManifestDigest"] == digest), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"SourceId\":\"source\",\"TemplateId\":\"demo\",\"ManifestDigest\":\"" + digest + "\",\"SignatureStatus\":\"Verified\",\"TrustState\":\"Trusted\",\"HasEffectiveReviewAuthorization\":true,\"CanExecute\":true,\"Reason\":\"Marketplace.Ready\"}" });

        var client = new PowerShellModuleClient(powerShell.Object);
        var template = Assert.Single(await client.GetTemplatesAsync(forceRefresh: true, query: "dev", category: "Development"));
        var status = await client.GetTemplateMarketplaceStatusAsync("source", "demo", digest);

        Assert.Equal("demo", template.Id);
        Assert.True(status.CanExecute);
        powerShell.VerifyAll();
    }

    [Fact]
    public async Task CompactInstanceAsync_UsesOnlyTheFixedModuleCmdletAndName()
    {
        var ps = new Mock<IPowerShellService>(MockBehavior.Strict);
        ps.Setup(service => service.ExecuteModuleCmdletAsync(
                "Compress-DistroNexusInstance",
                It.Is<Dictionary<string, object>>(parameters => parameters.Count == 1 && (string)parameters["Name"] == "Ubuntu"),
                It.Is<ModuleCallOptions>(options => options.ParseAsJson),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"succeeded\":true,\"instanceName\":\"Ubuntu\",\"outcomeCode\":\"Lifecycle.Compacted\",\"beforeBytes\":100,\"afterBytes\":50,\"savedBytes\":50,\"method\":\"Diskpart\",\"restarted\":false}" });

        var result = await new PowerShellModuleClient(ps.Object).CompactInstanceAsync("Ubuntu");

        Assert.True(result.Succeeded);
        Assert.Equal(50, result.SavedBytes);
        ps.VerifyAll();
    }

    [Fact]
    public async Task GlobalConfigurationClient_UsesExactModeledCmdletsAndNeverForwardsRawAuthority()
    {
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusGlobalConfiguration", It.Is<Dictionary<string, object>>(p => p.Count == 0), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Values\":{},\"SupportedFields\":[],\"Capabilities\":[],\"DisplayPreview\":\"\",\"PendingRestart\":false,\"HostRamMb\":1,\"HostCpuCount\":1}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusGlobalConfigurationPreview", It.Is<Dictionary<string, object>>(p => p.Count == 1 && p.ContainsKey("Changes") && !p.ContainsKey("Fingerprint") && !p.ContainsKey("Path")), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Changes\":{\"wsl2.memory\":\"4GB\"},\"ChangedSettings\":[\"wsl2.memory\"],\"DisplayPreview\":\"\",\"PendingRestart\":true,\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Set-DistroNexusGlobalConfiguration", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["PreviewToken"] == new string('a', 32)), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"ChangedSettings\":[\"wsl2.memory\"],\"PendingRestart\":true}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        Assert.Equal(1, (await client.GetGlobalConfigurationAsync()).HostCpuCount);
        Assert.Equal(new string('a', 32), (await client.GetGlobalConfigurationPreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "4GB" })).PreviewToken);
        Assert.True((await client.SetGlobalConfigurationAsync(new string('a', 32))).PendingRestart);
        var invalidPowerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        var invalidClient = new PowerShellModuleClient(invalidPowerShell.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => invalidClient.GetGlobalConfigurationPreviewAsync(new Dictionary<string, string?> { ["raw.document"] = "x" }));
        await Assert.ThrowsAsync<ArgumentException>(() => invalidClient.SetGlobalConfigurationAsync("bad"));
        invalidPowerShell.VerifyNoOtherCalls();
        powerShell.VerifyAll();
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
    public async Task RecoveryMetadataClient_PreviewsCanonicalNotesAndExecutesOnlyTheToken()
    {
        var id = Guid.NewGuid();
        var token = new string('a', 32);
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusRecoveryPointMetadataPreview",
                It.Is<Dictionary<string, object>>(p => p.Count == 4 && (Guid)p["Id"] == id && (string)p["Description"] == "note" && ((string[])p["Tag"]).Length == 1 && ((string[])p["Tag"])[0] == "safe" && (bool)p["Pinned"]),
                It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = $$"""{"Token":"{{token}}","Operation":"Notes","RecoveryPointId":"{{id}}","SourceInstance":"Ubuntu","TargetInstance":"","TargetDirectory":"","Format":"Tar","RequiresStop":false,"ImportInPlace":false,"Warnings":[],"EstimatedBytes":1}""" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync(
                "Set-DistroNexusRecoveryPointMetadata",
                It.Is<Dictionary<string, object>>(p => p.Count == 2 && HasOnlyRecoveryToken(p["Preview"], token) && p.ContainsKey("Confirm") && !(bool)p["Confirm"]),
                It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{}" });

        var client = new PowerShellModuleClient(powerShell.Object);
        var preview = await client.PreviewRecoveryPointNotesAsync(id, "note", ["safe"], true);
        await client.ExecuteRecoveryPointNotesAsync(preview.Token);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteRecoveryPointNotesAsync("forged"));
        powerShell.VerifyAll();
    }

    private static bool HasOnlyRecoveryToken(object value, string token) =>
        value.GetType().GetProperties() is var properties
        && properties.Length == 1
        && properties[0].Name == "Token"
        && properties[0].GetValue(value) as string == token;

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
    public async Task InstanceResourceOperations_UseFixedCmdletsAndTokenOnlyMutation()
    {
        var token = new string('a', 64);
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusInstanceResources", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["Name"] == "Ubuntu"), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Name\":\"Ubuntu\",\"WslVersion\":2,\"SparseMode\":false}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusInstanceSparsePreview", It.Is<Dictionary<string, object>>(p => p.Count == 2 && (string)p["Name"] == "Ubuntu" && (bool)p["Enabled"]), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"PreviewToken\":\"" + token + "\",\"Name\":\"Ubuntu\",\"Enabled\":true,\"ExpiresAt\":\"2026-07-28T00:02:00Z\",\"Effects\":[\"set\"]}" });
        powerShell.Setup(x => x.ExecuteModuleCmdletAsync("Set-DistroNexusInstanceSparseMode", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (string)p["PreviewToken"] == token), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Succeeded\":true,\"OutcomeCode\":\"Succeeded\"}" });
        var client = new PowerShellModuleClient(powerShell.Object);
        Assert.False((await client.GetInstanceResourcesAsync("Ubuntu")).SparseMode);
        Assert.Equal(token, (await client.GetInstanceSparsePreviewAsync("Ubuntu", true)).PreviewToken);
        Assert.True((await client.SetInstanceSparseModeAsync(token)).Succeeded);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SetInstanceSparseModeAsync("bad"));
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

    [Fact]
    public async Task GetBootstrapSettingsAsync_UsesFixedCommandAndRejectsUnknownFields()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusBootstrapSettings", null, It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"Settings\":{},\"ModuleState\":\"Ready\"}" });
        var result = await new PowerShellModuleClient(service.Object).GetBootstrapSettingsAsync();
        Assert.Equal("Ready", result.ModuleState);
        service.Verify(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusBootstrapSettings", null, It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUpdateStatusAsync_RejectsUnknownFieldsAndInvalidReleaseUri()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusUpdateStatus", It.IsAny<Dictionary<string, object>>(), It.IsAny<ModuleCallOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"CurrentVersion\":\"1.0.0\",\"LatestVersion\":\"1.0.1\",\"IsUpdateAvailable\":true,\"ReleaseNotes\":\"ok\",\"ReleaseUri\":\"https://example.test/\",\"ReleasedAt\":null,\"IsPreRelease\":false,\"OutcomeCode\":\"Ready\",\"Unexpected\":true}" });
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new PowerShellModuleClient(service.Object).GetUpdateStatusAsync());
    }

    [Fact]
    public async Task GetUpdateStatusAsync_ForwardsEachPrereleaseChoiceToTheFixedModuleCmdlet()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusUpdateStatus",
                It.Is<Dictionary<string, object>>(parameters => parameters.Count == 1 && parameters["IncludePrerelease"].Equals(false)),
                It.Is<ModuleCallOptions>(options => options.ParseAsJson),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"CurrentVersion\":\"1.0.0\",\"LatestVersion\":null,\"IsUpdateAvailable\":false,\"ReleaseNotes\":null,\"ReleaseUri\":null,\"ReleasedAt\":null,\"IsPreRelease\":false,\"OutcomeCode\":\"Unavailable\"}" });
        service.Setup(x => x.ExecuteModuleCmdletAsync(
                "Get-DistroNexusUpdateStatus",
                It.Is<Dictionary<string, object>>(parameters => parameters.Count == 1 && parameters["IncludePrerelease"].Equals(true)),
                It.Is<ModuleCallOptions>(options => options.ParseAsJson),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "{\"CurrentVersion\":\"1.0.0\",\"LatestVersion\":\"1.1.0-alpha.10\",\"IsUpdateAvailable\":true,\"ReleaseNotes\":null,\"ReleaseUri\":null,\"ReleasedAt\":null,\"IsPreRelease\":true,\"OutcomeCode\":\"Ready\"}" });

        var client = new PowerShellModuleClient(service.Object);

        Assert.False((await client.GetUpdateStatusAsync(false)).IsPreRelease);
        Assert.True((await client.GetUpdateStatusAsync(true)).IsPreRelease);
        service.VerifyAll();
    }

    [Fact]
    public async Task PackageDownloadJobResponses_RejectUnknownFieldsAndInvalidState()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPackageDownloadJob", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[{\"JobId\":\"" + new string('a', 32) + "\",\"PackageId\":\"ubuntu\",\"PackageLabel\":\"Ubuntu\",\"State\":\"Downloading\",\"ProgressPercent\":101,\"OutcomeCode\":\"Package.Queued\",\"Unexpected\":true}]" });
        await Assert.ThrowsAsync<JsonException>(() => new PowerShellModuleClient(service.Object).GetPackageDownloadJobsAsync());
    }

    [Fact]
    public async Task PackageDownloadJobList_RejectsMoreThanTwoHundredItemsBeforeMaterialization()
    {
        var service = new Mock<IPowerShellService>(MockBehavior.Strict);
        var job = "{\"JobId\":\"" + new string('a', 32) + "\",\"PackageId\":\"ubuntu\",\"PackageLabel\":\"Ubuntu\",\"State\":\"Completed\",\"ProgressPercent\":100,\"OutcomeCode\":\"Package.Completed\"}";
        service.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusPackageDownloadJob", null, It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Output = "[" + string.Join(',', Enumerable.Repeat(job, 201)) + "]" });

        await Assert.ThrowsAsync<JsonException>(() => new PowerShellModuleClient(service.Object).GetPackageDownloadJobsAsync());
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
