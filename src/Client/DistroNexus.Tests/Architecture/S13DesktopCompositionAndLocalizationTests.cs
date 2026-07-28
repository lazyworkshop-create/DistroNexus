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
    public void DesktopComposition_RetainsOnlyTheTypedModuleTransportForNamedShellSurfaces()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "App.xaml.cs"));
        Assert.Equal(1, CountOccurrences(app, "IPowerShellService"));
        Assert.Contains("services.AddSingleton<IPowerShellService>(sp => new PowerShellService", app, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<IPowerShellModuleClient, PowerShellModuleClient>();", app, StringComparison.Ordinal);
        foreach (var retired in new[]
        {
            "IWslManagerService", "ICatalogService", "IWslConfigService", "INetworkService", "ISystemdService",
            "INetworkDiagnosticsService", "IFirewallOperationBroker", "INetworkConfigurationService", "IWslEventWatcher"
        })
            Assert.DoesNotContain(retired, app, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedDesktopSources_HaveNoRawPowerShellOrCoreBusinessServiceDependency()
    {
        var root = FindRepositoryRoot();
        var relative = new[] { "App.xaml.cs", "ViewModels/MainViewModel.cs", "ViewModels/WslInstanceViewModel.cs", "ViewModels/InstanceDetailViewModel.cs", "ViewModels/SettingsViewModel.cs" };
        var sources = relative.ToDictionary(path => path, path => File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", path)));
        var forbidden = new[] { "IPowerShellService", "IWslManagerService", "ICatalogService", "IWslConfigService", "INetworkService", "ISystemdService", "INetworkDiagnosticsService", "IFirewallOperationBroker", "INetworkConfigurationService", "INetworkStatusAdapter", "IBrowserLauncher", "IWslEventWatcher", "diagnostic.snapshot.v1", "System.IO.", "File.Read", "File.Write", "Directory." };
        foreach (var (path, source) in sources)
        {
            foreach (var prohibited in forbidden)
            {
                if (path == "App.xaml.cs" && prohibited == "IPowerShellService") continue;
                Assert.DoesNotContain(prohibited, source, StringComparison.Ordinal);
            }
            Assert.DoesNotContain("GetDiagnosticInfoAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("catch {", source, StringComparison.Ordinal);
        }

        var outstanding = new Dictionary<string, string>
        {
            ["ViewModels/TemplatesViewModel.cs"] = "File.ReadAllTextAsync(dialog.FileName)",
            ["Wizard/Steps/ProgressStep.cs"] = "Context.LogFilePath = Path.Combine",
            ["Wizard/Steps/ResultStep.cs"] = "Directory.CreateDirectory(logFolder)",
            ["Controls/Tabs/IntegrationsTabView.xaml.cs"] = "Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)"
        };
        foreach (var (path, exactPattern) in outstanding)
        {
            var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", path));
            Assert.Contains(exactPattern, source, StringComparison.Ordinal);
        }
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

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        for (var index = value.IndexOf(pattern, StringComparison.Ordinal); index >= 0; index = value.IndexOf(pattern, index + pattern.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static string FindRepositoryRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "AGENTS.md")))
            path = Directory.GetParent(path)?.FullName ?? throw new DirectoryNotFoundException();
        return path;
    }
}
