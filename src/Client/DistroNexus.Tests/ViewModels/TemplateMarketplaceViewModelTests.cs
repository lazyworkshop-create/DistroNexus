using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class TemplateMarketplaceViewModelTests
{
    [Fact]
    public async Task ApproveCandidate_DeclinedReviewDoesNotPromoteAndShowsBoundedDiff()
    {
        var marketplace = Marketplace();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var vm = ViewModel(marketplace.Object, dialogs.Object);
        var artifact = History();
        vm.SelectedMarketplaceSource = Source();
        vm.SelectedMarketplaceArtifact = artifact;

        await vm.ApproveMarketplaceCandidateCommand.ExecuteAsync(null);

        marketplace.Verify(x => x.CreateReviewGrantAsync("source", artifact.Artifact.Sha256, It.IsAny<CancellationToken>()), Times.Once);
        marketplace.Verify(x => x.ApproveCandidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("new.ps1", vm.MarketplaceDiffAddedDisplay);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.Is<string>(text => text.Contains("new.ps1") && text.Contains("old text") && text.Contains("Truncated", StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task ApproveCandidate_ConfirmedReviewPromotesExactCandidateAndFormatsFields()
    {
        var marketplace = Marketplace();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = ViewModel(marketplace.Object, dialogs.Object);
        var artifact = History();
        vm.SelectedMarketplaceSource = Source();
        vm.SelectedMarketplaceArtifact = artifact;

        await vm.ApproveMarketplaceCandidateCommand.ExecuteAsync(null);

        marketplace.Verify(x => x.ApproveCandidateAsync("grant", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("NetworkAccess, Root", vm.MarketplaceCapabilitiesDisplay);
        Assert.Equal("scripts/setup.ps1: abc", vm.MarketplaceScriptsDisplay);
        Assert.Equal("WSL 2", vm.MarketplaceCompatibilityDisplay);
        Assert.Equal("check", vm.MarketplaceHealthDisplay);
        Assert.NotEmpty(vm.MarketplaceSignatureVerificationDisplay);
        Assert.NotEmpty(vm.MarketplaceTrustStateDisplay);
    }

    [Fact]
    public async Task MarketplaceStatus_DisplaysCoreVerifiedSignatureForApprovedCandidate()
    {
        var signed = Manifest() with { PublisherPublicKey = Convert.ToBase64String(new byte[32]), PublisherSignature = Convert.ToBase64String(new byte[64]) };
        var marketplace = Marketplace(signed);
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = ViewModel(marketplace.Object, dialogs.Object);
        vm.SelectedMarketplaceSource = Source();
        vm.SelectedMarketplaceArtifact = History(signed);

        await vm.ApproveMarketplaceCandidateCommand.ExecuteAsync(null);

        Assert.Equal("Verified", vm.MarketplaceSignatureVerificationDisplay);
        Assert.Equal("Review required", vm.MarketplaceTrustStateDisplay);
    }

    [Fact]
    public async Task MarketplaceStatus_DisplaysUntrustedStateForDisabledSource()
    {
        var marketplace = Marketplace();
        marketplace.Setup(x => x.GetStatusAsync("source", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateMarketplaceStatus(null, TemplateSignatureStatus.NotPresent, TemplateTrustState.Untrusted, false, false, "Source unavailable"));
        var vm = ViewModel(marketplace.Object, new Mock<IDialogService>().Object);
        vm.SelectedMarketplaceSource = Source() with { IsEnabled = false };
        await Task.Delay(25);

        Assert.Equal("Not supplied", vm.MarketplaceSignatureVerificationDisplay);
        Assert.Equal("Unavailable", vm.MarketplaceTrustStateDisplay);
    }

    [Fact]
    public async Task MarketplaceStatus_DisplaysInvalidSignatureWhenCoreRejectsManifest()
    {
        var marketplace = Marketplace();
        marketplace.Setup(x => x.FetchManifestAsync("source", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("signature invalid", DistroNexusErrorCode.TemplateArtifactIntegrityFailed));
        var vm = ViewModel(marketplace.Object, new Mock<IDialogService>().Object);
        vm.SelectedMarketplaceSource = Source();

        await vm.DownloadMarketplaceArtifactCommand.ExecuteAsync(null);

        Assert.Equal("Invalid", vm.MarketplaceSignatureVerificationDisplay);
        Assert.Equal("Unavailable", vm.MarketplaceTrustStateDisplay);
    }

    [Fact]
    public async Task SourceLifecycleAndRollback_RequireConfirmationThenCallExactServiceMethods()
    {
        var marketplace = Marketplace();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = ViewModel(marketplace.Object, dialogs.Object);
        var source = Source(); var artifact = History();
        vm.SelectedMarketplaceSource = source;
        vm.SelectedMarketplaceArtifact = artifact;

        await vm.SetMarketplaceSourceEnabledCommand.ExecuteAsync(false);
        await vm.RollbackMarketplaceArtifactCommand.ExecuteAsync(null);

        marketplace.Verify(x => x.SetSourceEnabledAsync("source", false, It.IsAny<CancellationToken>()), Times.Once);
        marketplace.Verify(x => x.RollbackAsync("demo", artifact.Artifact.Sha256, It.IsAny<CancellationToken>()), Times.Once);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task BuiltInSource_IsVisibleButNeverOffersLifecycleCrud()
    {
        var builtIn = new TemplateSource("builtin", "distronexus://built-in", TemplateSourceKind.BuiltIn, "DistroNexus");
        var marketplace = Marketplace();
        marketplace.Setup(x => x.GetSourcesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([builtIn]);
        var dialogs = new Mock<IDialogService>();
        var vm = ViewModel(marketplace.Object, dialogs.Object);

        await vm.LoadMarketplaceCommand.ExecuteAsync(null);
        Assert.Contains(vm.MarketplaceSources, x => x.Kind == TemplateSourceKind.BuiltIn);
        vm.SelectedMarketplaceSource = builtIn;
        await vm.SetMarketplaceSourceEnabledCommand.ExecuteAsync(false);
        await vm.RemoveMarketplaceSourceCommand.ExecuteAsync(null);

        marketplace.Verify(x => x.SetSourceEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        marketplace.Verify(x => x.RemoveSourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddSource_ErrorUsesFormattedCoreErrorInsteadOfRawException()
    {
        var marketplace = Marketplace();
        marketplace.Setup(x => x.AddSourceAsync(It.IsAny<string>(), It.IsAny<TemplateSourceKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WslOperationFailedException("token=not-displayed", DistroNexusErrorCode.TemplateTrustRequired));
        var vm = ViewModel(marketplace.Object, new Mock<IDialogService>().Object);
        vm.MarketplaceSourceUrl = "https://catalog.example.test/template.json";

        await vm.AddMarketplaceSourceCommand.ExecuteAsync(null);

        Assert.Contains("DN-", vm.MarketplaceStatus);
        Assert.DoesNotContain("token=not-displayed", vm.MarketplaceStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddUserLocalSource_RequiresLocalizedConfirmationAndUsesUserLocalKind()
    {
        var marketplace = Marketplace();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = ViewModel(marketplace.Object, dialogs.Object);
        vm.MarketplaceSourceKind = "UserLocal"; vm.MarketplaceSourceUrl = "file:///C:/templates/catalog.json";

        await vm.AddMarketplaceSourceCommand.ExecuteAsync(null);

        marketplace.Verify(x => x.AddSourceAsync("file:///C:/templates/catalog.json", TemplateSourceKind.UserLocal, true, It.IsAny<CancellationToken>()), Times.Once);
        dialogs.Verify(x => x.ShowConfirmAsync(It.IsAny<string>(), It.Is<string>(text => text.Contains("file:///", StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task ReviewUpdate_UsesSelectedSecondCatalogEntryIdentity()
    {
        var marketplace = Marketplace(); var source = Source();
        var second = new TemplateMarketplaceEntry(Manifest() with { Id = "second" }, source, TemplateTrustState.Untrusted, null, false, "Review", "second-digest");
        marketplace.Setup(x => x.ReviewUpdateAsync("source", "second", "second-digest", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateUpdateReview("second", "old", "new", [], true, false, true));
        var vm = ViewModel(marketplace.Object, new Mock<IDialogService>().Object);
        vm.SelectedMarketplaceSource = source; vm.SelectedMarketplaceEntry = second;

        await vm.ReviewMarketplaceUpdateCommand.ExecuteAsync(null);

        marketplace.Verify(x => x.ReviewUpdateAsync("source", "second", "second-digest", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarketplaceBrowseSearchAndSelection_UsesSelectedExactCatalogEntry()
    {
        var marketplace = Marketplace();
        var source = Source();
        var first = new TemplateMarketplaceEntry(Manifest() with { Id = "first", Name = "First template", Version = "1" }, source, TemplateTrustState.Untrusted, null, false, "Review", "first-digest");
        var second = new TemplateMarketplaceEntry(Manifest() with { Id = "second", Name = "Second template", Version = "2" }, source, TemplateTrustState.Untrusted, null, false, "Review", "second-digest");
        marketplace.Setup(x => x.DiscoverAsync(It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        marketplace.Setup(x => x.GetStatusAsync("source", "first", "first-digest", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateMarketplaceStatus(first.Manifest, TemplateSignatureStatus.NotPresent, TemplateTrustState.Untrusted, false, false, "Review"));
        marketplace.Setup(x => x.GetStatusAsync("source", "second", "second-digest", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateMarketplaceStatus(second.Manifest, TemplateSignatureStatus.Verified, TemplateTrustState.ReviewRequired, false, false, "Review"));
        var vm = ViewModel(marketplace.Object, new Mock<IDialogService>().Object);
        vm.SelectedMarketplaceSource = source;
        await Task.Delay(50);
        Assert.Equal(2, vm.MarketplaceEntries.Count);
        vm.MarketplaceSearchQuery = "second";
        Assert.Single(vm.MarketplaceEntries);
        Assert.Equal("second", vm.SelectedMarketplaceEntry!.Manifest.Id);
        Assert.Equal("second", vm.SelectedMarketplaceManifest!.Id);
        await Task.Delay(25);
        Assert.Equal("Verified", vm.MarketplaceSignatureVerificationDisplay);
        marketplace.Verify(x => x.GetStatusAsync("source", "second", "second-digest", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static TemplatesViewModel ViewModel(ITemplateMarketplaceService marketplace, IDialogService dialogs)
    {
        var templates = new Mock<ITemplateService>();
        templates.Setup(x => x.LoadTemplatesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        templates.Setup(x => x.GetTemplateScriptsPath()).Returns("templates");
        return new TemplatesViewModel(templates.Object, new Mock<INavigationService>().Object, NullLogger<TemplatesViewModel>.Instance, new Mock<IServiceProvider>().Object, marketplace, dialogs);
    }
    private static TemplateSource Source() => new("source", "https://catalog.example.test/template.json", TemplateSourceKind.Remote);
    private static TemplateArtifactHistoryEntry History(TemplateManifestV2? manifest = null) => new(manifest ?? Manifest(), new TemplateArtifact(new string('a', 64), "artifact", DateTimeOffset.UtcNow, "demo"), DateTimeOffset.UtcNow);
    private static TemplateManifestV2 Manifest() => new() { Id = "demo", Name = "Demo", Version = "1", ArtifactUrl = "https://catalog.example.test/demo.zip", ArtifactSha256 = new string('a', 64), PublisherFingerprint = "fingerprint", Capabilities = [TemplateCapability.NetworkAccess, TemplateCapability.Root], ScriptHashes = ["scripts/setup.ps1: abc"], Compatibility = "WSL 2", HealthChecks = ["check"] };
    private static Mock<ITemplateMarketplaceService> Marketplace(TemplateManifestV2? manifest = null)
    {
        var candidate = manifest ?? Manifest();
        var result = new Mock<ITemplateMarketplaceService>();
        result.Setup(x => x.FetchManifestAsync("source", It.IsAny<CancellationToken>())).ReturnsAsync(candidate);
        result.Setup(x => x.GetStatusAsync("source", It.IsAny<CancellationToken>())).ReturnsAsync(() => new TemplateMarketplaceStatus(candidate, string.IsNullOrWhiteSpace(candidate.PublisherSignature) ? TemplateSignatureStatus.NotPresent : TemplateSignatureStatus.Verified, TemplateTrustState.ReviewRequired, false, false, "Explicit review is required"));
        result.Setup(x => x.CreateReviewGrantAsync("source", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new TemplateReviewGrant("grant", "source", "https://catalog.example.test/template.json", candidate, new TemplateArtifact(new string('a', 64), "artifact", DateTimeOffset.UtcNow), new TemplateScriptDiff(["new.ps1"], ["old.ps1"], ["changed.ps1"], [new TemplateScriptTextChange("changed.ps1", "old text", "new text", true)], true), DateTimeOffset.UtcNow.AddMinutes(5)));
        result.Setup(x => x.ApproveCandidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateArtifact(new string('a', 64), "artifact", DateTimeOffset.UtcNow));
        result.Setup(x => x.GetArtifactHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        result.Setup(x => x.GetSourcesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return result;
    }
}
