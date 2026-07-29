namespace DistroNexus.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public void ImportRoute_ExecutesOnlyTheConfirmationPreviewToken()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md"))) root = Directory.GetParent(root)!.FullName;
        var source = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "ViewModels", "MainViewModel.cs"));

        Assert.Contains("new ImportInstanceViewModel(existingNames, _moduleClient)", source, StringComparison.Ordinal);
        Assert.Contains("vm.ImportPreview ?? throw", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteLifecycleOperationAsync(preview.PreviewToken)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewImportInstanceAsync(vm.", source, StringComparison.Ordinal);
    }
}
