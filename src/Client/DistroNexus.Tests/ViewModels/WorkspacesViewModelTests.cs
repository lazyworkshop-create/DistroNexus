using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Moq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DistroNexus.Tests.ViewModels;

public sealed class WorkspacesViewModelTests
{
    [Fact]
    public void AddAction_SelectsTheActionInTheReplacementGroup()
    {
        var viewModel = new WorkspacesViewModel(new Mock<IWorkspaceService>().Object);
        var existing = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "existing", []);
        var group = new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [existing]);
        viewModel.EditingDefinition = Definition(group);
        viewModel.SelectedGroup = group;

        viewModel.AddActionCommand.Execute(null);

        Assert.NotNull(viewModel.SelectedAction);
        Assert.NotEqual(existing.Id, viewModel.SelectedAction!.Id);
        Assert.Contains(viewModel.SelectedAction.Id, viewModel.SelectedGroup!.Actions.Select(action => action.Id));
    }

    [Fact]
    public async Task Remove_WhenConfirmationDeclined_DoesNotCallService()
    {
        var service = new Mock<IWorkspaceService>(MockBehavior.Strict);
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var definition = Definition(new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])]));
        var viewModel = new WorkspacesViewModel(service.Object, dialogs: dialogs.Object);
        viewModel.Workspaces.Add(definition);
        viewModel.SelectedWorkspace = definition;

        await viewModel.RemoveCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Workspaces);
        service.Verify(x => x.RemoveAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task FullDefinitionEditor_RoundTripsCoreStringEnums()
    {
        var definition = Definition(new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Systemd, "systemd", ["start", "demo.service"], WorkspaceFailurePolicy.Continue)]));
        var json = JsonSerializer.Serialize(definition, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } });
        var service = new Mock<IWorkspaceService>();
        service.Setup(x => x.ExportAsync(definition.Id, definition.Revision, It.IsAny<CancellationToken>())).ReturnsAsync(json);
        service.Setup(x => x.SaveAsync(It.IsAny<WorkspaceDefinition>(), definition.Revision, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceDefinition item, long _, CancellationToken _) => item);
        var viewModel = new WorkspacesViewModel(service.Object);
        viewModel.Workspaces.Add(definition); viewModel.SelectedWorkspace = definition;

        await viewModel.LoadDefinitionEditorCommand.ExecuteAsync(null);
        await viewModel.SaveDefinitionEditorCommand.ExecuteAsync(null);

        service.Verify(x => x.SaveAsync(It.Is<WorkspaceDefinition>(item => item.TrustState == WorkspaceTrustState.Trusted && item.ActionGroups[0].Actions[0].Type == WorkspaceActionType.Systemd && item.ActionGroups[0].Actions[0].FailurePolicy == WorkspaceFailurePolicy.Continue), definition.Revision, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static WorkspaceDefinition Definition(WorkspaceActionGroup group) => new(Guid.NewGuid(), "demo", "Ubuntu", "/home/demo", [], [group], new(), WorkspaceTrustState.Trusted);
}
