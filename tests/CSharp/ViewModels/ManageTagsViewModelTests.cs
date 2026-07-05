using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for <see cref="ManageTagsViewModel"/>.
/// </summary>
public sealed class ManageTagsViewModelTests
{
    private static WslInstance MakeInstance(string name) =>
        new() { Name = name, State = "Running", Version = 2 };

    private static (Mock<ITagService>, Mock<IWslManagerService>, Mock<IDialogService>) CreateMocks()
    {
        var tagService = new Mock<ITagService>();
        var wslManager = new Mock<IWslManagerService>();
        var dialog = new Mock<IDialogService>();

        dialog.Setup(d => d.ShowAlertAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(false);

        return (tagService, wslManager, dialog);
    }

    private static ManageTagsViewModel CreateSut(
        Mock<ITagService> tagService,
        Mock<IWslManagerService> wslManager,
        Mock<IDialogService> dialog)
        => new(tagService.Object, wslManager.Object, dialog.Object);

    // ── LoadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_BuildsTagListWithCorrectCounts()
    {
        var (tagService, wslManager, dialog) = CreateMocks();

        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([MakeInstance("A"), MakeInstance("B")]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend", "frontend"]);
        tagService.Setup(t => t.GetTagsAsync("A", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.GetTagsAsync("B", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend", "frontend"]);

        var sut = CreateSut(tagService, wslManager, dialog);
        await sut.LoadAsync();

        sut.Tags.Should().HaveCount(2);
        sut.Tags.First(t => t.Name == "backend").UsedByCount.Should().Be(2);
        sut.Tags.First(t => t.Name == "frontend").UsedByCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_CalledTwice_OnlyCallsGetAllTagsOnce()
    {
        var (tagService, wslManager, dialog) = CreateMocks();
        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);

        var sut = CreateSut(tagService, wslManager, dialog);

        await sut.LoadAsync();
        await sut.LoadAsync();

        tagService.Verify(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── DeleteTag ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTagCommand_WhenConfirmed_CallsRemoveTagAndRemovesFromList()
    {
        var (tagService, wslManager, dialog) = CreateMocks();
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);

        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([MakeInstance("A"), MakeInstance("B")]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.GetTagsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.RemoveTagAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var sut = CreateSut(tagService, wslManager, dialog);
        await sut.LoadAsync();

        var item = sut.Tags.First();
        await sut.DeleteTagCommand.ExecuteAsync(item);

        tagService.Verify(t => t.RemoveTagAsync("A", "backend", It.IsAny<CancellationToken>()), Times.Once);
        tagService.Verify(t => t.RemoveTagAsync("B", "backend", It.IsAny<CancellationToken>()), Times.Once);
        sut.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTagCommand_WhenDeclined_DoesNotRemoveTag()
    {
        var (tagService, wslManager, dialog) = CreateMocks();
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(false);

        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([MakeInstance("A")]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.GetTagsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);

        var sut = CreateSut(tagService, wslManager, dialog);
        await sut.LoadAsync();

        var item = sut.Tags.First();
        await sut.DeleteTagCommand.ExecuteAsync(item);

        tagService.Verify(t => t.RemoveTagAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sut.Tags.Should().HaveCount(1);
    }

    // ── BulkDelete ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkDeleteCommand_WhenConfirmed_DeletesAllSelectedTags()
    {
        var (tagService, wslManager, dialog) = CreateMocks();
        dialog.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);

        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([MakeInstance("A")]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend", "frontend", "infra"]);
        tagService.Setup(t => t.GetTagsAsync("A", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend", "frontend", "infra"]);
        tagService.Setup(t => t.RemoveTagAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var sut = CreateSut(tagService, wslManager, dialog);
        await sut.LoadAsync();

        // select first two
        sut.Tags[0].IsSelected = true;
        sut.Tags[1].IsSelected = true;

        await sut.BulkDeleteCommand.ExecuteAsync(null);

        // RemoveTagAsync called for 2 selected × 1 instance = 2 times
        tagService.Verify(
            t => t.RemoveTagAsync("A", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        sut.Tags.Should().HaveCount(1);
    }

    // ── RenameTag ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenameTagCommand_UpdatesTagOnAllInstances()
    {
        var (tagService, wslManager, dialog) = CreateMocks();

        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([MakeInstance("A"), MakeInstance("B")]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.GetTagsAsync("A", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.GetTagsAsync("B", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.SetTagsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var sut = CreateSut(tagService, wslManager, dialog);
        await sut.LoadAsync();

        var item = sut.Tags.First();
        item.PendingName = "renamed-backend";

        await sut.RenameTagCommand.ExecuteAsync(item);

        tagService.Verify(
            t => t.SetTagsAsync("A", It.Is<IEnumerable<string>>(ts => ts.Contains("renamed-backend")), It.IsAny<CancellationToken>()),
            Times.Once);
        tagService.Verify(
            t => t.SetTagsAsync("B", It.Is<IEnumerable<string>>(ts => ts.Contains("renamed-backend")), It.IsAny<CancellationToken>()),
            Times.Once);
        item.Name.Should().Be("renamed-backend");
        item.IsRenaming.Should().BeFalse();
    }

    [Fact]
    public async Task RenameTagCommand_WhenNameUnchanged_DoesNotCallSetTags()
    {
        var (tagService, wslManager, dialog) = CreateMocks();

        wslManager.Setup(m => m.GetInstancesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync([MakeInstance("A")]);
        tagService.Setup(t => t.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);
        tagService.Setup(t => t.GetTagsAsync("A", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(["backend"]);

        var sut = CreateSut(tagService, wslManager, dialog);
        await sut.LoadAsync();

        var item = sut.Tags.First();
        item.PendingName = "backend"; // same as original

        await sut.RenameTagCommand.ExecuteAsync(item);

        tagService.Verify(
            t => t.SetTagsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
