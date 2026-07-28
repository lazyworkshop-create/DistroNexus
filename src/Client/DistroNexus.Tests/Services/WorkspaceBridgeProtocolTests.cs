using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Core.Interfaces;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class WorkspaceBridgeProtocolTests
{
    [Fact]
    public void FixedLoopbackHandler_LaunchesOnlyTheExactValidatedHttpUri()
    {
        ProcessStartInfo? captured = null;
        var prior = DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Launch; DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Launch = info => captured = info;
        var result = DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Open("::1", 8080); DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Launch = prior;
        Assert.True(result.Succeeded); Assert.Equal("http://[::1]:8080/", captured!.FileName); Assert.True(captured.UseShellExecute);
    }
    [Theory]
    [InlineData("10.0.0.4", 80)]
    [InlineData("localhost", 0)]
    [InlineData("localhost", 65536)]
    public void FixedLoopbackHandler_RejectsInvalidTargetsWithoutLaunching(string host, int port)
    {
        var launches = 0;
        var prior = DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Launch; DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Launch = _ => launches++;
        Assert.Throws<ArgumentException>(() => DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Open(host, port)); DistroNexus.WorkspaceBridge.FixedLoopbackBrowserHandler.Launch = prior;
        Assert.Equal(0, launches);
    }
    [Fact]
    public async Task WorkspaceV1Routes_UseClosedPayloadsAndSingleUseDurableTokens()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var empty = await bridge.SendAsync("workspace.list.v1");
        Assert.True(empty.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(0, empty.GetProperty("Value").GetArrayLength());

        var definition = JsonDocument.Parse("""
            {"Id":"b2acbf0d-27d7-496e-937c-612ec37ac5ee","DisplayName":"Bridge workspace","InstanceName":"Ubuntu","PreflightChecks":[],"ActionGroups":[],"ClosePolicy":{"Mode":"None","ServiceNames":[]},"TrustState":"Untrusted"}
            """).RootElement.Clone();
        var preview = await bridge.SendAsync("workspace.save.preview.v1", payload: JsonSerializer.SerializeToElement(new { Definition = definition, ExpectedRevision = 0 }));
        Assert.True(preview.GetProperty("Succeeded").GetBoolean());
        var token = preview.GetProperty("Value").GetProperty("PreviewToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var malformed = await bridge.SendAsync("workspace.save.execute.v1", payload: definition);
        Assert.False(malformed.GetProperty("Succeeded").GetBoolean());
        var imported = await bridge.SendAsync("workspace.save.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = token }));
        Assert.True(imported.GetProperty("Succeeded").GetBoolean());
        var saved = imported.GetProperty("Value");
        var id = saved.GetProperty("Id").GetGuid();
        Assert.Equal(1, saved.GetProperty("Revision").GetInt64());
        Assert.Equal("Untrusted", saved.GetProperty("TrustState").GetString());
        var replay = await bridge.SendAsync("workspace.save.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = token }));
        Assert.False(replay.GetProperty("Succeeded").GetBoolean());

        var trustPreview = await bridge.SendAsync("workspace.trust.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id, ExpectedRevision = 1 }));
        var approved = await bridge.SendAsync("workspace.trust.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = trustPreview.GetProperty("Value").GetProperty("PreviewToken").GetString() }));
        Assert.True(approved.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Trusted", approved.GetProperty("Value").GetProperty("TrustState").GetString());
        Assert.Equal(2, approved.GetProperty("Value").GetProperty("Revision").GetInt64());

        var conflict = await bridge.SendAsync("workspace.remove.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id, ExpectedRevision = 1 }));
        Assert.False(conflict.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.ConflictOrState", conflict.GetProperty("ErrorCode").GetString());

        var removePreview = await bridge.SendAsync("workspace.remove.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id, ExpectedRevision = 2 }));
        var removed = await bridge.SendAsync("workspace.remove.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = removePreview.GetProperty("Value").GetProperty("PreviewToken").GetString() }));
        Assert.True(removed.GetProperty("Succeeded").GetBoolean());
        var finalList = await bridge.SendAsync("workspace.list.v1");
        Assert.Equal(0, finalList.GetProperty("Value").GetArrayLength());
    }

    [Fact]
    public async Task UnsupportedExecutionOperation_IsSafelyRejected()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync("launch");
        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task DurableOperationStore_CancelsAndRecoversAnInterruptedWorker()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-operation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WorkspaceOperationStore(root);
            var operationId = new string('a', 64);
            await store.CreateAsync(new WorkspaceOperationRecord(operationId, WorkspaceOperationStore.CurrentSid(), "launch", Guid.NewGuid(), null, 1, [], false, false));
            Assert.True(await store.RequestCancelAsync(operationId));
            var recovered = await store.RecoverAsync(operationId);
            Assert.True(recovered.CancellationRequested);
            Assert.True(recovered.IsTerminal);
            Assert.Equal("Workspace.WorkerInterrupted", recovered.ErrorCode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DurableOperationStore_PreservesCancelWhenWorkerPublishesProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-operation-race-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new WorkspaceOperationStore(root); var id = new string('b', 64);
            var record = new WorkspaceOperationRecord(id, WorkspaceOperationStore.CurrentSid(), "launch", Guid.NewGuid(), null, 1, [], false, false);
            await store.CreateAsync(record);
            var workerSnapshot = await store.ReadAsync(id);
            await Task.WhenAll(store.RequestCancelAsync(id), store.WriteAsync(workerSnapshot with { Progress = [new WorkspaceActionResult(Guid.NewGuid(), WorkspaceActionOutcome.Succeeded, "ok")] }));
            Assert.True((await store.ReadAsync(id)).CancellationRequested);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PackagedWorker_HandsOperationToBridgeHostComposition()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-operation-host-" + Guid.NewGuid().ToString("N"));
        try
        {
            var id = new string('c', 64); var store = new WorkspaceOperationStore(Path.Combine(root, "workspace-operations"));
            await store.CreateAsync(new WorkspaceOperationRecord(id, WorkspaceOperationStore.CurrentSid(), "launch", Guid.NewGuid(), null, 1, [], false, false));
            var worker = FindPackagedWorkerHost();
            Assert.True(File.Exists(worker), $"The packaged worker host was not found: {worker}");
            using var process = Process.Start(new ProcessStartInfo(worker) { UseShellExecute = false, Environment = { ["DISTRONEXUS_WORKSPACE_STORE_ROOT"] = root }, ArgumentList = { id } })!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            var terminal = await store.ReadAsync(id);
            Assert.True(terminal.IsTerminal);
            Assert.NotEqual("Workspace.WorkerCompositionUnavailable", terminal.ErrorCode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void WorkerIdentity_RejectsVersionMismatch()
    {
        var candidate = new AssemblyName("DistroNexus.WorkspaceBridge") { Version = new Version(1, 0, 0, 0) };
        Assert.Throws<InvalidOperationException>(() => WorkspaceWorkerIdentity.EnsureApprovedBridge(candidate, new Version(1, 0, 0, 1)));
    }

    [Fact]
    public void WorkerIdentity_RejectsWrongWorkerAssembly()
    {
        var candidate = new AssemblyName("DistroNexus.WorkspaceBridge") { Version = new Version(1, 0, 0, 0) };
        Assert.Throws<InvalidOperationException>(() => WorkspaceWorkerIdentity.EnsureApprovedWorker(candidate, new Version(1, 0, 0, 0)));
    }

    [Fact]
    public async Task DiagnosticExport_RejectsInvalidPayloadAndSanitizesTokenFailures()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var invalid = await bridge.SendAsync("diagnostics.export.v1", payload: JsonDocument.Parse("{\"DestinationFileName\":\"report.json\",\"Script\":\"x\"}").RootElement.Clone(), token: "C:\\Users\\alice\\private-token");
        var stale = await bridge.SendAsync("diagnostics.export.v1", payload: JsonDocument.Parse("{\"DestinationFileName\":\"report.json\"}").RootElement.Clone(), token: "C:\\Users\\alice\\private-token");

        Assert.False(invalid.GetProperty("Succeeded").GetBoolean());
        Assert.False(stale.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Diagnostic.ExportInvalid", stale.GetProperty("ErrorCode").GetString());
        Assert.DoesNotContain("alice", stale.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-token", stale.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GlobalConfigurationRoutes_RejectUnexpectedPayloadsAndForgedOrDriftedTokens()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var getWithPayload = await bridge.SendAsync("configuration.global.get.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var previewUnknown = await bridge.SendAsync("configuration.global.preview.v1", payload: JsonDocument.Parse("{\"Changes\":{\"wsl2.memory\":\"4GB\"},\"Fingerprint\":\"forged\"}").RootElement.Clone());
        var previewRawKey = await bridge.SendAsync("configuration.global.preview.v1", payload: JsonDocument.Parse("{\"Changes\":{\"custom.path\":\"C:\\\\secret\"}}").RootElement.Clone());
        var executeForged = await bridge.SendAsync("configuration.global.execute.v1", payload: JsonDocument.Parse("{\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}").RootElement.Clone());

        foreach (var response in new[] { getWithPayload, previewUnknown, previewRawKey })
        {
            Assert.False(response.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
            Assert.DoesNotContain("secret", response.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        Assert.False(executeForged.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.ConflictOrState", executeForged.GetProperty("ErrorCode").GetString());
        Assert.DoesNotContain("secret", executeForged.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiagnosticPreview_DeadlineCancelsTheFixedRequestBeforeExportCanBeAuthorized()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var cancelled = await bridge.SendAsync("diagnostics.preview.v1", payload: JsonDocument.Parse("{\"Format\":\"Json\",\"DeadlineMilliseconds\":1}").RootElement.Clone());

        Assert.False(cancelled.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Diagnostic.ExportInvalid", cancelled.GetProperty("ErrorCode").GetString());
        Assert.DoesNotContain("C:\\Users", cancelled.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstanceList_UsesItsFixedVersionedBridgeRouteAndReturnsAProtocolFrame()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var response = await bridge.SendAsync("instance.list.v1");

        Assert.True(response.TryGetProperty("Succeeded", out _));
        if (response.GetProperty("Succeeded").GetBoolean())
            Assert.Equal(JsonValueKind.Array, response.GetProperty("Value").ValueKind);
    }

    [Theory]
    [InlineData("instance.start.v1")]
    [InlineData("instance.stop.v1")]
    public async Task InstanceMutation_RejectsMissingOrInvalidPayloadBeforeHostInvocation(string operation)
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var missing = await bridge.SendAsync(operation);
        var empty = await bridge.SendAsync(operation, payload: JsonDocument.Parse("{\"Name\":\"\"}").RootElement.Clone());

        Assert.False(missing.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", missing.GetProperty("ErrorCode").GetString());
        Assert.False(empty.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", empty.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task InstanceLifecycleRoutes_RejectUnknownPayloadFields()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var list = await bridge.SendAsync("instance.list.v1", payload: JsonDocument.Parse("{\"ForceRefresh\":true,\"Command\":\"wsl.exe\"}").RootElement.Clone());
        var start = await bridge.SendAsync("instance.start.v1", payload: JsonDocument.Parse("{\"Name\":\"Ubuntu\",\"Arguments\":[\"--exec\"]}").RootElement.Clone());
        var stop = await bridge.SendAsync("instance.stop.v1", payload: JsonDocument.Parse("{\"Name\":\"Ubuntu\",\"Force\":true}").RootElement.Clone());

        foreach (var response in new[] { list, start, stop })
        {
            Assert.False(response.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
        }
    }

    [Theory]
    [InlineData("instance.compact.preview.v1", "{\"Name\":\"Ubuntu\",\"Method\":\"diskpart\"}")]
    [InlineData("instance.compact.execute.v1", "{\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Path\":\"C:\\\\unsafe\"}")]
    public async Task InstanceCompactionRoutes_RejectFieldsOtherThanNameOrPreviewToken(string operation, string json)
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync(operation, payload: JsonDocument.Parse(json).RootElement.Clone());
        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
    }

    [Theory]
    [InlineData("install.source.resolve.v1", "{\"PackageId\":\"ubuntu\",\"Path\":\"C:\\\\unsafe\"}")]
    [InlineData("package.acquire.preview.v1", "{\"PackageId\":\"ubuntu\",\"Url\":\"https://unsafe.test\"}")]
    [InlineData("package.acquire.execute.v1", "{\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Command\":\"cmd\"}")]
    [InlineData("instance.install.preview.v1", "{\"PackageReference\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Name\":\"Ubuntu\",\"InstallRoot\":\"D:\\\\WSL\",\"Username\":\"developer\",\"Shell\":\"bash\",\"SetAsDefault\":false,\"Command\":\"cmd\"}")]
    [InlineData("instance.install.execute.v1", "{\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"PackagePath\":\"C:\\\\unsafe\"}")]
    public async Task VerifiedInstallRoutes_RejectUnknownPayloadFields(string operation, string json)
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync(operation, payload: JsonDocument.Parse(json).RootElement.Clone());
        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
    }

    [Theory]
    [InlineData("install.source.resolve.v1", "{\"PackageId\":\"\"}", "Lifecycle.AcquisitionInvalid", "")]
    [InlineData("package.acquire.preview.v1", "{\"PackageId\":\"\"}", "Lifecycle.AcquisitionInvalid", "")]
    [InlineData("package.acquire.execute.v1", "{\"PreviewToken\":\"gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg\"}", "Lifecycle.GrantInvalid", "")]
    [InlineData("instance.install.preview.v1", "{\"PackageReference\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Name\":\"bad\\nname\",\"InstallRoot\":\"C:\\\\private\\\\secret-root\",\"Username\":\"developer\",\"Shell\":\"bash\",\"SetAsDefault\":false}", "Workspace.Bridge.Invalid", "C:\\private\\secret-root")]
    [InlineData("instance.install.execute.v1", "{\"PreviewToken\":\"gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg\"}", "Lifecycle.GrantInvalid", "")]
    public async Task VerifiedInstallRoutes_MapFailuresToStableSanitizedOutcomes(string operation, string json, string outcome, string forbidden)
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync(operation, payload: JsonDocument.Parse(json).RootElement.Clone());

        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(outcome, response.GetProperty("ErrorCode").GetString());
        Assert.Equal(outcome, response.GetProperty("ErrorMessage").GetString());
        Assert.DoesNotContain("private", response.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-root", response.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", response.GetRawText(), StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(forbidden)) Assert.DoesNotContain(forbidden, response.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CredentialPreviewRoute_PreservesStableSanitizedCredentialInvalidOutcome()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync("instance.credential.preview.v1", payload: JsonDocument.Parse("{\"Name\":\"Ubuntu\",\"Username\":\"developer\",\"SecretEnvelope\":\"plaintext-secret\"}").RootElement.Clone());

        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Lifecycle.CredentialInvalid", response.GetProperty("ErrorCode").GetString());
        Assert.Equal("Lifecycle.CredentialInvalid", response.GetProperty("ErrorMessage").GetString());
        Assert.DoesNotContain("plaintext-secret", response.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CapabilityV1Routes_RejectPayloadsOutsideTheirFixedContractsBeforeProbe()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var hostWithPayload = await bridge.SendAsync("capability.host.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var missingInstance = await bridge.SendAsync("capability.instance.v1");
        var unknownField = await bridge.SendAsync("capability.instance.v1", payload: JsonDocument.Parse("{\"InstanceName\":\"Ubuntu\",\"Script\":\"Get-ChildItem\"}").RootElement.Clone());
        var invalidName = await bridge.SendAsync("capability.instance.v1", payload: JsonDocument.Parse("{\"InstanceName\":\"bad\\nname\"}").RootElement.Clone());
        var unknownOperation = await bridge.SendAsync("capability.host.v2");

        foreach (var response in new[] { hostWithPayload, missingInstance, unknownField, invalidName, unknownOperation })
        {
            Assert.False(response.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
        }
    }

    [Fact]
    public async Task Settings_UseFixedVersionedRoutesAndPersistTheTypedModel()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var initial = await bridge.SendAsync("settings.get.v1");
        Assert.True(initial.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(2, initial.GetProperty("Value").GetProperty("DefaultWslVersion").GetInt32());

        var payload = JsonDocument.Parse("""{"Settings":{"DefaultInstallPath":"D:\\WSL","PackageCachePath":"","TerminalStartPath":"~","DefaultWslVersion":1,"DefaultUsername":"root","DefaultDistributionId":"","EnableLogging":true,"LogPath":"","CheckUpdatesOnStartup":true,"CatalogUrl":"https://example.test/catalog.json","Theme":"Dark","Language":"en-US","ShowConfirmationDialogs":true,"MaxConcurrentDownloads":3,"AutoRetryDownloads":true,"MaxRetryAttempts":3,"AutoSaveEnabled":true,"AutoSaveInterval":30,"CustomData":{},"PowerShellModulePath":null,"LocalhostForwardingHealthEndpoint":""}}""").RootElement.Clone();
        var saved = await bridge.SendAsync("settings.save.v1", payload: payload);
        Assert.True(saved.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("D:\\WSL", saved.GetProperty("Value").GetProperty("DefaultInstallPath").GetString());
        Assert.Equal("Dark", saved.GetProperty("Value").GetProperty("Theme").GetString());

        var reset = await bridge.SendAsync("settings.reset.v1");
        Assert.True(reset.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("C:\\WSL", reset.GetProperty("Value").GetProperty("DefaultInstallPath").GetString());
        Assert.Equal("Auto", reset.GetProperty("Value").GetProperty("Theme").GetString());
    }

    [Fact]
    public async Task Settings_LegacyModulePathIsClearedByActualBridgeReadsAndSuccessfulSaves()
    {
        var store = Path.Combine(Path.GetTempPath(), "DistroNexusBridge-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(store);
        var settingsPath = Path.Combine(store, "settings.json");
        try
        {
            await File.WriteAllTextAsync(settingsPath, """{"PowerShellModulePath":"C:\\legacy-module","Theme":"Dark"}""");
            await using var bridge = await BridgeProcess.StartAsync(store);

            var read = await bridge.SendAsync("settings.get.v1");
            Assert.True(read.GetProperty("Succeeded").GetBoolean());
            Assert.True(read.GetProperty("Value").TryGetProperty("PowerShellModulePath", out var legacyPath));
            Assert.Equal(JsonValueKind.Null, legacyPath.ValueKind);

            var saved = await bridge.SendAsync("settings.save.v1", payload: JsonDocument.Parse("""{"Settings":{"Theme":"Light"}}""").RootElement.Clone());
            Assert.True(saved.GetProperty("Succeeded").GetBoolean());

            var afterSave = await bridge.SendAsync("settings.get.v1");
            Assert.True(afterSave.GetProperty("Succeeded").GetBoolean());
            Assert.True(afterSave.GetProperty("Value").TryGetProperty("PowerShellModulePath", out legacyPath));
            Assert.Equal(JsonValueKind.Null, legacyPath.ValueKind);

            using var persisted = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
            Assert.False(persisted.RootElement.GetProperty("value").TryGetProperty("PowerShellModulePath", out _));
        }
        finally
        {
            if (Directory.Exists(store)) Directory.Delete(store, true);
        }
    }

    [Fact]
    public async Task UpdateStatusRoute_AcceptsBothExplicitPrereleaseValuesAtTheActualBridgeBoundary()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var stable = await bridge.SendAsync("update-status.get.v1", payload: JsonSerializer.SerializeToElement(new { IncludePrerelease = false }));
        var prerelease = await bridge.SendAsync("update-status.get.v1", payload: JsonSerializer.SerializeToElement(new { IncludePrerelease = true }));

        foreach (var response in new[] { stable, prerelease })
        {
            Assert.True(response.GetProperty("Succeeded").GetBoolean());
            Assert.True(response.GetProperty("Value").TryGetProperty("OutcomeCode", out _));
        }
    }

    [Fact]
    public async Task Settings_RoutesRejectPayloadsOutsideTheirFixedTypedContract()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var get = await bridge.SendAsync("settings.get.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var reset = await bridge.SendAsync("settings.reset.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var arbitrary = await bridge.SendAsync("settings.save.v1", payload: JsonDocument.Parse("""{"Settings":{"DefaultInstallPath":"D:\\WSL"},"Script":"Get-ChildItem"}""").RootElement.Clone());

        Assert.False(get.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", get.GetProperty("ErrorCode").GetString());
        Assert.False(reset.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", reset.GetProperty("ErrorCode").GetString());
        Assert.False(arbitrary.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", arbitrary.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task NetworkAndFirewallRoutes_RejectUnexpectedPayloadsAndKeepFixedContracts()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var status = await bridge.SendAsync("network.status.v1");
        var statusWithPayload = await bridge.SendAsync("network.status.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var ports = await bridge.SendAsync("network.port-mappings.v1", payload: JsonDocument.Parse("{\"Name\":\"Ubuntu\",\"Script\":\"Get-ChildItem\"}").RootElement.Clone());
        var firewall = await bridge.SendAsync("firewall.preview-create.v1", payload: JsonDocument.Parse("{\"Request\":{\"Direction\":\"Inbound\",\"Protocol\":\"Tcp\",\"Port\":443,\"Profiles\":[\"Private\"]},\"Grant\":\"forged\"}").RootElement.Clone());
        var listWithPayload = await bridge.SendAsync("firewall.list.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());

        Assert.True(status.TryGetProperty("Succeeded", out _));
        Assert.False(statusWithPayload.GetProperty("Succeeded").GetBoolean());
        Assert.False(ports.GetProperty("Succeeded").GetBoolean());
        Assert.False(firewall.GetProperty("Succeeded").GetBoolean());
        Assert.False(listWithPayload.GetProperty("Succeeded").GetBoolean());
    }

    [Fact]
    public async Task CatalogSource_RoutesCoverEveryManagerOperationWithTypedPayloads()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var defaults = await bridge.SendAsync("catalog-source.defaults.get.v1");
        Assert.True(defaults.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(2, defaults.GetProperty("Value").GetArrayLength());

        var initial = await bridge.SendAsync("catalog-source.list.v1");
        Assert.True(initial.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(0, initial.GetProperty("Value").GetArrayLength());

        var addPayload = JsonDocument.Parse("""{"Name":"Test source","Url":"https://example.test/catalog.json","Description":"Test-only","IsActive":true}""").RootElement.Clone();
        var added = await bridge.SendAsync("catalog-source.add.v1", payload: addPayload);
        Assert.True(added.GetProperty("Succeeded").GetBoolean());
        var sourceId = added.GetProperty("Value").GetProperty("Id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sourceId));

        var updatePayload = JsonDocument.Parse($$"""{"SourceId":"{{sourceId}}","Name":"Updated source","Url":"https://example.test/updated.json","Description":"Updated","IsActive":true}""").RootElement.Clone();
        var updated = await bridge.SendAsync("catalog-source.update.v1", payload: updatePayload);
        Assert.True(updated.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Updated source", updated.GetProperty("Value").GetProperty("Name").GetString());

        var active = await bridge.SendAsync("catalog-source.active.set.v1", payload: JsonDocument.Parse($$"""{"SourceId":"{{sourceId}}","IsActive":false}""").RootElement.Clone());
        Assert.True(active.GetProperty("Succeeded").GetBoolean());
        Assert.True(active.GetProperty("Value").GetBoolean());

        var reordered = await bridge.SendAsync("catalog-source.reorder.v1", payload: JsonDocument.Parse($$"""{"SourceIds":["{{sourceId}}"]}""").RootElement.Clone());
        Assert.True(reordered.GetProperty("Succeeded").GetBoolean());
        Assert.True(reordered.GetProperty("Value").GetBoolean());

        var removed = await bridge.SendAsync("catalog-source.remove.v1", payload: JsonDocument.Parse($$"""{"SourceId":"{{sourceId}}"}""").RootElement.Clone());
        Assert.True(removed.GetProperty("Succeeded").GetBoolean());
        Assert.True(removed.GetProperty("Value").GetBoolean());

        var reset = await bridge.SendAsync("catalog-source.defaults.reset.v1");
        Assert.True(reset.GetProperty("Succeeded").GetBoolean());
        Assert.True(reset.GetProperty("Value").GetBoolean());
    }

    [Fact]
    public async Task CatalogSource_RoutesRejectMalformedOrGenericPayloadsBeforeExecution()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var listWithPayload = await bridge.SendAsync("catalog-source.list.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var malformedAdd = await bridge.SendAsync("catalog-source.add.v1", payload: JsonDocument.Parse("""{"Name":"","Url":"","Script":"Get-ChildItem"}""").RootElement.Clone());
        var incompleteUpdate = await bridge.SendAsync("catalog-source.update.v1", payload: JsonDocument.Parse("""{"SourceId":"source-1","IsActive":true}""").RootElement.Clone());
        var blankUpdate = await bridge.SendAsync("catalog-source.update.v1", payload: JsonDocument.Parse("""{"SourceId":"source-1","Name":"","Url":"","IsActive":true}""").RootElement.Clone());
        var malformedReorder = await bridge.SendAsync("catalog-source.reorder.v1", payload: JsonDocument.Parse("""{"SourceIds":[],"Operation":"settings.save.v1"}""").RootElement.Clone());
        var test = await bridge.SendAsync("catalog-source.test.v1", payload: JsonDocument.Parse("""{"Url":""}""").RootElement.Clone());

        Assert.False(listWithPayload.GetProperty("Succeeded").GetBoolean());
        Assert.False(malformedAdd.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", malformedAdd.GetProperty("ErrorCode").GetString());
        Assert.False(incompleteUpdate.GetProperty("Succeeded").GetBoolean());
        Assert.False(blankUpdate.GetProperty("Succeeded").GetBoolean());
        Assert.False(malformedReorder.GetProperty("Succeeded").GetBoolean());
        Assert.False(test.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", test.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task HealthScan_UsesConcreteCoreChecksForEveryAdvertisedCategory()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync("healthScan");

        Assert.True(response.GetProperty("Succeeded").GetBoolean());
        var findings = response.GetProperty("Value").GetProperty("Findings").EnumerateArray()
            .Select(x => x.GetProperty("Id").GetString() ?? string.Empty).ToArray();
        foreach (var prefix in new[] { "wslconfig.", "backup.", "template", "wslg.", "capability.", "disk.", "monitoring.", "network.", "windows.feature.", "windows.virtualization." })
            Assert.True(findings.Any(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)), $"Missing {prefix}; observed: {string.Join(",", findings)}");
    }

    [Theory]
    [InlineData("open.wsl-update", false, "DN-7004")]
    [InlineData("open.windows-virtualization-settings", false, "DN-7004")]
    [InlineData("enable.windows-features", false, "DN-7004")]
    [InlineData("wsl.update", true, null)]
    [InlineData("wsl.trim", true, null)]
    public async Task HealthRepairPreview_RecognizesEveryHostRepairRouteAndMarksDesktopOnlyActions(string repairId, bool expectedPreview, string? expectedCode)
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            Finding = new { Id = "repair." + repairId, Severity = "Warning", Scope = "Host", Title = "test", Detail = "test", InstanceName = "Ubuntu", RepairId = repairId, Evidence = new { feature = "VirtualMachinePlatform" } }
        })).RootElement.Clone();

        var response = await bridge.SendAsync("healthRepairPreview", payload: payload);

        Assert.True(response.GetProperty("Succeeded").GetBoolean());
        var preview = response.GetProperty("Value");
        Assert.Equal(repairId, preview.GetProperty("RepairId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(preview.GetProperty("PreviewToken").GetString()));
        if (expectedPreview)
            Assert.NotEqual(0, preview.GetProperty("Commands").GetArrayLength());
        else
            Assert.Contains(preview.GetProperty("Preconditions").EnumerateArray().Select(x => x.GetString()), value =>
                value?.Contains(expectedCode!, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task DesktopOnlyHealthRepairExecute_ReturnsStructuredResultWithoutMutation()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var finding = JsonDocument.Parse("""
            {"Id":"repair.open","Severity":"Warning","Scope":"Host","Title":"test","Detail":"test","RepairId":"open.wsl-update"}
            """).RootElement.Clone();
        var previewRequest = JsonDocument.Parse($$"""{"Finding":{{finding.GetRawText()}}}""").RootElement.Clone();
        var preview = await bridge.SendAsync("health.repair-preview.v1", payload: previewRequest);
        var execute = await bridge.SendAsync("health.repair.v1", token: preview.GetProperty("Value").GetProperty("PreviewToken").GetString(),
            payload: JsonDocument.Parse("{}").RootElement.Clone());

        Assert.True(execute.GetProperty("Succeeded").GetBoolean());
        Assert.False(execute.GetProperty("Value").GetProperty("Succeeded").GetBoolean());
        Assert.Contains("Desktop-only", execute.GetProperty("Value").GetProperty("Error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceV1PreviewDoesNotPersistAndExecuteRequiresItsDurableToken()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var definition = JsonDocument.Parse("""
            {"Id":"d2acbf0d-27d7-496e-937c-612ec37ac5ee","DisplayName":"Dry run","InstanceName":"Ubuntu","PreflightChecks":[],"ActionGroups":[],"ClosePolicy":{"Mode":"None","ServiceNames":[]},"TrustState":"Trusted"}
            """).RootElement.Clone();

        var create = await bridge.SendAsync("workspace.save.preview.v1", payload: JsonSerializer.SerializeToElement(new { Definition = definition, ExpectedRevision = 0 }));
        Assert.True(create.GetProperty("Succeeded").GetBoolean());
        var value = create.GetProperty("Value");
        Assert.False(string.IsNullOrWhiteSpace(value.GetProperty("PreviewToken").GetString()));
        Assert.Equal(0, (await bridge.SendAsync("workspace.list.v1")).GetProperty("Value").GetArrayLength());

        var saved = await bridge.SendAsync("workspace.save.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = value.GetProperty("PreviewToken").GetString() }));
        var id = saved.GetProperty("Value").GetProperty("Id").GetGuid();
        var export = await bridge.SendAsync("workspace.export.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id, ExpectedRevision = 1 }));
        Assert.True(export.GetProperty("Succeeded").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(export.GetProperty("Value").GetProperty("PreviewToken").GetString()));
    }

    [Fact]
    public async Task WorkspaceV1ExportRejectsStaleRevisionAndConsumesOnlyPreviewToken()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var definition = JsonDocument.Parse("""
            {"Id":"e2acbf0d-27d7-496e-937c-612ec37ac5ee","DisplayName":"Export revision","InstanceName":"Ubuntu","PreflightChecks":[],"ActionGroups":[],"ClosePolicy":{"Mode":"None","ServiceNames":[]},"TrustState":"Trusted"}
            """).RootElement.Clone();
        var savePreview = await bridge.SendAsync("workspace.save.preview.v1", payload: JsonSerializer.SerializeToElement(new { Definition = definition, ExpectedRevision = 0 }));
        var saved = await bridge.SendAsync("workspace.save.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = savePreview.GetProperty("Value").GetProperty("PreviewToken").GetString() }));
        Assert.True(saved.GetProperty("Succeeded").GetBoolean());
        var id = saved.GetProperty("Value").GetProperty("Id").GetGuid();

        var missing = await bridge.SendAsync("workspace.export.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id }));
        Assert.False(missing.GetProperty("Succeeded").GetBoolean());
        var stale = await bridge.SendAsync("workspace.export.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id, ExpectedRevision = 0 }));
        Assert.False(stale.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.ConflictOrState", stale.GetProperty("ErrorCode").GetString());

        var exportPreview = await bridge.SendAsync("workspace.export.preview.v1", payload: JsonSerializer.SerializeToElement(new { Id = id, ExpectedRevision = 1 }));
        var exported = await bridge.SendAsync("workspace.export.execute.v1", payload: JsonSerializer.SerializeToElement(new { PreviewToken = exportPreview.GetProperty("Value").GetProperty("PreviewToken").GetString() }));
        Assert.True(exported.GetProperty("Succeeded").GetBoolean());
        Assert.Contains("Export revision", exported.GetProperty("Value").GetProperty("Content").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogRoutes_UseFixedPayloadsAndRejectMalformedRequests()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var list = await bridge.SendAsync("catalog.list.v1");
        var family = await bridge.SendAsync("catalog.list.v1", payload: JsonDocument.Parse("{\"Family\":\"Ubuntu\"}").RootElement.Clone());
        var force = await bridge.SendAsync("catalog.list.v1", payload: JsonDocument.Parse("{\"ForceReload\":true}").RootElement.Clone());
        var search = await bridge.SendAsync("catalog.search.v1", payload: JsonDocument.Parse("{\"Query\":\"ubuntu\"}").RootElement.Clone());
        var get = await bridge.SendAsync("catalog.get.v1", payload: JsonDocument.Parse("{\"Id\":\"unknown\"}").RootElement.Clone());
        Assert.True(list.GetProperty("Succeeded").GetBoolean());
        Assert.True(family.GetProperty("Succeeded").GetBoolean());
        Assert.True(force.GetProperty("Succeeded").GetBoolean());
        Assert.True(search.GetProperty("Succeeded").GetBoolean());
        Assert.True(get.GetProperty("Succeeded").GetBoolean());
        foreach (var malformed in new[] {
            await bridge.SendAsync("catalog.search.v1"),
            await bridge.SendAsync("catalog.search.v1", payload: JsonDocument.Parse("{\"Query\":\"\"}").RootElement.Clone()),
            await bridge.SendAsync("catalog.get.v1", payload: JsonDocument.Parse("null").RootElement.Clone()),
            await bridge.SendAsync("catalog.list.v1", payload: JsonDocument.Parse("{\"Family\":\"\",\"Script\":\"x\"}").RootElement.Clone()),
            await bridge.SendAsync("catalog.unknown.v1") })
        {
            Assert.False(malformed.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Workspace.Bridge.Invalid", malformed.GetProperty("ErrorCode").GetString());
        }
    }

    [Fact]
    public async Task PackageCacheRoutes_RejectMalformedDeletePayloadsBeforeMutation()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var location = await bridge.SendAsync("package-cache.location.v1");
        var usage = await bridge.SendAsync("package-cache.usage.v1");
        Assert.True(location.GetProperty("Succeeded").GetBoolean());
        Assert.True(location.GetProperty("Value").TryGetProperty("CachePath", out _));
        Assert.True(usage.GetProperty("Succeeded").GetBoolean());
        foreach (var invalid in new[]
        {
            await bridge.SendAsync("package-cache.delete.v1"),
            await bridge.SendAsync("package-cache.delete.v1", payload: JsonDocument.Parse("{}").RootElement.Clone()),
            await bridge.SendAsync("package-cache.delete.v1", payload: JsonDocument.Parse("{\"CacheEntryId\":\"a\",\"DefaultName\":\"b\"}").RootElement.Clone()),
            await bridge.SendAsync("package-cache.delete.v1", payload: JsonDocument.Parse("{\"CacheEntryId\":\"a\",\"Script\":\"x\"}").RootElement.Clone()),
            await bridge.SendAsync("package-cache.clear.v1", payload: JsonDocument.Parse("{}").RootElement.Clone())
        })
        {
            Assert.False(invalid.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Workspace.Bridge.Invalid", invalid.GetProperty("ErrorCode").GetString());
        }
    }

    [Fact]
    public async Task TerminalAndPackageCacheLaunchRoutes_RejectUntrustedPayloadsBeforeLaunching()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var invalid = new[]
        {
            await bridge.SendAsync("terminal.status.v1", payload: JsonDocument.Parse("{}").RootElement.Clone()),
            await bridge.SendAsync("terminal.launch.v1"),
            await bridge.SendAsync("terminal.launch.v1", payload: JsonDocument.Parse("{\"InstanceName\":\"Ubuntu\",\"StartPath\":\"C:\\\\outside\"}").RootElement.Clone()),
            await bridge.SendAsync("terminal.launch.v1", payload: JsonDocument.Parse("{\"InstanceName\":\"Ubuntu\",\"Program\":\"cmd.exe\"}").RootElement.Clone()),
            await bridge.SendAsync("explorer.package-cache.v1", payload: JsonDocument.Parse("{\"Path\":\"C:\\\\outside\"}").RootElement.Clone())
        };
        foreach (var response in invalid)
        {
            Assert.False(response.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Workspace.Bridge.Invalid", response.GetProperty("ErrorCode").GetString());
        }
    }

    [Fact]
    public void FixedExternalLaunchSpecs_AllowOnlyTerminalAndConfiguredCacheArgumentShapes()
    {
        var windowsTerminal = FixedLaunchProcess.CreateTerminalStartInfo(TerminalKind.WindowsTerminal, "Ubuntu", "/home/user");
        Assert.Equal("wt.exe", windowsTerminal.FileName);
        Assert.False(windowsTerminal.UseShellExecute);
        Assert.Equal(["-w", "0", "new-tab", "--", "wsl.exe", "-d", "Ubuntu", "--cd", "/home/user"], windowsTerminal.ArgumentList);

        var windowsTerminalHome = FixedLaunchProcess.CreateTerminalStartInfo(TerminalKind.WindowsTerminal, "Ubuntu", null);
        Assert.Equal(["-w", "0", "new-tab", "--", "wsl.exe", "-d", "Ubuntu"], windowsTerminalHome.ArgumentList);

        var commandPrompt = FixedLaunchProcess.CreateTerminalStartInfo(TerminalKind.CommandPrompt, "Ubuntu", null);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), commandPrompt.FileName);
        Assert.Equal(["/k", "wsl", "-d", "Ubuntu"], commandPrompt.ArgumentList);

        var cache = FixedLaunchProcess.CreatePackageCacheStartInfo(Path.Combine(Path.GetTempPath(), "DistroNexus-cache"));
        Assert.Equal("explorer.exe", cache.FileName);
        Assert.Equal([Path.Combine(Path.GetTempPath(), "DistroNexus-cache")], cache.ArgumentList);
    }

    [Fact]
    public async Task PackageCacheDelete_MapsForgedAuthorityToStableSanitizedError()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync("package-cache.delete.v1", payload: JsonDocument.Parse("{\"CacheEntryId\":\"forged\"}").RootElement.Clone());
        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("PackageCache.EntryInvalid", response.GetProperty("ErrorCode").GetString());
        Assert.Equal("Package cache entry is invalid.", response.GetProperty("ErrorMessage").GetString());
    }

    [Fact]
    public async Task OperationalVersionedRoutes_MatchLegacyReadAliasesAndRejectUnsupportedVersions()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var legacyRecovery = await bridge.SendAsync("recoveryList");
        var versionedRecovery = await bridge.SendAsync("recovery.list.v1");
        var versionedHistory = await bridge.SendAsync("health.history.v1");
        var unsupported = await bridge.SendAsync("health.scan.v2");

        Assert.True(legacyRecovery.GetProperty("Succeeded").GetBoolean());
        Assert.True(versionedRecovery.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(legacyRecovery.GetProperty("Value").ValueKind, versionedRecovery.GetProperty("Value").ValueKind);
        Assert.True(versionedHistory.GetProperty("Succeeded").GetBoolean());
        Assert.False(unsupported.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", unsupported.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task OperationalRoutes_RejectUnknownPayloadFieldsWithAliasVersionParity()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var invalid = JsonDocument.Parse("""{"Unexpected":true}""").RootElement.Clone();
        var pairs = new[]
        {
            ("systemdList", "systemd.list.v1"), ("systemdPreview", "systemd.preview.v1"), ("systemdExecute", "systemd.execute.v1"),
            ("recoveryList", "recovery.list.v1"), ("recoveryHistory", "recovery.history.v1"), ("recoveryVerify", "recovery.verify.v1"),
            ("recoveryPreviewCreate", "recovery.preview-create.v1"), ("recoveryCreate", "recovery.create.v1"),
            ("recoveryPreviewRestore", "recovery.preview-restore.v1"), ("recoveryRestore", "recovery.restore.v1"),
            ("recoveryPreviewRemove", "recovery.preview-remove.v1"), ("recoveryRemove", "recovery.remove.v1"),
            ("healthScan", "health.scan.v1"), ("healthRepairPreview", "health.repair-preview.v1")
        };
        foreach (var (legacy, versioned) in pairs)
        {
            var legacyResponse = await bridge.SendAsync(legacy, Guid.NewGuid(), invalid, token: "forged");
            var versionedResponse = await bridge.SendAsync(versioned, Guid.NewGuid(), invalid, token: "forged");
            Assert.False(legacyResponse.GetProperty("Succeeded").GetBoolean(), legacy);
            Assert.False(versionedResponse.GetProperty("Succeeded").GetBoolean(), versioned);
            Assert.Equal(legacyResponse.GetProperty("ErrorCode").GetString(), versionedResponse.GetProperty("ErrorCode").GetString());
        }
    }

    [Fact]
    public async Task RecoveryRetention_RequiresCurrentPreviewAndRejectsStaleRequestBeforeMutation()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var payload = JsonDocument.Parse("""{"SourceInstance":"Ubuntu","Maximum":2}""").RootElement.Clone();
        var noPreview = await bridge.SendAsync("recovery.retention.set.v1", payload: payload);
        var preview = await bridge.SendAsync("recovery.retention.preview.v1", payload: payload);
        var stalePayload = JsonDocument.Parse("""{"SourceInstance":"Ubuntu","Maximum":3}""").RootElement.Clone();
        var stale = await bridge.SendAsync("recovery.retention.set.v1", payload: stalePayload, token: preview.GetProperty("Value").GetProperty("Token").GetString());
        var retention = await bridge.SendAsync("recovery.retention.get.v1", payload: JsonDocument.Parse("""{"SourceInstance":"Ubuntu"}""").RootElement.Clone());

        Assert.False(noPreview.GetProperty("Succeeded").GetBoolean());
        Assert.False(stale.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, retention.GetProperty("Value").GetProperty("Maximum").ValueKind);
    }

    [Fact]
    public async Task RecoveryNotesRoutes_AcceptOnlyCanonicalPreviewPayloadAndTokenOnlyExecution()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var legacyMutable = await bridge.SendAsync("recovery.notes.v1", payload: JsonDocument.Parse("""{"Id":"11111111-1111-1111-1111-111111111111","Description":"forged","Tags":["admin"],"Pinned":true}""").RootElement.Clone());
        var previewWithAuthority = await bridge.SendAsync("recovery.notes.preview.v1", payload: JsonDocument.Parse("""{"Id":"11111111-1111-1111-1111-111111111111","Description":"note","Tags":[],"Pinned":false,"Path":"C:\\secret"}""").RootElement.Clone());
        var executeWithNotes = await bridge.SendAsync("recovery.notes.execute.v1", payload: JsonDocument.Parse("""{"PreviewToken":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","Description":"forged"}""").RootElement.Clone());
        var forgedToken = await bridge.SendAsync("recovery.notes.execute.v1", payload: JsonDocument.Parse("""{"PreviewToken":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""").RootElement.Clone());

        Assert.False(legacyMutable.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", legacyMutable.GetProperty("ErrorCode").GetString());
        Assert.Equal(JsonValueKind.Null, legacyMutable.GetProperty("Value").ValueKind);
        Assert.False(previewWithAuthority.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", previewWithAuthority.GetProperty("ErrorCode").GetString());
        Assert.False(executeWithNotes.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", executeWithNotes.GetProperty("ErrorCode").GetString());
        Assert.False(forgedToken.GetProperty("Succeeded").GetBoolean());
        Assert.DoesNotContain("secret", previewWithAuthority.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WslgRoutes_RejectUnknownPayloadsAndReturnRedactedGrantCodes()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var unknown = await bridge.SendAsync("wslg.status.v1", payload: JsonDocument.Parse("""{"InstanceName":"Ubuntu","Unexpected":true}""").RootElement.Clone());
        var forged = await bridge.SendAsync("wslg.launch.v1", payload: JsonDocument.Parse($$"""{"DiscoveryToken":"{{new string('a', 64)}}","ApplicationId":"forged"}""").RootElement.Clone());
        Assert.False(unknown.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.Bridge.Invalid", unknown.GetProperty("ErrorCode").GetString());
        Assert.False(forged.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Wslg.DiscoveryGrantInvalid", forged.GetProperty("ErrorCode").GetString());
        Assert.Equal("Wslg.DiscoveryGrantInvalid", forged.GetProperty("ErrorMessage").GetString());
    }

    [Fact]
    public async Task DockerIntegrationRoutes_RejectUnknownAndMissingPayloadsBeforeExecution()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var unknown = await bridge.SendAsync("docker.integration.get.v1", payload: JsonDocument.Parse("""{"Name":"Ubuntu","Path":"C:\\secret"}""").RootElement.Clone());
        var missing = await bridge.SendAsync("docker.integration.preview-set.v1", payload: JsonDocument.Parse("""{"Name":"Ubuntu"}""").RootElement.Clone());
        var forged = await bridge.SendAsync("docker.integration.set.v1", payload: JsonDocument.Parse("""{"Name":"Ubuntu","Enabled":true}""").RootElement.Clone(), token: new string('a', 64));
        Assert.False(unknown.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("DockerIntegration.Conflict", unknown.GetProperty("ErrorCode").GetString());
        Assert.False(missing.GetProperty("Succeeded").GetBoolean());
        Assert.False(forged.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("DockerIntegration.PreviewInvalid", forged.GetProperty("ErrorCode").GetString());
        Assert.DoesNotContain("secret", forged.GetProperty("ErrorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MonitoringRoutes_RejectMalformedAndForgedGrantsWithOnlySafeStableErrors()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var extra = await bridge.SendAsync("monitoring.snapshot.v1", payload: JsonDocument.Parse("{\"Name\":\"Ubuntu\",\"IntervalSeconds\":1,\"Script\":\"x\"}").RootElement.Clone());
        var missing = await bridge.SendAsync("monitoring.process.preview.v1", payload: JsonDocument.Parse("{\"SnapshotToken\":\"" + new string('a', 64) + "\",\"ProcessId\":22}").RootElement.Clone());
        var forged = await bridge.SendAsync("monitoring.process.execute.v1", payload: JsonDocument.Parse("{\"PreviewToken\":\"" + new string('a', 64) + "\"}").RootElement.Clone());
        foreach (var response in new[] { extra, missing, forged })
        {
            Assert.False(response.GetProperty("Succeeded").GetBoolean());
            var code = response.GetProperty("ErrorCode").GetString();
            var message = response.GetProperty("ErrorMessage").GetString();
            Assert.StartsWith("Monitor.", code);
            Assert.Equal(code, message);
            Assert.DoesNotContain("Ubuntu", message, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal("Monitor.InvalidRequest", extra.GetProperty("ErrorCode").GetString());
        Assert.Equal("Monitor.SnapshotInvalid", forged.GetProperty("ErrorCode").GetString());
    }

    [Fact]
    public async Task FixedExplorerRoutes_RejectPayloadsMissingIdsAndUnknownRecoveryPointsBeforeLaunch()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var payload = JsonDocument.Parse("{\"Path\":\"C:\\\\outside\"}").RootElement.Clone();
        var wslPayload = await bridge.SendAsync("explorer.wslconfig.v1", payload: payload);
        var recoveryPayload = await bridge.SendAsync("explorer.recovery-point.v1", Guid.NewGuid(), payload);
        var recoveryMissingId = await bridge.SendAsync("explorer.recovery-point.v1");
        var recoveryUnknown = await bridge.SendAsync("explorer.recovery-point.v1", Guid.NewGuid());

        foreach (var response in new[] { wslPayload, recoveryPayload, recoveryMissingId, recoveryUnknown })
            Assert.False(response.GetProperty("Succeeded").GetBoolean());
    }

    [Fact]
    public void FixedExplorerLaunchSpec_UsesOnlyExplorerAndOneResolvedArgument()
    {
        var info = FixedLaunchProcess.CreateExplorerStartInfo(@"C:\safe\fixed-target");
        Assert.Equal("explorer.exe", info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal([@"C:\safe\fixed-target"], info.ArgumentList);
    }

    [Fact]
    public async Task FixedExplorerRoutes_RejectMissingWslConfigAndForeignRecoveryWithoutLaunching()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-reparse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var id = Guid.NewGuid();
            var foreign = new RecoveryPointSummary(new RecoveryPointManifest(1, id, "foreign", "Ubuntu", 2, RecoveryPointFormat.Tar, DateTimeOffset.UtcNow, "payload.tar", 1, "hash", "test", [], ""), root, RecoveryPointVerification.Verified);
            var recovery = new Mock<IRecoveryPointService>(MockBehavior.Strict);
            recovery.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([foreign]);
            var launches = 0;
            var routes = new FixedExplorerRoutes(recovery.Object, () => root, _ => launches++, _ => true);

            Assert.Throws<InvalidOperationException>(() => routes.OpenWslConfig(new BridgeRequest("explorer.wslconfig.v1", null, null, null)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => routes.OpenRecoveryPointAsync(new BridgeRequest("explorer.recovery-point.v1", id, null, null)));
            Assert.Equal(0, launches);
            recovery.Verify(x => x.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TemplateV1Routes_UseClosedPayloadsAndDoNotExposeCoreScriptOrPathFields()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var catalog = await bridge.SendAsync("template.catalog.list.v1", payload: JsonDocument.Parse("{\"ForceRefresh\":false}").RootElement.Clone());
        var category = await bridge.SendAsync("template.catalog.list.v1", payload: JsonDocument.Parse("{\"ForceRefresh\":false,\"Category\":\"Development\"}").RootElement.Clone());
        var legacyMarketplace = await bridge.SendAsync("marketplaceScriptDiff", payload: JsonDocument.Parse("{\"TemplateId\":\"forged\",\"Sha256\":\"" + new string('a', 64) + "\"}").RootElement.Clone());
        var sourcesWithPayload = await bridge.SendAsync("template.marketplace.sources.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var discoveryWithPayload = await bridge.SendAsync("template.marketplace.discover.v1", payload: JsonDocument.Parse("{}").RootElement.Clone());
        var malformedImport = await bridge.SendAsync("template.local.import-preview.v1", payload: JsonDocument.Parse("{\"Content\":\"{}\",\"Path\":\"C:\\\\outside\"}").RootElement.Clone());
        var malformedExecute = await bridge.SendAsync("template.local.export-execute.v1", payload: JsonDocument.Parse("{\"PreviewToken\":\"" + new string('a', 64) + "\",\"TemplateId\":\"forged\"}").RootElement.Clone());
        var replay = await bridge.SendAsync("template.local.remove-execute.v1", payload: JsonDocument.Parse("{\"PreviewToken\":\"" + new string('b', 64) + "\"}").RootElement.Clone());
        var invalidReview = await bridge.SendAsync("template.marketplace.approve.v1", payload: JsonDocument.Parse("{\"ReviewToken\":\"" + new string('c', 64) + "\"}").RootElement.Clone());

        Assert.True(catalog.GetProperty("Succeeded").GetBoolean());
        Assert.True(category.GetProperty("Succeeded").GetBoolean());
        Assert.All(category.GetProperty("Value").GetProperty("Templates").EnumerateArray(), template => Assert.Equal("Development", template.GetProperty("Category").GetString()));
        var templates = catalog.GetProperty("Value").GetProperty("Templates");
        if (templates.GetArrayLength() > 0)
        {
            var display = templates[0];
            Assert.False(display.TryGetProperty("Scripts", out _));
            Assert.False(display.TryGetProperty("MarketplaceArtifactRoot", out _));
        }
        Assert.Equal("Workspace.Bridge.Invalid", legacyMarketplace.GetProperty("ErrorCode").GetString());
        foreach (var result in new[] { sourcesWithPayload, discoveryWithPayload, malformedImport, malformedExecute, replay, invalidReview })
        {
            Assert.False(result.GetProperty("Succeeded").GetBoolean());
        }
        Assert.Equal("Template.InvalidRequest", sourcesWithPayload.GetProperty("ErrorCode").GetString());
        Assert.Equal("Template.InvalidRequest", discoveryWithPayload.GetProperty("ErrorCode").GetString());
        Assert.Equal("Template.InvalidRequest", malformedImport.GetProperty("ErrorCode").GetString());
        Assert.Equal("Template.InvalidRequest", malformedExecute.GetProperty("ErrorCode").GetString());
        Assert.Equal("Template.GrantInvalid", replay.GetProperty("ErrorCode").GetString());
        Assert.Equal("Template.ReviewGrantInvalid", invalidReview.GetProperty("ErrorCode").GetString());
    }

    [Theory]
    [InlineData("template.apply.preview.v1", "{\"InstanceName\":\"Ubuntu\",\"TemplateId\":\"dev\",\"Variables\":{},\"DeclineRecoveryOffer\":true,\"Script\":\"Get-ChildItem\"}")]
    [InlineData("template.apply.execute.v1", "{\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Command\":\"cmd.exe\"}")]
    [InlineData("template.apply.status.v1", "{\"OperationId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Path\":\"C:\\\\outside\"}")]
    [InlineData("template.apply.cancel.v1", "{\"OperationId\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"ProcessId\":1}")]
    public async Task TemplateApplyRoutes_RejectUnknownPayloadFieldsBeforeAnyExecution(string operation, string json)
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var response = await bridge.SendAsync(operation, payload: JsonDocument.Parse(json).RootElement.Clone());

        Assert.False(response.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Template.InvalidRequest", response.GetProperty("ErrorCode").GetString());
        Assert.DoesNotContain("outside", response.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-ChildItem", response.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PackagedTemplateWorker_UsesFixedIdentityAndOpaqueOperationHandoff()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-template-worker-" + Guid.NewGuid().ToString("N"));
        try
        {
            var id = new string('d', 64);
            var store = new TemplateApplyOperationStore(Path.Combine(root, "template-operations"));
            var now = DateTimeOffset.UtcNow;
            await store.CreateAsync(new TemplateApplyOperationRecord(1, id, TemplateApplyGrantStore.CurrentSid(), "Ubuntu", "missing-template", "1", "", "", "", "", "", true, TemplateOperationState.Queued, now, now.AddMinutes(1), now, 0, 0, null, "Queued", null, [], false));
            var worker = FindPackagedTemplateWorkerHost();
            var bridge = FindPackagedBridgeHost();
            Assert.True(File.Exists(worker), $"The packaged template worker host was not found: {worker}");
            Assert.True(File.Exists(bridge), $"The packaged bridge host was not found: {bridge}");
            var workerName = AssemblyName.GetAssemblyName(Path.ChangeExtension(worker, ".dll"));
            var bridgeName = AssemblyName.GetAssemblyName(Path.ChangeExtension(bridge, ".dll"));
            Assert.Equal("DistroNexus.TemplateWorker", workerName.Name);
            Assert.Equal(bridgeName.Version, workerName.Version);

            using var invalid = Process.Start(new ProcessStartInfo(worker) { UseShellExecute = false, ArgumentList = { "--run-template-operation", id } })!;
            await invalid.WaitForExitAsync();
            Assert.Equal(2, invalid.ExitCode);

            using var process = Process.Start(new ProcessStartInfo(worker) { UseShellExecute = false, Environment = { ["DISTRONEXUS_TEMPLATE_STORE_ROOT"] = root }, ArgumentList = { id } })!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            var terminal = await store.ReadAsync(id);
            Assert.True(TemplateApplyOperationStore.Terminal(terminal.State));
            Assert.NotEqual("Template.WorkerStartFailed", terminal.ErrorCode);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TemplateMarketplaceReviewGrant_IsApprovedByAFreshBridgeProcess_AndCannotReplay()
    {
        var store = Path.Combine(Path.GetTempPath(), "DistroNexusTemplateGrant-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(store);
        try
        {
            var artifactPath = Path.Combine(store, "review-template.zip");
            CreateReviewArtifact(artifactPath);
            var artifactHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(artifactPath))).ToLowerInvariant();
            var scriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("echo reviewed"))).ToLowerInvariant();
            var catalogPath = Path.Combine(store, "review-catalog.json");
            var manifest = new
            {
                SchemaVersion = 2,
                Id = "review-template",
                Name = "Review Template",
                Version = "1",
                ArtifactUrl = new Uri(artifactPath).AbsoluteUri,
                ArtifactSha256 = artifactHash,
                Capabilities = Array.Empty<string>(),
                ScriptHashes = new[] { scriptHash },
                ExecutableFiles = Array.Empty<object>(),
                HealthChecks = Array.Empty<string>(),
                Compatibility = ""
            };
            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new { SchemaVersion = 2, Templates = new[] { manifest } }));

            string reviewToken;
            await using (var first = await BridgeProcess.StartAsync(store))
            {
                var add = await first.SendAsync("template.marketplace.add-source.v1", payload: JsonPayload(new { Url = new Uri(catalogPath).AbsoluteUri, Kind = "UserLocal", AcceptNonHttps = true }));
                Assert.True(add.GetProperty("Succeeded").GetBoolean());
                var sourceId = add.GetProperty("Value").GetProperty("Id").GetString();
                var discovery = await first.SendAsync("template.marketplace.discover.v1");
                Assert.True(discovery.GetProperty("Succeeded").GetBoolean());
                var entry = Assert.Single(discovery.GetProperty("Value").EnumerateArray());
                var review = await first.SendAsync("template.marketplace.review.v1", payload: JsonPayload(new { SourceId = sourceId, TemplateId = "review-template", ManifestDigest = entry.GetProperty("ManifestDigest").GetString() }));
                Assert.True(review.GetProperty("Succeeded").GetBoolean());
                var reviewValue = review.GetProperty("Value");
                Assert.Equal("1", reviewValue.GetProperty("TemplateVersion").GetString());
                Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new Uri(catalogPath).AbsoluteUri.TrimEnd('/')))).ToLowerInvariant(), reviewValue.GetProperty("NormalizedSourceIdentity").GetString());
                Assert.Matches("^[a-f0-9]{64}$", reviewValue.GetProperty("ScriptDiffDigest").GetString());
                Assert.True(reviewValue.GetProperty("ChangedScriptIdentifiers").GetArrayLength() <= 100);
                Assert.Equal(reviewValue.GetProperty("ChangedScriptIdentifiers").GetArrayLength(), reviewValue.GetProperty("ChangedScriptCount").GetInt32());
                reviewToken = review.GetProperty("Value").GetProperty("ReviewToken").GetString()!;
            }

            await using var second = await BridgeProcess.StartAsync(store);
            var approval = await second.SendAsync("template.marketplace.approve.v1", payload: JsonPayload(new { ReviewToken = reviewToken }));
            var replay = await second.SendAsync("template.marketplace.approve.v1", payload: JsonPayload(new { ReviewToken = reviewToken }));
            Assert.True(approval.GetProperty("Succeeded").GetBoolean());
            Assert.Equal(artifactHash, approval.GetProperty("Value").GetProperty("Sha256").GetString());
            Assert.Equal("1", approval.GetProperty("Value").GetProperty("Version").GetString());
            Assert.False(replay.GetProperty("Succeeded").GetBoolean());
            Assert.Equal("Template.ReviewGrantInvalid", replay.GetProperty("ErrorCode").GetString());
        }
        finally { try { Directory.Delete(store, true); } catch { } }
    }

    private static JsonElement JsonPayload(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static void CreateReviewArtifact(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("template.json");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("{\"id\":\"review-template\",\"name\":\"Review Template\",\"scripts\":[{\"content\":\"echo reviewed\"}]}");
    }

    private sealed class BridgeProcess : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string store;
        private readonly bool ownsStore;

        private BridgeProcess(Process process, string store, bool ownsStore)
        {
            this.process = process;
            this.store = store;
            this.ownsStore = ownsStore;
        }

        public static Task<BridgeProcess> StartAsync(string? store = null)
        {
            var root = FindRoot();
            var bridgeDirectory = Path.Combine(root, "src", "Client", "DistroNexus.WorkspaceBridge", "bin");
            var bridge = new[] { "Debug", "Release" }
                .Select(configuration => Path.Combine(bridgeDirectory, configuration, "net10.0", "DistroNexus.WorkspaceBridge.dll"))
                .FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException("Build the WorkspaceBridge project before running protocol tests.");
            var ownsStore = string.IsNullOrWhiteSpace(store);
            store ??= Path.Combine(Path.GetTempPath(), "DistroNexusBridge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(store);
            var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{bridge}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["DISTRONEXUS_WORKSPACE_STORE_ROOT"] = store },
            }) ?? throw new InvalidOperationException("Unable to start WorkspaceBridge.");
            return Task.FromResult(new BridgeProcess(process, store, ownsStore));
        }

        public async Task<JsonElement> SendAsync(string operation, Guid? id = null, JsonElement? payload = null, long? expectedRevision = null, string? token = null)
        {
            var request = new { Operation = operation, Id = id, Payload = payload, ExpectedRevision = expectedRevision, Token = token };
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
            var line = await process.StandardOutput.ReadLineAsync();
            Assert.False(string.IsNullOrWhiteSpace(line), "WorkspaceBridge ended before returning a response.");
            using var document = JsonDocument.Parse(line!);
            return document.RootElement.Clone();
        }

        public async ValueTask DisposeAsync()
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync();
            process.Dispose();
            if (ownsStore) Directory.Delete(store, true);
        }
    }

    private static string FindRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "AGENTS.md")))
            path = Directory.GetParent(path)?.FullName ?? throw new DirectoryNotFoundException();
        return path;
    }

    private static string FindPackagedWorkerHost()
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Name
            ?? throw new DirectoryNotFoundException("Unable to determine the active test configuration.");
        return Path.Combine(FindRoot(), "src", "Client", "DistroNexus.WorkspaceBridge", "bin", configuration, "net10.0", "WorkspaceWorker", "DistroNexus.WorkspaceWorker.exe");
    }

    private static string FindPackagedTemplateWorkerHost()
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Name
            ?? throw new DirectoryNotFoundException("Unable to determine the active test configuration.");
        return Path.Combine(FindRoot(), "src", "Client", "DistroNexus.WorkspaceBridge", "bin", configuration, "net10.0", "TemplateWorker", "DistroNexus.TemplateWorker.exe");
    }

    private static string FindPackagedBridgeHost()
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Name
            ?? throw new DirectoryNotFoundException("Unable to determine the active test configuration.");
        return Path.Combine(FindRoot(), "src", "Client", "DistroNexus.WorkspaceBridge", "bin", configuration, "net10.0", "DistroNexus.WorkspaceBridge.exe");
    }
}
