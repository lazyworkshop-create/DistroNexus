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

    [Fact]
    public void PackageManagerRefreshHandlers_UseOneModuleRefreshAndNoCatalogServiceRefresh()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "ViewModels", "PackageManagerViewModel.cs"));
        Assert.DoesNotContain("_catalogService.RefreshCatalogAsync", source, StringComparison.Ordinal);
        Assert.Equal(2, Count(source, "await _moduleClient.RefreshCatalogAsync()"));
        Assert.Equal(1, CountMethodCall(source, "private async Task RefreshCatalogAsync", "await _moduleClient.RefreshCatalogAsync()"));
        Assert.Equal(1, CountMethodCall(source, "private async Task UpdateSourcesAsync", "await _moduleClient.RefreshCatalogAsync()"));
    }

    private static int Count(string source, string text) => source.Split(text, StringSplitOptions.None).Length - 1;
    private static int CountMethodCall(string source, string signature, string call)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        var end = source.IndexOf("\n    private", start + signature.Length, StringComparison.Ordinal);
        return Count(source[start..(end < 0 ? source.Length : end)], call);
    }
}
