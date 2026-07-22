using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class SystemdNetworkServicesTests
{
    [Fact]
    public async Task NetworkConfiguration_PreviewsAndSavesLosslesslyWithWslRestartImpact()
    {
        var config = new Mock<IWslConfigurationService>();
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("#keep\n[custom]\nx=y\n"));
        config.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string>()), source, [], 1, "fp", RestartScope.Wsl, source.ToString()));
        config.Setup(x => x.PreviewAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationPreview(source.ToString(), "#keep\n[custom]\nx=y\n[wsl2]\nnetworkingMode=nat\n", ["wsl2.networkingMode"], RestartScope.Wsl));
        config.Setup(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        var caps = HostCapabilities(CapabilityId.MirroredNetworking, CapabilityStatus.Supported);
        var service = new NetworkConfigurationService(config.Object, caps, new NetworkDiagnosticsService());
        var preview = await service.PreviewModeAsync(WslNetworkingMode.Nat);
        Assert.Equal(RestartScope.Wsl, preview.Configuration.RestartScope);
        await service.ApplyModeAsync(WslNetworkingMode.Nat, preview.Token);
        config.Verify(x => x.SaveAsync(It.Is<IReadOnlyDictionary<string, string?>>(v => v["wsl2.networkingMode"] == "nat"), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task NetworkSettings_RequireMatchingSingleUsePreviewToken()
    {
        var config = new Mock<IWslConfigurationService>();
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("[wsl2]\n"));
        config.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string>()), source, [], 0, "fp", RestartScope.Wsl, source.ToString()));
        config.Setup(x => x.PreviewAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationPreview("", "", [], RestartScope.Wsl));
        config.Setup(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        var service = new NetworkConfigurationService(config.Object, HostCapabilities(CapabilityId.MirroredNetworking, CapabilityStatus.Supported), new NetworkDiagnosticsService());
        var settings = new NetworkSettings(DnsTunneling: true);
        var preview = await service.PreviewSettingsAsync(settings);
        await service.ApplySettingsAsync(settings, preview.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplySettingsAsync(settings, preview.Token));
    }

    [Fact]
    public async Task NetworkSettings_ReadsCurrentValuesWithoutProjectingMissingKeys()
    {
        var config = new Mock<IWslConfigurationService>();
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("[wsl2]\ndnsTunneling=true\nuntouched=value\n"));
        config.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string> { ["wsl2.dnsTunneling"] = "true" }), source, [], 0, "fp", RestartScope.None, source.ToString()));
        var service = new NetworkConfigurationService(config.Object, HostCapabilities(CapabilityId.MirroredNetworking, CapabilityStatus.Supported), new NetworkDiagnosticsService());
        var settings = await service.ReadSettingsAsync();
        Assert.True(settings.DnsTunneling); Assert.Null(settings.Firewall); Assert.Null(settings.IgnoredPorts);
    }

    [Fact]
    public async Task NetworkStatusAdapter_ReportsUnavailableWithoutInventingCollision()
    {
        var adapter = new WindowsNetworkStatusAdapter();
        Assert.True((await adapter.GetFirewallStatusAsync()).Availability is FirewallStatusAvailability.Available or FirewallStatusAvailability.Unavailable);
        Assert.False((await adapter.GetPortCollisionsAsync([new PortMapping { Port = 80, Protocol = "TCP" }])).Single().IsCollision);
    }

    [Fact]
    public void SafeBrowserUri_AllowsOnlyValidatedLoopbackHttp()
    {
        Assert.Equal("http://127.0.0.1:8080/", SafeBrowserUri.FromPortMapping(new PortMapping { LocalAddress = "127.0.0.1", Port = 8080 })!.AbsoluteUri);
        Assert.Null(SafeBrowserUri.FromPortMapping(new PortMapping { LocalAddress = "10.0.0.4", Port = 80 }));
        Assert.Null(SafeBrowserUri.FromPortMapping(new PortMapping { LocalAddress = "localhost", Port = 0 }));
        Assert.Null(SafeBrowserUri.FromPortMapping(new PortMapping { LocalAddress = "localhost", Port = 80 }, "file"));
    }
    [Fact]
    public void SystemdUnitName_RejectsShellAndPaths()
    {
        Assert.Throws<ArgumentException>(() => new SystemdUnitName("ssh.service; id"));
        Assert.Throws<ArgumentException>(() => new SystemdUnitName("../ssh.service"));
        Assert.Equal("ssh.service", new SystemdUnitName("ssh.service").Value);
    }

    [Fact]
    public void ParseServices_HandlesStablePropertyBlocks()
    {
        var result = SystemdService.ParseServices("Id=ssh.service\nDescription=OpenSSH\nActiveState=active\nSubState=running\nUnitFileState=enabled\nLoadState=loaded\n\n", SystemdScope.System);
        var item = Assert.Single(result);
        Assert.Equal("ssh.service", item.Name.Value);
        Assert.Equal("enabled", item.EnabledState);
    }

    [Theory]
    [InlineData("Identifiant=ssh.service\nEtat=actif\n")]
    [InlineData("Id=ssh.service; rm -rf /\nActiveState=active\n")]
    [InlineData("Id=ssh.service\nDescription=truncated")]
    public void ParseServices_MalformedLocalizedOrTruncatedOutput_DoesNotInventServices(string output)
    {
        Assert.Empty(SystemdService.ParseServices(output, SystemdScope.System));
    }

    [Fact]
    public void ParseUnitList_JoinsMachineReadableUnitAndEnablementState()
    {
        var result = SystemdService.ParseUnitList("[{\"unit\":\"ssh.service\",\"load\":\"loaded\",\"active\":\"active\",\"sub\":\"running\",\"description\":\"OpenSSH\"}]", new Dictionary<string, string> { ["ssh.service"] = "enabled" }, SystemdScope.System);
        var service = Assert.Single(result);
        Assert.Equal("ssh.service", service.Name.Value); Assert.Equal("enabled", service.EnabledState); Assert.Equal("running", service.SubState);
    }

    [Fact]
    public async Task PreviewAndExecute_UseArgumentListAndNonInteractiveSudo()
    {
        ProcessRequest? captured = null;
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ProcessResult(0, "Id=ssh.service\nDescription=SSH\nActiveState=active\nSubState=running\nUnitFileState=enabled\nLoadState=loaded", "", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        var preview = await service.PreviewAsync("Ubuntu", new SystemdUnitName("ssh.service"), SystemdAction.Restart, SystemdScope.System);
        Assert.True(preview.RequiresLinuxPrivilege);
        var result = await service.ExecuteAsync(preview);
        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        runner.Verify(x => x.RunAsync(It.Is<ProcessRequest>(r => r.Arguments.Contains("sudo") && r.Arguments.Contains("--non-interactive")), It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(captured!.Arguments, x => x.Contains("-c", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UserScope_UsesSystemctlUserManager()
    {
        ProcessRequest? captured = null;
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ProcessResult(0, "Id=ssh.service\nDescription=SSH\nActiveState=active\nSubState=running\nUnitFileState=enabled\nLoadState=loaded", "", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        await service.ExecuteAsync(await service.PreviewAsync("Ubuntu", new SystemdUnitName("ssh.service"), SystemdAction.Start, SystemdScope.User));
        Assert.Contains("--user", captured!.Arguments);
        Assert.Equal(["--distribution", "Ubuntu", "--user", "dev", "--", "systemctl", "--user", "show", "ssh.service", "--no-page", "--property=Id,Description,ActiveState,SubState,UnitFileState,LoadState,FragmentPath,After,Requires"], captured.Arguments);
        Assert.DoesNotContain("sudo", captured.Arguments);
    }

    [Fact]
    public async Task SystemdReads_NeverUseSudo()
    {
        ProcessRequest? captured = null;
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).Callback<ProcessRequest, CancellationToken>((r, _) => captured = r).ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        await service.ListAsync("Ubuntu", SystemdScope.System);
        Assert.DoesNotContain("sudo", captured!.Arguments);
    }

    [Fact]
    public async Task ServiceList_UsesBoundedMachineReadableEnumerationForUserScope()
    {
        var requests = new List<ProcessRequest>();
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).Callback<ProcessRequest, CancellationToken>((r, _) => requests.Add(r)).ReturnsAsync(new ProcessResult(0, "[]", "", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        await service.ListAsync("Ubuntu", SystemdScope.User);
        Assert.Equal(2, requests.Count);
        Assert.Equal(["--distribution", "Ubuntu", "--user", "dev", "--", "systemctl", "--user", "list-units", "--all", "--type=service", "--no-legend", "--plain", "--output=json"], requests[0].Arguments);
        Assert.Equal(["--distribution", "Ubuntu", "--user", "dev", "--", "systemctl", "--user", "list-unit-files", "--type=service", "--no-legend", "--plain", "--output=json"], requests[1].Arguments);
    }

    [Fact]
    public async Task WslDnsAdapter_UsesFixedArgumentList()
    {
        ProcessRequest? captured = null;
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).Callback<ProcessRequest, CancellationToken>((r, _) => captured = r).ReturnsAsync(new ProcessResult(0, "127.0.0.1 example", "", TimeSpan.Zero, false, false, false, 1));
        var result = await new WslNetworkDiagnosticsAdapter(runner.Object).ProbeAsync(new NetworkProbeRequest(NetworkProbeKind.WslInstance, "example.test", DistributionName: "Ubuntu"));
        Assert.Equal(NetworkProbeOutcome.Resolved, result.Outcome);
        Assert.Equal(["--distribution", "Ubuntu", "--exec", "getent", "ahosts", "example.test"], captured!.Arguments);
    }

    [Fact]
    public async Task Execute_PrivilegeFailureNeverRequestsOrStoresPassword()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, "", "sudo: a password is required", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        var result = await service.ExecuteAsync(await service.PreviewAsync("Ubuntu", new SystemdUnitName("ssh.service"), SystemdAction.Start, SystemdScope.System));
        Assert.False(result.Succeeded);
        Assert.Equal("RequiresLinuxPrivilege", result.OutcomeCode);
        Assert.Contains("DN-8002", result.Guidance!);
    }

    [Fact]
    public async Task SystemdPreviewToken_RejectsForgedAndReuse()
    {
        var runner = new Mock<IProcessRunner>(); runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcessResult(0, "Id=ssh.service\nActiveState=active\nLoadState=loaded\n", "", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        var preview = await service.PreviewAsync("Ubuntu", new SystemdUnitName("ssh.service"), SystemdAction.Start, SystemdScope.User);
        var forged = preview with { PreviewToken = "forged" };
        Assert.Equal("PreviewRequired", (await service.ExecuteAsync(forged)).OutcomeCode);
        Assert.True((await service.ExecuteAsync(preview)).Succeeded);
        Assert.Equal("PreviewRequired", (await service.ExecuteAsync(preview)).OutcomeCode);
    }

    [Theory]
    [InlineData(SystemdAction.Start, "active", true)]
    [InlineData(SystemdAction.Stop, "inactive", true)]
    [InlineData(SystemdAction.Start, "inactive", false)]
    public async Task Execute_VerifiesExpectedActiveState(SystemdAction action, string activeState, bool succeeds)
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, $"Id=ssh.service\nActiveState={activeState}\nLoadState=loaded\n", "", TimeSpan.Zero, false, false, false, 1));
        var service = new SystemdService(runner.Object, CapabilityService(), DistributionConfig());
        var result = await service.ExecuteAsync(await service.PreviewAsync("Ubuntu", new SystemdUnitName("ssh.service"), action, SystemdScope.User));
        Assert.Equal(succeeds, result.Succeeded);
        Assert.Equal(succeeds ? "Succeeded" : "PostconditionFailed", result.OutcomeCode);
    }

    [Fact]
    public async Task NetworkDiagnostics_ClassifiesRefusedAndInvalidInput()
    {
        var service = new NetworkDiagnosticsService();
        var invalid = await service.ProbeAsync(new NetworkProbeRequest(NetworkProbeKind.TcpEndpoint, "bad\nhost", 443));
        Assert.Equal(NetworkProbeOutcome.InvalidInput, invalid.Outcome);
        var refused = await service.ProbeAsync(new NetworkProbeRequest(NetworkProbeKind.TcpEndpoint, "127.0.0.1", 1, TimeSpan.FromSeconds(1)));
        Assert.Contains(refused.Outcome, new[] { NetworkProbeOutcome.Refused, NetworkProbeOutcome.TimedOut, NetworkProbeOutcome.ToolUnavailable });
    }

    [Theory]
    [InlineData(NetworkProbeKind.WslInstance)]
    [InlineData(NetworkProbeKind.Localhost)]
    public async Task WslSideProbeKinds_DoNotMasqueradeAsWindowsTcp(NetworkProbeKind kind)
    {
        var result = await new NetworkDiagnosticsService().ProbeAsync(new NetworkProbeRequest(kind, "127.0.0.1", 80, DistributionName: "Ubuntu"));
        Assert.True(result.Outcome is NetworkProbeOutcome.ToolUnavailable or NetworkProbeOutcome.Refused or NetworkProbeOutcome.TimedOut);
    }

    [Fact]
    public async Task GatewayWithoutPort_IsTypedInvalidInput()
    {
        var result = await new NetworkDiagnosticsService().ProbeAsync(new NetworkProbeRequest(NetworkProbeKind.Gateway, "192.0.2.1"));
        Assert.Equal(NetworkProbeOutcome.InvalidInput, result.Outcome);
    }

    [Fact]
    public async Task FirewallBroker_PreviewsExactScopeAndRejectsUnownedRemoval()
    {
        var broker = new GuardedFirewallOperationBroker();
        var preview = await broker.PreviewCreateAsync(new FirewallRuleRequest(FirewallDirection.Inbound, FirewallProtocol.Tcp, 443, ["Private"], "10.0.0.0/8", @"C:\tools\app.exe"));
        Assert.True(preview.RequiresElevation);
        Assert.Contains("443", string.Join(" ", preview.Effects));
        await Assert.ThrowsAsync<InvalidOperationException>(() => broker.PreviewRemoveAsync("third-party"));
        Assert.Equal("ElevatedHelperUnavailable", (await broker.CreateAsync(preview)).OutcomeCode);
        Assert.Equal("PreviewRequired", (await broker.CreateAsync(preview)).OutcomeCode);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => broker.PreviewCreateAsync(new FirewallRuleRequest(FirewallDirection.Inbound, FirewallProtocol.Tcp, 0, ["Private"])));
    }

    [Fact]
    public async Task FirewallUnavailableCreate_DoesNotInventOwnedRule()
    {
        var broker = new GuardedFirewallOperationBroker();
        var create = await broker.PreviewCreateAsync(new FirewallRuleRequest(FirewallDirection.Inbound, FirewallProtocol.Tcp, 8080, ["Private"]));
        Assert.Equal("ElevatedHelperUnavailable", (await broker.CreateAsync(create)).OutcomeCode);
        Assert.Empty(await broker.ListOwnedAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => broker.PreviewRemoveAsync(create.RuleId));
    }

    private static IPlatformCapabilityService CapabilityService()
    {
        var capability = new Mock<IPlatformCapabilityService>();
        var result = new CapabilityResult(CapabilityId.InstanceSystemd, CapabilityStatus.Supported, "test", CapabilitySource.InstanceCli, DateTimeOffset.UtcNow);
        capability.Setup(x => x.GetInstanceSnapshotAsync("Ubuntu", false, It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceCapabilitySnapshot(new InstancePlatformFacts("Ubuntu", 2, null, null, true, true), new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.InstanceSystemd] = result }, DateTimeOffset.UtcNow));
        return capability.Object;
    }
    private static IDistributionConfigurationService DistributionConfig(string? user = "dev")
    {
        var configuration = new Mock<IDistributionConfigurationService>();
        var values = user is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["user.default"] = user };
        var source = LosslessIniDocument.Parse(System.Text.Encoding.UTF8.GetBytes("[user]\ndefault=dev\n"));
        configuration.Setup(x => x.ReadAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationDocument<DistributionConfigurationSettings>(new DistributionConfigurationSettings(values), source, [], 0, "fp", RestartScope.None, source.ToString()));
        return configuration.Object;
    }
    private static IPlatformCapabilityService HostCapabilities(CapabilityId id, CapabilityStatus status)
    {
        var mock = new Mock<IPlatformCapabilityService>(); var now = DateTimeOffset.UtcNow;
        mock.Setup(x => x.GetHostSnapshotAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null), new Dictionary<CapabilityId, CapabilityResult> { [id] = new(id, status, "test", CapabilitySource.WslCli, now) }, new Dictionary<CapabilityId, CapabilityResult>(), now));
        return mock.Object;
    }
}
