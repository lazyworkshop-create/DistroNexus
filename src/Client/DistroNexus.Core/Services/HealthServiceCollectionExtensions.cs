using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DistroNexus.Core.Services;

public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddHealthCenter(this IServiceCollection services)
    {
        // Health Center can be composed on its own (for example in a command-line host or a
        // focused test).  Desktop may register the same shared registry first; TryAdd keeps
        // both health scans and monitor sessions attached to that single projection.
        services.TryAddSingleton<MonitoringWarningRegistry>();
        services.TryAddSingleton<IMonitoringWarningSource>(sp => sp.GetRequiredService<MonitoringWarningRegistry>());
        services.TryAddSingleton<IMonitoringWarningSink>(sp => sp.GetRequiredService<MonitoringWarningRegistry>());
        services.AddSingleton<IHealthRuntimeAdapter, HealthRuntimeAdapter>();
        services.AddSingleton<ISystemdService, SystemdService>();
        services.AddSingleton<IWslNetworkDiagnosticsAdapter, WslNetworkDiagnosticsAdapter>();
        services.AddSingleton<INetworkDiagnosticsService, NetworkDiagnosticsService>();
        services.AddSingleton<INetworkConfigurationService, NetworkConfigurationService>();
        services.AddSingleton<INetworkStatusAdapter, WindowsNetworkStatusAdapter>();
        services.AddSingleton<IFirewallOperationBroker, GuardedFirewallOperationBroker>();
        // Health checks only replay the deliberately restricted, read-only runtime
        // contracts persisted with a template application record.
        services.AddSingleton<ITemplateRuntimePreflightEvaluator, TemplateRuntimePreflightEvaluator>();
        services.AddSingleton<ILocalhostForwardingEndpointStrategy, SettingsLocalhostForwardingEndpointStrategy>();
        services.AddSingleton<IWindowsPrerequisiteProbe, WindowsPrerequisiteProbe>();
        services.AddSingleton<IBackupHealthSource, BackupHealthSource>();
        services.AddSingleton<IHealthProbe, DefaultHealthProbe>();
        services.AddSingleton<IHealthCheck, InitialProbeHealthCheck>();
        services.AddSingleton<IHealthCheck, CapabilityHealthCheck>();
        services.AddSingleton<IHealthCheck, WindowsPrerequisiteHealthCheck>();
        services.AddSingleton<IHealthCheck, GlobalConfigurationHealthCheck>();
        services.AddSingleton<IHealthCheck, DistributionConfigurationHealthCheck>();
        services.AddSingleton<IHealthCheck, StorageHealthCheck>();
        services.AddSingleton<IHealthCheck, BackupHealthCheck>();
        services.AddSingleton<IHealthCheck, IntegrationHealthCheck>();
        services.AddSingleton<IHealthCheck, NetworkHealthCheck>();
        services.AddSingleton<IHealthCheck, SystemdHealthCheck>();
        services.AddSingleton<IHealthCheck, TemplateHealthCheck>();
        services.AddSingleton<IHealthCheck, MonitoringHealthCheck>();
        services.AddSingleton<IHealthOrchestrator, HealthOrchestrator>();
        services.AddSingleton<IHealthNavigationBroker, NullHealthNavigationBroker>();
        services.AddSingleton<IWindowsFeatureRepairBroker, ElevatedWindowsFeatureRepairBroker>();
        services.AddSingleton<IRepairAction>(sp => new OpenSettingsRepairAction("open.wsl-update", sp.GetRequiredService<IHealthNavigationBroker>()));
        services.AddSingleton<IRepairAction>(sp => new OpenSettingsRepairAction("open.windows-virtualization-settings", sp.GetRequiredService<IHealthNavigationBroker>()));
        services.AddSingleton<IRepairAction, GlobalConfigurationRepairAction>();
        services.AddSingleton<IRepairAction, InstanceConfigurationRepairAction>();
        services.AddSingleton<IRepairAction>(sp => new FixedProcessRepairAction("wsl.update", "Update WSL", RepairSafety.RequiresConfirmation, RepairIdempotency.Idempotent,
            ["Download and install the latest available WSL update."], _ => new ProcessRequest("wsl.exe", ["--update"], TimeSpan.FromMinutes(5)), sp.GetRequiredService<IProcessRunner>(),
            _ => new ProcessRequest("wsl.exe", ["--version"], TimeSpan.FromSeconds(30))));
        services.AddSingleton<IRepairAction, WslRestartRepairAction>();
        services.AddSingleton<IRepairAction>(sp => new FixedProcessRepairAction("wsl.trim", "Trim Linux filesystem", RepairSafety.PrivilegedOrDisruptive, RepairIdempotency.Idempotent,
            ["Run fstrim in the selected running distribution. Linux privilege policy may reject this operation."], finding => string.IsNullOrWhiteSpace(finding.InstanceName) ? null : new ProcessRequest("wsl.exe", ["--distribution", finding.InstanceName, "--", "sudo", "--non-interactive", "fstrim", "-av"], TimeSpan.FromMinutes(2)), sp.GetRequiredService<IProcessRunner>(),
            finding => string.IsNullOrWhiteSpace(finding.InstanceName) ? null : new ProcessRequest("wsl.exe", ["--distribution", finding.InstanceName, "--", "sh", "-lc", "df -Pk /"], TimeSpan.FromSeconds(30))));
        services.AddSingleton<IRepairAction, ElevationRequiredRepairAction>();
        services.AddSingleton<IHealthRepairService, HealthRepairService>();
        services.AddSingleton<ApplicationDiagnosticLogProvider>();
        services.AddSingleton<IDiagnosticLogProvider>(sp => sp.GetRequiredService<ApplicationDiagnosticLogProvider>());
        services.AddSingleton<IStructuredErrorProvider, StructuredFileErrorProvider>();
        services.AddSingleton<IDiagnosticReportService, DiagnosticReportService>();
        return services;
    }
}
