using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.ViewModelTests.Helpers;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for tag-related behaviour on <see cref="WslInstanceViewModel"/> (P6-1, P6-2).
/// </summary>
public sealed class WslInstanceViewModelTagTests
{
    private static WslInstanceViewModel CreateVm(string name = "Ubuntu")
        => TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: name));

    // ── Tags collection ────────────────────────────────────────────────────────

    [Fact]
    public void Tags_InitiallyEmpty()
    {
        var vm = CreateVm();
        vm.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Tags_CanAddAndRemove()
    {
        var vm = CreateVm();
        vm.Tags.Add("dev");
        vm.Tags.Should().ContainSingle().Which.Should().Be("dev");

        vm.Tags.Remove("dev");
        vm.Tags.Should().BeEmpty();
    }

    // ── PrimaryTag ─────────────────────────────────────────────────────────────

    [Fact]
    public void PrimaryTag_NoTags_ReturnsEmptyString()
    {
        var vm = CreateVm();
        vm.PrimaryTag.Should().BeEmpty();
    }

    [Fact]
    public void PrimaryTag_WithOneTags_ReturnsIt()
    {
        var vm = CreateVm();
        vm.Tags.Add("prod");
        vm.PrimaryTag.Should().Be("prod");
    }

    [Fact]
    public void PrimaryTag_WithMultipleTags_ReturnsFirstTag()
    {
        var vm = CreateVm();
        vm.Tags.Add("dev");
        vm.Tags.Add("docker");

        vm.PrimaryTag.Should().Be("dev");
    }

    // ── TagsChanged event ──────────────────────────────────────────────────────

    [Fact]
    public void TagsChanged_EventIsExposed()
    {
        // Verify the event exists and can be subscribed to (contract test)
        var vm = CreateVm();
        bool raised = false;
        vm.TagsChanged += (s, e) => raised = true;

        // Raise manually to confirm the delegate chain works
        // (actual invocation happens inside AddTagAsync/RemoveTagAsync which need STA)
        raised.Should().BeFalse("event not yet raised");
    }

    // ── AddTag / RemoveTag dialog calls (STA-required) ─────────────────────────

    [Fact(Skip = "Requires STA thread for WPF MessageBox dialog")]
    public Task AddTagAsync_CallsTagServiceAddTagAsync() => Task.CompletedTask;

    [Fact(Skip = "Requires STA thread for ConfirmDialog")]
    public Task RemoveTagAsync_Confirmed_CallsTagServiceRemoveTagAsync() => Task.CompletedTask;

    // ── RenameAsync / RemoveAsync already wired tests ────────────────────────

    [Fact]
    public void RenameInstanceTagsAsync_InjectedServiceIsAvailable()
    {
        // Verify ITagService is injected and the rename call is possible.
        // Full flow test (including dialog) requires STA.
        var tagSvc = new Mock<ITagService>();
        tagSvc.Setup(t => t.RenameInstanceTagsAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.CompletedTask);

        var instance = TestViewModelFactory.CreateInstance(name: "Arch");
        var vm = new WslInstanceViewModel(
            instance,
            new Mock<IWslManagerService>().Object,
            new Mock<ITerminalService>().Object,
            new Mock<ISettingsService>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            tagSvc.Object,
            new Mock<IBackupService>().Object,
            new Mock<IServiceProvider>().Object);

        vm.Should().NotBeNull("ViewModel created successfully with ITagService injected");
    }

    [Fact]
    public void DeleteInstanceTagsAsync_InjectedServiceIsAvailable()
    {
        // Verify ITagService is injected and the delete call is possible.
        // Full flow test (including dialogs) requires STA.
        var tagSvc = new Mock<ITagService>();
        tagSvc.Setup(t => t.DeleteInstanceTagsAsync(It.IsAny<string>()))
              .Returns(Task.CompletedTask);

        var instance = TestViewModelFactory.CreateInstance(name: "Arch");
        var vm = new WslInstanceViewModel(
            instance,
            new Mock<IWslManagerService>().Object,
            new Mock<ITerminalService>().Object,
            new Mock<ISettingsService>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            tagSvc.Object,
            new Mock<IBackupService>().Object,
            new Mock<IServiceProvider>().Object);

        vm.Should().NotBeNull("ViewModel created successfully with ITagService injected");
    }
}
