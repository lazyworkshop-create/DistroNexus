namespace DistroNexus.Tests.Services;

public sealed class WorkspacesPageContractTests
{
    [Fact]
    public void StructuredEditor_DeclaresRequiredBindingsAndCommands()
    {
        var root = FindRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "Client", "DistroNexus.Desktop", "Views", "WorkspacesPage.xaml"));
        foreach (var value in new[]
        {
            "ItemsSource=\"{Binding Workspaces}\"", "SelectedItem=\"{Binding SelectedWorkspace}\"", "EditName", "EditInstance", "ImportContent", "Preview", "ProgressText",
            "DefinitionContent", "LoadDefinitionEditorCommand", "SaveDefinitionEditorCommand",
            "SelectedGroup", "SelectedAction", "ActionType", "ActionFailurePolicy", "ActionDependencies", "ActionTimeoutSeconds", "PreflightKind", "PreflightValue",
            "RefreshCommand", "PreviewLaunchCommand", "ApproveTrustCommand", "LaunchCommand", "CancelCommand", "RetryCommand", "CloseCommand", "CreateShortcutCommand", "ExportCommand", "PreviewImportCommand", "ImportCommand",
            "AddGroupCommand", "RemoveGroupCommand", "MoveGroupUpCommand", "AddActionCommand", "RemoveActionCommand", "MoveActionUpCommand", "ApplyActionCommand", "AddPreflightCommand", "RemovePreflightCommand", "SaveStructuredEditorCommand"
        }) Assert.Contains(value, xaml);
        Assert.Contains("AutomationProperties.Name", xaml);
        foreach (var key in new[] { "Workspace_ActionName", "Workspace_ActionType", "Workspace_ActionArguments", "Workspace_ActionDependencies", "Workspace_ActionTimeout", "Workspace_ActionFailurePolicy", "Workspace_ProjectPath", "Workspace_MissingRemediation", "Workspace_CloseMode", "Workspace_CloseServices", "Workspace_PreflightKind", "Workspace_PreflightValue", "Workspace_Required" })
            Assert.Contains($"AutomationProperties.Name=\"{{lex:Loc {key}}}\"", xaml);
    }
    private static string FindRoot() { var path=Directory.GetCurrentDirectory(); while(!File.Exists(Path.Combine(path,"AGENTS.md"))) path=Directory.GetParent(path)?.FullName??throw new DirectoryNotFoundException(); return path; }
}
