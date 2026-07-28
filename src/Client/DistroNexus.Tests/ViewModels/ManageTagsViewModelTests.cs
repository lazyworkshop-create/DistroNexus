using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class ManageTagsViewModelTests
{
    [Fact]
    public async Task RenameTagCommand_ReplacesTheMatchingInstanceTagsThroughTheModuleClient()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        moduleClient
            .Setup(client => client.GetInstanceTagsAsync("Ubuntu", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DistroNexusInstanceTagResult("Ubuntu", ["dev", "docker"])]);
        moduleClient
            .Setup(client => client.SetInstanceTagsAsync("Ubuntu", It.Is<IReadOnlyList<string>>(tags => tags.SequenceEqual(new[] { "prod", "docker" })), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var wslManager = Instances("Ubuntu");
        var item = new TagItemViewModel("dev", 1) { PendingName = "prod", IsRenaming = true };
        var viewModel = new ManageTagsViewModel(moduleClient.Object, wslManager.Object, Mock.Of<IDialogService>());

        await viewModel.RenameTagCommand.ExecuteAsync(item);

        moduleClient.VerifyAll();
        Assert.Equal("prod", item.Name);
        Assert.False(item.IsRenaming);
    }

    [Fact]
    public async Task DeleteTagCommand_UsesTheTypedRemoveOperationAfterConfirmation()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        moduleClient
            .Setup(client => client.RemoveInstanceTagAsync("Ubuntu", "dev", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var item = new TagItemViewModel("dev", 1);
        var viewModel = new ManageTagsViewModel(moduleClient.Object, Instances("Ubuntu").Object, dialogs.Object);
        viewModel.Tags.Add(item);

        await viewModel.DeleteTagCommand.ExecuteAsync(item);

        moduleClient.VerifyAll();
        Assert.DoesNotContain(item, viewModel.Tags);
    }

    [Fact]
    public async Task BulkDeleteCommand_RoutesEverySelectedTagThroughTheTypedRemoveOperation()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        moduleClient.Setup(client => client.RemoveInstanceTagAsync("Ubuntu", "dev", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        moduleClient.Setup(client => client.RemoveInstanceTagAsync("Ubuntu", "test", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var viewModel = new ManageTagsViewModel(moduleClient.Object, Instances("Ubuntu").Object, dialogs.Object);
        viewModel.Tags.Add(new TagItemViewModel("dev", 1) { IsSelected = true });
        viewModel.Tags.Add(new TagItemViewModel("test", 1) { IsSelected = true });

        await viewModel.BulkDeleteCommand.ExecuteAsync(null);

        moduleClient.VerifyAll();
        Assert.Empty(viewModel.Tags);
    }

    [Fact]
    public async Task LoadAsync_PopulatesUsageCountsFromTypedModuleTagResults()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        moduleClient
            .Setup(client => client.GetInstanceTagsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DistroNexusInstanceTagResult("Ubuntu", ["dev", "docker"]),
                new DistroNexusInstanceTagResult("Orphaned", ["legacy"])
            ]);
        moduleClient
            .Setup(client => client.GetInstanceTagsAsync("Ubuntu", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DistroNexusInstanceTagResult("Ubuntu", ["dev", "docker"])]);
        var wslManager = new Mock<IWslManagerService>(MockBehavior.Strict);
        wslManager
            .Setup(service => service.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WslInstance { Name = "Ubuntu", State = "Running" }]);
        var viewModel = new ManageTagsViewModel(moduleClient.Object, wslManager.Object, Mock.Of<IDialogService>());

        await viewModel.LoadAsync();

        Assert.Collection(
            viewModel.Tags.OrderBy(tag => tag.Name),
            tag => { Assert.Equal("dev", tag.Name); Assert.Equal(1, tag.UsedByCount); },
            tag => { Assert.Equal("docker", tag.Name); Assert.Equal(1, tag.UsedByCount); },
            tag => { Assert.Equal("legacy", tag.Name); Assert.Equal(0, tag.UsedByCount); });
        moduleClient.Verify(client => client.GetInstanceTagsAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        moduleClient.Verify(client => client.GetInstanceTagsAsync("Ubuntu", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IWslManagerService> Instances(params string[] names)
    {
        var manager = new Mock<IWslManagerService>(MockBehavior.Strict);
        manager.Setup(service => service.GetInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(names.Select(name => new WslInstance { Name = name, State = "Running" }).ToList());
        return manager;
    }
}
