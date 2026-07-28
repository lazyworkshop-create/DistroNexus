using System.Text;
using System.IO;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.Desktop.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public class ConfigurationViewModelTests
{
    [Theory]
    [MemberData(nameof(GlobalConfigurationFailures))]
    public async Task GlobalConfiguration_SaveFailures_MapToCopyableRedactedAlerts(Exception exception, int expectedCode)
    {
        var (vm, configuration, dialogs, _) = NewGlobalConfigurationViewModel();
        await vm.LoadAsync();
        vm.Fields.Single(f => f.Id == "wsl2.memory").Desired = "4GB";
        configuration.Setup(s => s.PreviewAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        await vm.SaveAndMarkPendingRestartCommand.ExecuteAsync(null);

        AssertAlertIsRedactedAndCoded(dialogs, expectedCode);
    }

    [Fact]
    public async Task GlobalConfiguration_LoadWslOperationFailure_MapsToCopyableRedactedAlert()
    {
        var wslConfig = new Mock<IWslConfigService>();
        var (vm, _, dialogs, _) = NewGlobalConfigurationViewModel(wslConfig);
        wslConfig.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));

        await vm.LoadAsync();

        AssertAlertIsRedactedAndCoded(dialogs, 9003);
    }

    [Fact]
    public async Task GlobalConfiguration_LoadWslException_MapsToCopyableRedactedAlert()
    {
        var legacy = new Mock<IWslConfigService>();
        var (vm, _, dialogs, _) = NewGlobalConfigurationViewModel(legacy);
        legacy.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));

        await vm.LoadAsync();

        AssertAlertIsRedactedAndCoded(dialogs, 9003);
    }

    [Fact]
    public async Task GlobalConfiguration_PreviewWslException_MapsToCopyableRedactedAlert()
    {
        var (vm, configuration, dialogs, _) = NewGlobalConfigurationViewModel();
        await vm.LoadAsync();
        configuration.Setup(s => s.PreviewAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));
        vm.Fields.Single(f => f.Id == "wsl2.memory").Desired = "4GB";

        await vm.SaveAndMarkPendingRestartCommand.ExecuteAsync(null);

        AssertAlertsAreRedactedAndCoded(dialogs, 9003);
    }

    [Fact]
    public async Task GlobalConfiguration_SaveWslException_MapsToCopyableRedactedAlert()
    {
        var (vm, configuration, dialogs, _) = NewGlobalConfigurationViewModel();
        await vm.LoadAsync();
        configuration.Setup(s => s.PreviewAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationPreview(string.Empty, "[wsl2]\\nmemory=4GB\\n", ["wsl2.memory"], RestartScope.Wsl));
        configuration.Setup(s => s.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));
        vm.Fields.Single(f => f.Id == "wsl2.memory").Desired = "4GB";

        await vm.SaveAndMarkPendingRestartCommand.ExecuteAsync(null);

        AssertAlertIsRedactedAndCoded(dialogs, 9003);
    }

    [Fact]
    public async Task GlobalConfiguration_SaveMarksRestartPendingWithoutShuttingDownWsl()
    {
        var (vm, configuration, dialogs, manager) = NewGlobalConfigurationViewModel();
        var preview = new ConfigurationPreview(string.Empty, "[wsl2]\nmemory=4GB\n", ["wsl2.memory"], RestartScope.Wsl);
        configuration.Setup(s => s.PreviewAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);
        configuration.Setup(s => s.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl));
        manager.Setup(s => s.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance { Name = "Ubuntu", State = "Running" }]);

        await vm.LoadAsync();
        vm.Fields.Single(f => f.Id == "wsl2.memory").Desired = "4GB";
        await vm.SaveAndMarkPendingRestartCommand.ExecuteAsync(null);

        manager.Verify(s => s.ShutdownWslAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString("Configuration_PendingWslRestart"), vm.PendingRestart);
        dialogs.Verify(d => d.ShowAlertAsync(It.IsAny<string>(), It.Is<string>(m => m.Contains("shutdown is pending", StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    public static IEnumerable<object[]> GlobalConfigurationFailures()
    {
        yield return [new ConfigurationTransportException("config.write.timeout", "token=super-secret C:\\Users\\alice"), 9003];
        yield return [new ConfigurationValidationException([new(2, "config.invalidValue", "token=super-secret C:\\Users\\alice")]), 9301];
        yield return [new ConfigurationConflictException("token=super-secret C:\\Users\\alice"), 9201];
        yield return [new IOException("backup write failed: token=super-secret C:\\Users\\alice"), 5002];
    }

    [Fact]
    public void GlobalField_TracksDirtyAndCapabilityState()
    {
        var definition = new WslSettingDefinition("wsl2", "firewall", ConfigurationValueType.Boolean,
            RestartScope.Wsl, RequiredCapability: "wsl.config.firewall");
        var field = new ConfigurationSettingFieldViewModel(definition, null, false, "Requires newer WSL");
        Assert.False(field.IsDirty); Assert.False(field.IsSupported); Assert.Equal("Requires newer WSL", field.UnsupportedReason);
        field.Desired = "true"; Assert.True(field.IsDirty);
        field.CommitDesired(); Assert.False(field.IsDirty); Assert.Equal("true", field.Current);
    }

    [Fact]
    public async Task DistributionTab_PreservesAbsentTriStateAndDisablesUnsupportedSystemd()
    {
        var instanceModel = new WslInstance { Name = "Ubuntu", State = "Running", Version = 2 };
        var instance = new WslInstanceViewModel(instanceModel, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(),
            Mock.Of<ISettingsService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());
        var bytes = Encoding.UTF8.GetBytes("[custom]\nx=y\n"); var source = LosslessIniDocument.Parse(bytes);
        var service = new Mock<IDistributionConfigurationService>();
        service.Setup(s => s.ReadAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(
            new ConfigurationDocument<DistributionConfigurationSettings>(new(new Dictionary<string, string>()), source, [], 1, "fp", RestartScope.Instance, source.ToString()));
        var capability = new Mock<IPlatformCapabilityService>();
        var unavailable = new CapabilityResult(CapabilityId.InstanceSystemd, CapabilityStatus.Unavailable, "Capability.Reason.InstanceNotRunning",
            CapabilitySource.InstanceCli, DateTimeOffset.UtcNow);
        capability.Setup(c => c.GetInstanceSnapshotAsync("Ubuntu", false, It.IsAny<CancellationToken>())).ReturnsAsync(
            new InstanceCapabilitySnapshot(new("Ubuntu", 2, null, null, null, null),
                new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.InstanceSystemd] = unavailable }, DateTimeOffset.UtcNow));
        var hostWsl = new CapabilityResult(CapabilityId.Systemd, CapabilityStatus.Unavailable, "Capability.Wsl.NotInstalled", CapabilitySource.WslCli, DateTimeOffset.UtcNow);
        capability.Setup(c => c.GetHostSnapshotAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformCapabilitySnapshot(
            new("", new Version(10, 0), "x64", false, null, null, null, null, null),
            new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Systemd] = hostWsl }, new Dictionary<CapabilityId, CapabilityResult>(), DateTimeOffset.UtcNow));
        var vm = new ConfigurationTabViewModel(instance, service.Object, capability.Object, Mock.Of<IDialogService>());
        await vm.InitializeAsync();
        Assert.Null(vm.Systemd); Assert.Null(vm.AutomountEnabled); Assert.False(vm.IsSystemdSupported);
        Assert.Equal(source.ToString(), vm.DesiredRawPreview);
    }

    [Fact]
    public void GlobalCapabilityMapping_RequiresTypedSupportedResult()
    {
        var now = DateTimeOffset.UtcNow;
        var supported = new CapabilityResult(CapabilityId.ConfigFirewall, CapabilityStatus.Supported, "test", CapabilitySource.WslCli, now);
        var unknown = new CapabilityResult(CapabilityId.ConfigAutoProxy, CapabilityStatus.Unknown, "test", CapabilitySource.WslCli, now);
        var snapshot = new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null),
            new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.ConfigFirewall] = supported, [CapabilityId.ConfigAutoProxy] = unknown }, new Dictionary<CapabilityId, CapabilityResult>(), now);
        var mapped = WslConfigurationSchema.MapCapabilities(snapshot);
        Assert.Contains("wsl.config.firewall", mapped); Assert.DoesNotContain("wsl.config.autoProxy", mapped);
    }

    [Fact]
    public async Task DistributionTab_AllowsEnablingSystemdWhenHostCapabilityAndWsl2AreSupported()
    {
        var instance = new WslInstanceViewModel(new WslInstance { Name = "Ubuntu", State = "Stopped", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(),
            Mock.Of<ISettingsService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());
        var source = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes("[boot]\nsystemd=false\n"));
        var service = new Mock<IDistributionConfigurationService>();
        service.Setup(s => s.ReadAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationDocument<DistributionConfigurationSettings>(new(new Dictionary<string, string> { ["boot.systemd"] = "false" }), source, [], 0, "fp", RestartScope.Instance, source.ToString()));
        service.Setup(s => s.PreviewAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationPreview(source.ToString(), "[boot]\nsystemd=true\n", ["boot.systemd"], RestartScope.Instance));
        service.Setup(s => s.SaveAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", "/backup", RestartScope.Instance));
        var capability = new Mock<IPlatformCapabilityService>(); var now = DateTimeOffset.UtcNow;
        capability.Setup(c => c.GetInstanceSnapshotAsync("Ubuntu", false, It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceCapabilitySnapshot(new("Ubuntu", 2, null, null, null, null), new Dictionary<CapabilityId, CapabilityResult>(), now));
        capability.Setup(c => c.GetHostSnapshotAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null),
            new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Systemd] = new(CapabilityId.Systemd, CapabilityStatus.Supported, "test", CapabilitySource.WslCli, now) }, new Dictionary<CapabilityId, CapabilityResult>(), now));
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = new ConfigurationTabViewModel(instance, service.Object, capability.Object, dialogs.Object);
        await vm.InitializeAsync(); vm.Systemd = true; await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsSystemdSupported); service.Verify(s => s.SaveAsync("Ubuntu", It.Is<IReadOnlyDictionary<string, string?>>(x => x["boot.systemd"] == "true"), "fp", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DistributionTab_AllowsSystemdEnablementWhenRecordedProbeShowsSupportedWsl2ButSystemdIsOff()
    {
        var instance = NewInstance();
        var source = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes("[boot]\nsystemd=false\n"));
        var configuration = ReadableConfigurationService(source);
        configuration.Setup(s => s.PreviewAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationPreview(source.ToString(), "[boot]\nsystemd=true\n", ["boot.systemd"], RestartScope.Instance));
        configuration.Setup(s => s.SaveAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Instance));
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((ProcessRequest request, CancellationToken _) => request.Arguments switch
        {
            ["--list", "--verbose"] => Result(0, "Ubuntu Stopped 2"),
            ["--distribution", "Ubuntu", "--exec", "cat", "/etc/os-release"] => Result(0, "ID=ubuntu"),
            ["--distribution", "Ubuntu", "--exec", "systemctl", "is-system-running"] => Result(1, "", "System has not been booted with systemd as init system"),
            ["--version"] => Result(0, "WSL version: 2.4.11.0"),
            _ when request.FileName == "where.exe" => Result(1),
            _ => Result(0, "Usage: wsl.exe --install")
        });
        var dialogs = new Mock<IDialogService>(); dialogs.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = new ConfigurationTabViewModel(instance, configuration.Object, new PlatformCapabilityService(runner.Object), dialogs.Object);

        await vm.InitializeAsync(); vm.Systemd = true; await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.IsSystemdSupported);
        configuration.Verify(s => s.SaveAsync("Ubuntu", It.Is<IReadOnlyDictionary<string, string?>>(x => x["boot.systemd"] == "true"), "fp", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ConfigurationFailures))]
    public async Task DistributionTab_MapsConfigurationSaveFailuresToStructuredRedactedDialogs(Exception exception, int expectedCode)
    {
        var instance = NewInstance();
        var source = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes("[boot]\nsystemd=false\n"));
        var configuration = ReadableConfigurationService(source);
        configuration.Setup(s => s.PreviewAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>())).ThrowsAsync(exception);
        var capability = SupportedCapabilities();
        var dialogs = new Mock<IDialogService>();
        string? alert = null;
        dialogs.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((_, message) => alert = message).Returns(Task.CompletedTask);
        var vm = new ConfigurationTabViewModel(instance, configuration.Object, capability.Object, dialogs.Object);

        await vm.InitializeAsync(); vm.Systemd = true; await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(alert); Assert.Contains($"[DN-{expectedCode:D4}]", alert); Assert.DoesNotContain("token=super-secret", alert, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice", alert, StringComparison.OrdinalIgnoreCase);
        if (exception is ConfigurationValidationException) Assert.Contains("[DN-9301]", vm.Diagnostics);
    }

    [Fact]
    public async Task DistributionTab_MapsReadTimeoutToCopyableStructuredAlert()
    {
        var configuration = new Mock<IDistributionConfigurationService>();
        configuration.Setup(s => s.ReadAsync("Ubuntu", It.IsAny<CancellationToken>())).ThrowsAsync(new ConfigurationTransportException("config.read.timeout", "token=super-secret C:\\Users\\alice"));
        var dialogs = new Mock<IDialogService>(); string? alert = null;
        dialogs.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((_, message) => alert = message).Returns(Task.CompletedTask);
        var vm = new ConfigurationTabViewModel(NewInstance(), configuration.Object, SupportedCapabilities().Object, dialogs.Object);

        await vm.InitializeAsync();

        Assert.Contains("[DN-9003]", alert); Assert.DoesNotContain("super-secret", alert); Assert.DoesNotContain("alice", alert);
    }

    [Fact]
    public async Task DistributionTab_MapsReadWslOperationFailureToCopyableRedactedAlert()
    {
        var configuration = new Mock<IDistributionConfigurationService>();
        configuration.Setup(s => s.ReadAsync("Ubuntu", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));
        var (dialogs, alert) = AlertCapturingDialogs();
        var vm = new ConfigurationTabViewModel(NewInstance(), configuration.Object, SupportedCapabilities().Object, dialogs.Object);

        await vm.InitializeAsync();

        AssertRedactedWslAlert(alert.Message, 9003);
    }

    [Fact]
    public async Task DistributionTab_MapsPreviewWslOperationFailureToCopyableRedactedAlert()
    {
        var source = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes("[boot]\nsystemd=false\n"));
        var configuration = ReadableConfigurationService(source);
        configuration.Setup(s => s.PreviewAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));
        var (dialogs, alert) = AlertCapturingDialogs();
        var vm = new ConfigurationTabViewModel(NewInstance(), configuration.Object, SupportedCapabilities().Object, dialogs.Object);

        await vm.InitializeAsync(); vm.Systemd = true; await vm.SaveCommand.ExecuteAsync(null);

        AssertRedactedWslAlert(alert.Message, 9003);
    }

    [Fact]
    public async Task DistributionTab_MapsSaveWslOperationFailureToCopyableRedactedAlert()
    {
        var source = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes("[boot]\nsystemd=false\n"));
        var configuration = ReadableConfigurationService(source);
        configuration.Setup(s => s.PreviewAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationPreview(source.ToString(), "[boot]\nsystemd=true\n", ["boot.systemd"], RestartScope.Instance));
        configuration.Setup(s => s.SaveAsync("Ubuntu", It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("token=super-secret C:\\Users\\alice", DistroNexusErrorCode.OperationTimeout));
        var (dialogs, alert) = AlertCapturingDialogs(confirm: true);
        var vm = new ConfigurationTabViewModel(NewInstance(), configuration.Object, SupportedCapabilities().Object, dialogs.Object);

        await vm.InitializeAsync(); vm.Systemd = true; await vm.SaveCommand.ExecuteAsync(null);

        AssertRedactedWslAlert(alert.Message, 9003);
    }

    public static IEnumerable<object[]> ConfigurationFailures()
    {
        yield return [new ConfigurationTransportException("config.write.timeout", "token=super-secret C:\\Users\\alice"), 9003];
        yield return [new ConfigurationValidationException([new(2, "config.invalidValue", "token=super-secret C:\\Users\\alice")]), 9301];
        yield return [new ConfigurationTransportException("config.write.failed", "backup write failed: token=super-secret C:\\Users\\alice"), 5002];
        yield return [new ConfigurationConflictException("configuration changed: token=super-secret C:\\Users\\alice"), 9201];
    }

    private static WslInstanceViewModel NewInstance() => new(new WslInstance { Name = "Ubuntu", State = "Stopped", Version = 2 }, Mock.Of<IWslManagerService>(), Mock.Of<ITerminalService>(),
        Mock.Of<ISettingsService>(), Mock.Of<ILogger>(), Mock.Of<IPowerShellModuleClient>(), Mock.Of<IBackupService>(), Mock.Of<IServiceProvider>());

    private static Mock<IDistributionConfigurationService> ReadableConfigurationService(LosslessIniDocument source)
    {
        var service = new Mock<IDistributionConfigurationService>();
        service.Setup(s => s.ReadAsync("Ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationDocument<DistributionConfigurationSettings>(new(new Dictionary<string, string> { ["boot.systemd"] = "false" }), source, [], 0, "fp", RestartScope.Instance, source.ToString()));
        return service;
    }

    private static Mock<IPlatformCapabilityService> SupportedCapabilities()
    {
        var capability = new Mock<IPlatformCapabilityService>(); var now = DateTimeOffset.UtcNow;
        capability.Setup(c => c.GetInstanceSnapshotAsync("Ubuntu", false, It.IsAny<CancellationToken>())).ReturnsAsync(new InstanceCapabilitySnapshot(new("Ubuntu", 2, null, null, null, null), new Dictionary<CapabilityId, CapabilityResult>(), now));
        capability.Setup(c => c.GetHostSnapshotAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null),
            new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Systemd] = new(CapabilityId.Systemd, CapabilityStatus.Supported, "test", CapabilitySource.WslCli, now) }, new Dictionary<CapabilityId, CapabilityResult>(), now));
        return capability;
    }

    private static (WslConfigSectionViewModel ViewModel, Mock<IWslConfigurationService> Configuration, Mock<IDialogService> Dialogs, Mock<IWslManagerService> Manager)
        NewGlobalConfigurationViewModel(Mock<IWslConfigService>? legacyService = null)
    {
        legacyService ??= new Mock<IWslConfigService>();
        legacyService.Setup(s => s.GetHostSpecsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((16384L, 8));
        legacyService.Setup(s => s.GetWslConfigAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new WslConfig());
        var configuration = new Mock<IWslConfigurationService>();
        var source = LosslessIniDocument.Empty();
        configuration.Setup(s => s.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string>()), source, [], 0, "fp", RestartScope.Wsl, source.ToString()));
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var manager = new Mock<IWslManagerService>();
        manager.Setup(s => s.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var vm = new WslConfigSectionViewModel(legacyService.Object, manager.Object, dialogs.Object,
            configuration.Object, SupportedGlobalCapabilities().Object);
        return (vm, configuration, dialogs, manager);
    }

    private static Mock<IPlatformCapabilityService> SupportedGlobalCapabilities()
    {
        var capability = new Mock<IPlatformCapabilityService>(); var now = DateTimeOffset.UtcNow;
        capability.Setup(c => c.GetHostSnapshotAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(
            new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null),
                new Dictionary<CapabilityId, CapabilityResult>(), new Dictionary<CapabilityId, CapabilityResult>(), now));
        return capability;
    }

    private static void AssertAlertIsRedactedAndCoded(Mock<IDialogService> dialogs, int expectedCode)
    {
        var messages = dialogs.Invocations.Where(i => i.Method.Name == nameof(IDialogService.ShowAlertAsync))
            .Select(i => (string)i.Arguments[1]).ToArray();
        var alert = Assert.Single(messages);
        Assert.Contains($"[DN-{expectedCode:D4}]", alert);
        Assert.DoesNotContain("super-secret", alert, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice", alert, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertAlertsAreRedactedAndCoded(Mock<IDialogService> dialogs, int expectedCode)
    {
        var messages = dialogs.Invocations.Where(i => i.Method.Name == nameof(IDialogService.ShowAlertAsync))
            .Select(i => (string)i.Arguments[1]).ToArray();
        Assert.NotEmpty(messages);
        Assert.All(messages, alert =>
        {
            Assert.Contains($"[DN-{expectedCode:D4}]", alert);
            Assert.DoesNotContain("super-secret", alert, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alice", alert, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static (Mock<IDialogService> Dialogs, AlertCapture Alert) AlertCapturingDialogs(bool confirm = false)
    {
        var alert = new AlertCapture();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(confirm);
        dialogs.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, message) => alert.Message = message).Returns(Task.CompletedTask);
        return (dialogs, alert);
    }

    private static void AssertRedactedWslAlert(string? alert, int expectedCode)
    {
        Assert.NotNull(alert); Assert.Contains($"[DN-{expectedCode:D4}]", alert);
        Assert.DoesNotContain("super-secret", alert, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice", alert, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AlertCapture
    {
        public string? Message { get; set; }
    }

    private static ProcessResult Result(int exitCode, string output = "", string error = "") => new(exitCode, output, error, TimeSpan.Zero, false, false, false, 1);
}
