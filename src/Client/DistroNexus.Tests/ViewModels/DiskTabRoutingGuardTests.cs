namespace DistroNexus.Tests.ViewModels;

public sealed class DiskTabRoutingGuardTests
{
    [Fact]
    public void DiskTab_UsesOnlyTypedCompactionAndRendersTruthfulResultFields()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "ViewModels", "Tabs", "DiskTabViewModel.cs"));

        Assert.DoesNotContain("IWslManagerService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetInstanceDiskSizeAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowConfirmAsync", source, StringComparison.Ordinal);
        Assert.Contains("_moduleClient.CompactInstanceAsync", source, StringComparison.Ordinal);
        Assert.Contains("result.BeforeBytes", source, StringComparison.Ordinal);
        Assert.Contains("result.AfterBytes", source, StringComparison.Ordinal);
        Assert.Contains("result.SavedBytes", source, StringComparison.Ordinal);
        Assert.Contains("EstimateKind = result.SavedBytes.HasValue ? \"Measured\" : \"Unknown\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiskTabView_DoesNotRenderVhdxPath()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Controls", "Tabs", "DiskTabView.xaml"));
        Assert.DoesNotContain("VhdxPath", source, StringComparison.Ordinal);
        Assert.Contains("EstimateKind", source, StringComparison.Ordinal);
        Assert.Contains("WarningsDisplay", source, StringComparison.Ordinal);
    }
}
