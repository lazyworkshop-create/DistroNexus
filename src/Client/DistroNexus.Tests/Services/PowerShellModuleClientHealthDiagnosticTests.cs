using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class PowerShellModuleClientHealthDiagnosticTests
{
    [Fact]
    public async Task HealthReadsAndPreview_UseOnlyRegisteredTypedParameters()
    {
        var ps = new Mock<IPowerShellService>(MockBehavior.Strict);
        ps.Setup(x => x.ExecuteModuleCmdletAsync("Invoke-DistroNexusHealthScan", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (bool)p["AsJson"]), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(Result("{\"ScanId\":\"00000000-0000-0000-0000-000000000001\",\"StartedAt\":\"2026-01-01T00:00:00Z\",\"CompletedAt\":\"2026-01-01T00:00:00Z\",\"Findings\":[]}"));
        ps.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusHealthHistory", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (bool)p["AsJson"]), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(Result("[]"));
        ps.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusDiagnosticLogOption", It.Is<Dictionary<string, object>>(p => p.Count == 1 && (bool)p["AsJson"]), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(Result("[\"app:current\"]"));
        var finding = new HealthFinding("id", HealthSeverity.Warning, HealthScope.Host, "title", "detail", RepairId: "repair");
        ps.Setup(x => x.ExecuteModuleCmdletAsync("Get-DistroNexusHealthRepairPreview", It.Is<Dictionary<string, object>>(p => p.Count == 1 && p.ContainsKey("Finding") && p["Finding"] == finding), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(Result("{\"RepairId\":\"repair\",\"Title\":\"title\",\"Safety\":\"Safe\",\"Idempotency\":\"Idempotent\",\"Changes\":[],\"Commands\":[],\"PreviewToken\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}"));
        var client = new PowerShellModuleClient(ps.Object);
        await client.ScanHealthAsync(); await client.GetHealthHistoryAsync(); Assert.Equal(["app:current"], await client.GetDiagnosticLogOptionsAsync()); await client.GetHealthRepairPreviewAsync(finding);
        ps.VerifyAll();
    }

    [Fact]
    public async Task RepairAndExport_ForwardOnlyOpaqueTokensBasenameConfirmAndDeadline()
    {
        var ps = new Mock<IPowerShellService>(MockBehavior.Strict); var token = new string('a', 32);
        ps.Setup(x => x.ExecuteModuleCmdletAsync("Repair-DistroNexusHealthFinding", It.Is<Dictionary<string, object>>(p => p.Count == 2 && p["Confirm"].Equals(false) && (string)p["PreviewToken"] == token), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(Result("{\"RepairId\":\"repair\",\"Succeeded\":true,\"Results\":[]}"));
        ps.Setup(x => x.ExecuteModuleCmdletAsync("Export-DistroNexusDiagnosticReport", It.Is<Dictionary<string, object>>(p => p.Count == 4 && (string)p["SnapshotToken"] == token && (string)p["DestinationFileName"] == "report.json" && (int)p["DeadlineMilliseconds"] == 100 && p["Confirm"].Equals(false) && !p.Values.Any(v => v is DiagnosticReportPreview)), It.Is<ModuleCallOptions>(o => o.ParseAsJson), It.IsAny<CancellationToken>())).ReturnsAsync(Result("{\"DestinationFileName\":\"report.json\",\"Location\":\"DistroNexusDiagnostics\"}"));
        var client = new PowerShellModuleClient(ps.Object); await client.RepairHealthAsync(token); await client.ExportDiagnosticReportAsync(token, "report.json", 100); ps.VerifyAll();
    }

    [Theory]
    [InlineData("bad", "report.json", 100)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "..\\report.json", 100)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "report.json", 30001)]
    public async Task RepairAndExport_InvalidInputsDoNotInvokeModule(string token, string name, int deadline)
    {
        var ps = new Mock<IPowerShellService>(MockBehavior.Strict); var client = new PowerShellModuleClient(ps.Object);
        if (token == "bad") await Assert.ThrowsAsync<ArgumentException>(() => client.RepairHealthAsync(token));
        else await Assert.ThrowsAsync<ArgumentException>(() => client.ExportDiagnosticReportAsync(token, name, deadline));
        ps.VerifyNoOtherCalls();
    }

    private static PowerShellScriptResult Result(string output) => new() { ExitCode = 0, Output = output };
}
