namespace DistroNexus.Tests.ViewModels;

public sealed class CatalogReadRoutingGuardTests
{
    [Theory]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs", "_moduleClient.GetPackagesAsync()")]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs", "_moduleClient.SearchPackagesAsync(SearchQuery)")]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs", "_moduleClient.GetPackagesAsync(forceReload: true)")]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/SettingsViewModel.cs", "_moduleClient.GetPackagesAsync()")]
    [InlineData("src/Client/DistroNexus.Desktop/ViewModels/InstallWizardViewModel.cs", "_moduleClient.GetPackagesAsync()")]
    [InlineData("src/Client/DistroNexus.Desktop/Wizard/Steps/SelectDistributionStep.cs", "_moduleClient.GetPackagesAsync()")]
    public void CatalogReadHandler_UsesTheClosedModuleClient(string relativePath, string requiredCall)
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains(requiredCall, source, StringComparison.Ordinal);
    }
}
