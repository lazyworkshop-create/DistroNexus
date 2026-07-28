using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class WorkspacesViewModelTests
{
    [Fact]
    public void AddAction_SelectsTheActionInTheReplacementGroup()
    {
        var viewModel = new WorkspacesViewModel(new Mock<IPowerShellModuleClient>().Object);
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
    public async Task Remove_WhenConfirmationDeclined_DoesNotExecuteToken()
    {
        var service = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(x => x.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var definition = Definition(new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])]));
        service.Setup(x => x.PreviewWorkspaceRemoveAsync(definition.Id, definition.Revision, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceOperationPreview("token", definition.Id, definition.Revision, ["remove"]));
        var viewModel = new WorkspacesViewModel(service.Object, dialogs: dialogs.Object);
        viewModel.Workspaces.Add(definition);
        viewModel.SelectedWorkspace = definition;

        await viewModel.RemoveCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Workspaces);
        service.Verify(x => x.RemoveWorkspaceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task FullDefinitionEditor_IsReadOnlyAndDoesNotDeserializeDocument()
    {
        var definition = Definition(new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Systemd, "systemd", ["start", "demo.service"], WorkspaceFailurePolicy.Continue)]));
        var service = new Mock<IPowerShellModuleClient>();
        service.Setup(x => x.PreviewWorkspaceExportAsync(definition.Id, definition.Revision, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceOperationPreview("token", definition.Id, definition.Revision, []));
        service.Setup(x => x.ExportWorkspaceAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceExportResult("{ invalid JSON"));
        var viewModel = new WorkspacesViewModel(service.Object);
        viewModel.Workspaces.Add(definition); viewModel.SelectedWorkspace = definition;

        await viewModel.LoadDefinitionEditorCommand.ExecuteAsync(null);
        await viewModel.SaveDefinitionEditorCommand.ExecuteAsync(null);

        service.Verify(x => x.SaveWorkspaceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WorkspaceDefinition Definition(WorkspaceActionGroup group) => new(Guid.NewGuid(), "demo", "Ubuntu", "/home/demo", [], [group], new(), WorkspaceTrustState.Trusted);
}
