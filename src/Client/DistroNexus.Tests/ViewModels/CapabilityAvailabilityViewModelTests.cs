using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using System.Globalization;

namespace DistroNexus.Tests.ViewModels;

public class CapabilityAvailabilityViewModelTests
{
    [Fact]
    public void CapabilityReasonAndSafeActionResources_HaveEnglishAndChineseParity()
    {
        string[] keys =
        [
            "Capability.Dependency.NotInstalled", "Capability.Dependency.ProbeUnavailable", "Capability.Dependency.RequiresElevation",
            "Capability.Dependency.VersionDetected", "Capability.Dependency.VersionMalformed", "Capability.Dependency.VersionProbeFailed",
            "Capability.Feature.CliSupported", "Capability.Feature.MalformedHelp", "Capability.Feature.NotAdvertisedByCli", "Capability.Feature.WslUnavailable",
            "Capability.Gpu.RequiresRuntimeProbe", "Capability.Instance.DistributionAbsent", "Capability.Instance.IdentityDetected",
            "Capability.Instance.SystemdDisabled", "Capability.Instance.SystemdMalformed", "Capability.Instance.SystemdNotRunning",
            "Capability.Instance.SystemdPermissionDenied", "Capability.Instance.SystemdProbeFailed", "Capability.Instance.SystemdRunning",
            "Capability.Instance.SystemdUnavailable", "Capability.Instance.VersionDetected", "Capability.Instance.VersionMalformed",
            "Capability.MirroredNetworking.RequiresVersionMatrix", "Capability.Probe.Cancelled", "Capability.Probe.ExecutableMissing",
            "Capability.Probe.Failed", "Capability.Probe.RequiresElevation", "Capability.Probe.Supported", "Capability.Probe.TimedOut",
            "Capability.Probe.UnexpectedFailure", "Capability.Systemd.RequiresInstanceProbe", "Capability.TaskScheduler.Available",
            "Capability.UsbIpd.VersionRequiresValidation", "Capability.Wsl.UpdateAvailable", "Capability.Wslg.NotReported", "Capability.Wslg.Supported",
            "Capability.Action.None", "Capability.Action.RunElevated", "Capability.Action.UpdateWsl",
            "Capability.Action.InstallPrerequisite", "Capability.Action.ReviewRequirements", "Capability.Action.RetryProbe"
        ];

        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-US"))), key + " en-US");
            Assert.False(string.IsNullOrWhiteSpace(DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("zh-CN"))), key + " zh-CN");
        }
    }

    [Theory]
    [InlineData(CapabilityStatus.Unsupported, "Capability.Action.ReviewRequirements")]
    [InlineData(CapabilityStatus.Unavailable, "Capability.Action.InstallPrerequisite")]
    [InlineData(CapabilityStatus.RequiresUpdate, "Capability.Action.UpdateWsl")]
    [InlineData(CapabilityStatus.RequiresElevation, "Capability.Action.RunElevated")]
    [InlineData(CapabilityStatus.Unknown, "Capability.Action.RetryProbe")]
    public void Apply_DisablesUnsupportedActionAndExplainsSafeNextStep(CapabilityStatus status, string expectedAction)
    {
        var vm = new CapabilityAvailabilityViewModel(key => "localized:" + key);
        var checkedAt = DateTimeOffset.Parse("2026-07-11T10:00:00Z");

        vm.Apply(new CapabilityResult(CapabilityId.Systemd, status, "Capability.Systemd.Prerequisite", CapabilitySource.InstanceCli, checkedAt));

        Assert.False(vm.IsLoading);
        Assert.False(vm.IsEnabled);
        Assert.Equal("localized:Capability.Systemd.Prerequisite", vm.Reason);
        Assert.Equal("localized:" + expectedAction, vm.SafeNextAction);
        Assert.Equal(checkedAt, vm.RefreshedAt);
    }

    [Fact]
    public void Apply_SupportedEnablesAction_AndRestartStateKeepsCurrentAndDesiredDistinct()
    {
        var vm = new CapabilityAvailabilityViewModel(key => key);
        vm.Apply(new CapabilityResult(CapabilityId.Wsl, CapabilityStatus.Supported, "supported", CapabilitySource.WslCli, DateTimeOffset.UtcNow));
        vm.MarkPendingRestart("current.off", "desired.on");

        Assert.True(vm.IsEnabled);
        Assert.True(vm.IsPendingRestart);
        Assert.Equal("current.off", vm.CurrentState);
        Assert.Equal("desired.on", vm.DesiredState);
    }

    [Fact]
    public void CapabilityTab_RemainsDiscoverableButDisabledWithExplanation()
    {
        var tab = new CapabilityTabItemViewModel("services", "Services", localize: key => "L:" + key);
        tab.Apply(new CapabilityResult(CapabilityId.InstanceSystemd, CapabilityStatus.Unavailable,
            "Capability.Instance.SystemdUnavailable", CapabilitySource.InstanceCli, DateTimeOffset.UtcNow));

        Assert.True(tab.IsDiscoverable);
        Assert.False(tab.IsEnabled);
        Assert.Contains("L:Capability.Instance.SystemdUnavailable", tab.UnavailableExplanation);
        Assert.Contains("L:Capability.Action.InstallPrerequisite", tab.UnavailableExplanation);
    }
}
