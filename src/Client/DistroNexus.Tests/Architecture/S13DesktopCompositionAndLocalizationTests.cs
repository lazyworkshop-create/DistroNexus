using System.Xml.Linq;

namespace DistroNexus.Tests.Architecture;

/// <summary>Cross-surface contracts that do not require starting a WPF application or WSL.</summary>
public sealed class S13DesktopCompositionAndLocalizationTests
{
    [Fact]
    public void ResourceCatalogs_HaveExactBidirectionalParity()
    {
        var root = FindRepositoryRoot();
        var neutral = ReadNames(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Properties", "Resources.resx"));
        var zhCn = ReadNames(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Properties", "Resources.zh-CN.resx"));

        Assert.Empty(neutral.Except(zhCn, StringComparer.Ordinal).OrderBy(x => x));
        Assert.Empty(zhCn.Except(neutral, StringComparer.Ordinal).OrderBy(x => x));
    }

    [Fact]
    public void DesktopComposition_RegistersEveryV23TopLevelSurfaceAndItsCoreServices()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "App.xaml.cs"));
        foreach (var registration in new[]
        {
            "services.AddPlatformCapabilities()", "services.AddHealthCenter()",
            "IRecoveryPointService", "IMonitoringService", "IUsbDeviceService", "IWorkspaceService",
            "IContainerRuntimeService", "ITemplateMarketplaceService", "HealthCenterViewModel",
            "UsbDevicesViewModel", "WorkspacesViewModel", "ApplicationsViewModel", "HealthCenterPage",
            "UsbDevicesPage", "WorkspacesPage", "ApplicationsPage"
        })
            Assert.Contains(registration, app, StringComparison.Ordinal);
        Assert.DoesNotContain("services.AddWslgApplications()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomationHealthBridge_ComposesEveryDesktopHealthCategoryWithConcreteCoreChecks()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.WorkspaceBridge", "Program.cs"));
        foreach (var category in new[]
        {
            "InitialProbeHealthCheck", "DefaultHealthProbe", "HealthRuntimeAdapter", "CapabilityHealthCheck",
            "WindowsPrerequisiteHealthCheck", "WindowsPrerequisiteProbe", "StorageHealthCheck", "IntegrationHealthCheck", "NetworkHealthCheck",
            "SystemdHealthCheck", "WslgHealthCheck", "GlobalConfigurationHealthCheck",
            "DistributionConfigurationHealthCheck", "BackupHealthCheck", "TemplateHealthCheck",
            "TemplateRuntimePreflightEvaluator", "MonitoringHealthCheck", "BridgeReadOnlyPowerShellService"
        })
            Assert.Contains(category, program, StringComparison.Ordinal);
    }

    private static HashSet<string> ReadNames(string path) => XDocument.Load(path)
        .Root!.Elements("data").Select(x => (string?)x.Attribute("name"))
        .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToHashSet(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "AGENTS.md")))
            path = Directory.GetParent(path)?.FullName ?? throw new DirectoryNotFoundException();
        return path;
    }
}
