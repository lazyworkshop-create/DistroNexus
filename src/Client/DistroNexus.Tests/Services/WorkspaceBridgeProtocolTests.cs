using System.Diagnostics;
using System.Text.Json;

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
            var bridge = new[] { "Release", "Debug" }
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
