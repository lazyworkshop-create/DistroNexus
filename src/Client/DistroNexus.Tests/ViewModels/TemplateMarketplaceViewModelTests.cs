using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class TemplateMarketplaceViewModelTests
{
    [Fact]
    public async Task MarketplaceLoad_UsesTypedModuleOperationsOnly()
    {
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.GetTemplatesAsync(true, null, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        client.Setup(x => x.GetTemplateSourcesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new TemplateSourceDisplay("source", "https://catalog.example.test", TemplateSourceKind.Remote, null, true, null)]);
        var vm = new TemplatesViewModel(client.Object, NullLogger<TemplatesViewModel>.Instance, Mock.Of<IDialogService>());

        await vm.InitializeAsync();

        Assert.Single(vm.MarketplaceSources);
        client.Verify(x => x.GetTemplateSourcesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
