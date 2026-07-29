using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Reads the two Windows features required by WSL2 plus firmware virtualization.
/// Commands and feature names are fixed allow-listed constants, never user input.</summary>
public sealed class WindowsPrerequisiteProbe : IWindowsPrerequisiteProbe
{
    public static readonly string[] RequiredFeatures = ["Microsoft-Windows-Subsystem-Linux", "VirtualMachinePlatform"];
    private readonly IProcessRunner _runner;
    public WindowsPrerequisiteProbe(IProcessRunner runner) => _runner = runner;
    public async Task<WindowsPrerequisiteSnapshot> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var states = new List<WindowsOptionalFeatureState>();
        foreach (var feature in RequiredFeatures)
        {
            var result = await _runner.RunAsync(new ProcessRequest("dism.exe", ["/Online", "/Get-FeatureInfo", "/FeatureName:" + feature], TimeSpan.FromSeconds(20)), cancellationToken).ConfigureAwait(false);
            var text = result.StandardOutput + "\n" + result.StandardError;
            bool? enabled = result.ExitCode == 0 ? text.Contains("State : Enabled", StringComparison.OrdinalIgnoreCase) : null;
            states.Add(new WindowsOptionalFeatureState(feature, enabled, enabled is null ? "Windows optional-feature state could not be read." : enabled.Value ? "Enabled." : "Disabled."));
        }
        var system = await _runner.RunAsync(new ProcessRequest("systeminfo.exe", [], TimeSpan.FromSeconds(20)), cancellationToken).ConfigureAwait(false);
        var all = system.StandardOutput + "\n" + system.StandardError;
        bool? virtualization = system.ExitCode == 0
            ? all.Contains("Virtualization Enabled In Firmware: Yes", StringComparison.OrdinalIgnoreCase)
                ? true : all.Contains("Virtualization Enabled In Firmware: No", StringComparison.OrdinalIgnoreCase) ? false : null
            : null;
        return new WindowsPrerequisiteSnapshot(states, virtualization, virtualization is null ? "Firmware virtualization state could not be read." : virtualization.Value ? "Firmware virtualization is enabled." : "Firmware virtualization is disabled.");
    }
}

public sealed class WindowsPrerequisiteHealthCheck : IHealthCheck
{
    private readonly IWindowsPrerequisiteProbe _probe;
    public WindowsPrerequisiteHealthCheck(IWindowsPrerequisiteProbe probe) => _probe = probe;
    public HealthCheckDescriptor Descriptor { get; } = new("host.windows-prerequisites", HealthScope.Host, []);
    public async Task<HealthCheckResult> CheckAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var snapshot = await _probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
        var missing = snapshot.OptionalFeatures.Where(x => x.Enabled == false).Select(x => x.FeatureName).ToArray();
        var findings = snapshot.OptionalFeatures.Select(feature => new HealthFinding("windows.feature." + feature.FeatureName,
            feature.Enabled == true ? HealthSeverity.Healthy : feature.Enabled == false ? HealthSeverity.Warning : HealthSeverity.Information,
            HealthScope.Host, "Windows optional feature: " + feature.FeatureName, feature.Detail,
            RepairId: feature.Enabled == false ? "enable.windows-features" : null,
            Evidence: feature.Enabled == false ? new Dictionary<string, string> { ["feature"] = feature.FeatureName } : null)).ToList();
        findings.Add(new HealthFinding("windows.virtualization.firmware", snapshot.VirtualizationEnabled == true ? HealthSeverity.Healthy : snapshot.VirtualizationEnabled == false ? HealthSeverity.Warning : HealthSeverity.Information,
            HealthScope.Host, "Firmware virtualization", snapshot.VirtualizationDetail,
            RepairId: snapshot.VirtualizationEnabled == false ? "open.windows-virtualization-settings" : null));
        return new HealthCheckResult(Descriptor.Id, findings, DateTimeOffset.UtcNow);
    }
}
