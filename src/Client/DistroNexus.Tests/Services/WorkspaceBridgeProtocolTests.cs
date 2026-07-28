using System.Diagnostics;
using System.Text.Json;
using DistroNexus.Core.Models;

namespace DistroNexus.Tests.Services;

public sealed class WorkspaceBridgeProtocolTests
{
    [Fact]
    public async Task Lifecycle_UsesCoreStoreAndEnforcesRevisions()
    {
        await using var bridge = await BridgeProcess.StartAsync();

        var empty = await bridge.SendAsync("list");
        Assert.True(empty.GetProperty("Succeeded").GetBoolean());
        Assert.Equal(0, empty.GetProperty("Value").GetArrayLength());

        var definition = JsonDocument.Parse("""
            {"Id":"b2acbf0d-27d7-496e-937c-612ec37ac5ee","DisplayName":"Bridge workspace","InstanceName":"Ubuntu","PreflightChecks":[],"ActionGroups":[],"ClosePolicy":{"Mode":"None","ServiceNames":[]},"TrustState":"Untrusted"}
            """).RootElement.Clone();
        var preview = await bridge.SendAsync("previewImport", payload: definition);
        Assert.True(preview.GetProperty("Succeeded").GetBoolean());
        var token = preview.GetProperty("Value").GetProperty("ImportToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var missingExpectedRevision = await bridge.SendAsync("import", payload: definition, token: token);
        Assert.False(missingExpectedRevision.GetProperty("Succeeded").GetBoolean());
        var imported = await bridge.SendAsync("import", payload: definition, expectedRevision: 0, token: token);
        Assert.True(imported.GetProperty("Succeeded").GetBoolean());
        var saved = imported.GetProperty("Value");
        var id = saved.GetProperty("Id").GetGuid();
        Assert.Equal(1, saved.GetProperty("Revision").GetInt64());
        Assert.Equal("Untrusted", saved.GetProperty("TrustState").GetString());

        var approved = await bridge.SendAsync("approveTrust", id, expectedRevision: 1);
        Assert.True(approved.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Trusted", approved.GetProperty("Value").GetProperty("TrustState").GetString());
        Assert.Equal(2, approved.GetProperty("Value").GetProperty("Revision").GetInt64());

        var conflict = await bridge.SendAsync("remove", id, expectedRevision: 1);
        Assert.False(conflict.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.ConflictOrState", conflict.GetProperty("ErrorCode").GetString());

        var removed = await bridge.SendAsync("remove", id, expectedRevision: 2);
        Assert.True(removed.GetProperty("Succeeded").GetBoolean());
        var finalList = await bridge.SendAsync("list");
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
        var preview = await bridge.SendAsync("healthRepairPreview", payload: previewRequest);
        var execute = await bridge.SendAsync("healthRepairExecute", token: preview.GetProperty("Value").GetProperty("PreviewToken").GetString(),
            payload: JsonDocument.Parse($$"""{"Finding":{{finding.GetRawText()}},"Confirmed":true}""").RootElement.Clone());

        Assert.True(execute.GetProperty("Succeeded").GetBoolean());
        Assert.False(execute.GetProperty("Value").GetProperty("Succeeded").GetBoolean());
        Assert.Contains("Desktop-only", execute.GetProperty("Value").GetProperty("Error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DryRunMutations_AreStructuredAndDoNotPersistOrIssueTokens()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var definition = JsonDocument.Parse("""
            {"Id":"d2acbf0d-27d7-496e-937c-612ec37ac5ee","DisplayName":"Dry run","InstanceName":"Ubuntu","PreflightChecks":[],"ActionGroups":[],"ClosePolicy":{"Mode":"None","ServiceNames":[]},"TrustState":"Trusted"}
            """).RootElement.Clone();

        var create = await bridge.SendAsync("previewSave", payload: definition, expectedRevision: 0);
        Assert.True(create.GetProperty("Succeeded").GetBoolean());
        var value = create.GetProperty("Value");
        Assert.True(value.GetProperty("SchemaValid").GetBoolean());
        Assert.False(value.TryGetProperty("LaunchToken", out _));
        Assert.False(value.TryGetProperty("ImportToken", out _));
        Assert.Equal(0, (await bridge.SendAsync("list")).GetProperty("Value").GetArrayLength());

        var import = await bridge.SendAsync("previewImportDryRun", payload: definition, expectedRevision: 0);
        Assert.True(import.GetProperty("Succeeded").GetBoolean());
        Assert.False(import.GetProperty("Value").TryGetProperty("ImportToken", out _));
        Assert.Equal(0, (await bridge.SendAsync("list")).GetProperty("Value").GetArrayLength());

        var saved = await bridge.SendAsync("save", payload: definition, expectedRevision: 0);
        var id = saved.GetProperty("Value").GetProperty("Id").GetGuid();
        var export = await bridge.SendAsync("previewExportDryRun", id, expectedRevision: 1);
        Assert.True(export.GetProperty("Succeeded").GetBoolean());
        Assert.True(export.GetProperty("Value").GetProperty("SchemaValid").GetBoolean());
        Assert.False(export.GetProperty("Value").TryGetProperty("LaunchToken", out _));
        Assert.False(export.GetProperty("Value").TryGetProperty("ImportToken", out _));
    }

    [Fact]
    public async Task Export_RequiresAndEnforcesTheExpectedRevision()
    {
        await using var bridge = await BridgeProcess.StartAsync();
        var definition = JsonDocument.Parse("""
            {"Id":"e2acbf0d-27d7-496e-937c-612ec37ac5ee","DisplayName":"Export revision","InstanceName":"Ubuntu","PreflightChecks":[],"ActionGroups":[],"ClosePolicy":{"Mode":"None","ServiceNames":[]},"TrustState":"Trusted"}
            """).RootElement.Clone();
        var saved = await bridge.SendAsync("save", payload: definition, expectedRevision: 0);
        Assert.True(saved.GetProperty("Succeeded").GetBoolean());
        var id = saved.GetProperty("Value").GetProperty("Id").GetGuid();

        var missing = await bridge.SendAsync("export", id);
        Assert.False(missing.GetProperty("Succeeded").GetBoolean());
        var stale = await bridge.SendAsync("export", id, expectedRevision: 0);
        Assert.False(stale.GetProperty("Succeeded").GetBoolean());
        Assert.Equal("Workspace.ConflictOrState", stale.GetProperty("ErrorCode").GetString());

        var exported = await bridge.SendAsync("export", id, expectedRevision: 1);
        Assert.True(exported.GetProperty("Succeeded").GetBoolean());
        Assert.Contains("Export revision", exported.GetProperty("Value").GetString(), StringComparison.Ordinal);
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
        Assert.Equal(["-w", "0", "wsl", "-d", "Ubuntu", "--cd", "/home/user"], windowsTerminal.ArgumentList);

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
            ("healthScan", "health.scan.v1"), ("healthRepairPreview", "health.repair-preview.v1"), ("healthRepairExecute", "health.repair.v1")
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

    private sealed class BridgeProcess : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string store;

        private BridgeProcess(Process process, string store)
        {
            this.process = process;
            this.store = store;
        }

        public static Task<BridgeProcess> StartAsync()
        {
            var root = FindRoot();
            var bridgeDirectory = Path.Combine(root, "src", "Client", "DistroNexus.WorkspaceBridge", "bin");
            var bridge = new[] { "Debug", "Release" }
                .Select(configuration => Path.Combine(bridgeDirectory, configuration, "net10.0", "DistroNexus.WorkspaceBridge.dll"))
                .FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException("Build the WorkspaceBridge project before running protocol tests.");
            var store = Path.Combine(Path.GetTempPath(), "DistroNexusBridge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(store);
            var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{bridge}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["DISTRONEXUS_WORKSPACE_STORE_ROOT"] = store },
            }) ?? throw new InvalidOperationException("Unable to start WorkspaceBridge.");
            return Task.FromResult(new BridgeProcess(process, store));
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
            Directory.Delete(store, true);
        }
    }

    private static string FindRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "AGENTS.md")))
            path = Directory.GetParent(path)?.FullName ?? throw new DirectoryNotFoundException();
        return path;
    }
}
