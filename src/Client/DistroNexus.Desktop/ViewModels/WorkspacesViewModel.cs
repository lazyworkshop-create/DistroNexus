using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Services;
using DistroNexus.Desktop.Properties;

namespace DistroNexus.Desktop.ViewModels;

public sealed partial class WorkspacesViewModel : ObservableObject
{
    private readonly IPowerShellModuleClient _workspaces;
    private readonly IWorkspaceShortcutWriter? _shortcuts;
    private readonly IDialogService? _dialogs;
    private readonly WorkspaceStartupRequest? _startupRequest;
    private CancellationTokenSource? _operation;
    private string? _operationId;
    public ObservableCollection<WorkspaceDefinition> Workspaces { get; } = [];
    [ObservableProperty] private WorkspaceDefinition? _selectedWorkspace;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _preview = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editInstance = string.Empty;
    [ObservableProperty] private string _importContent = string.Empty;
    [ObservableProperty] private string _definitionContent = string.Empty;
    [ObservableProperty] private WorkspaceDefinition? _editingDefinition;
    [ObservableProperty] private WorkspaceActionGroup? _selectedGroup;
    [ObservableProperty] private WorkspaceAction? _selectedAction;
    [ObservableProperty] private string _actionName = string.Empty;
    [ObservableProperty] private WorkspaceActionType _actionType;
    [ObservableProperty] private string _actionArguments = string.Empty;
    [ObservableProperty] private string _actionDependencies = string.Empty;
    [ObservableProperty] private string _actionTimeoutSeconds = string.Empty;
    [ObservableProperty] private WorkspaceFailurePolicy _actionFailurePolicy;
    [ObservableProperty] private bool _groupAllowParallel;
    [ObservableProperty] private string _projectPath = string.Empty;
    [ObservableProperty] private WorkspaceMissingInstanceRemediation _missingInstanceRemediation;
    [ObservableProperty] private WorkspaceCloseMode _closeMode;
    [ObservableProperty] private string _closeServices = string.Empty;
    [ObservableProperty] private string _validationError = string.Empty;
    [ObservableProperty] private string _preflightKind = "directory";
    [ObservableProperty] private string _preflightValue = string.Empty;
    [ObservableProperty] private bool _preflightRequired = true;
    public Array ActionTypes { get; } = Enum.GetValues(typeof(WorkspaceActionType));
    public Array FailurePolicies { get; } = Enum.GetValues(typeof(WorkspaceFailurePolicy));
    public Array MissingRemediations { get; } = Enum.GetValues(typeof(WorkspaceMissingInstanceRemediation));
    public Array CloseModes { get; } = Enum.GetValues(typeof(WorkspaceCloseMode));
    private WorkspaceImportPreview? _importPreview;
    [ObservableProperty] private Guid? _failedActionId;
    [ObservableProperty] private int _completedActions;
    [ObservableProperty] private string _progressText = string.Empty;
    public WorkspacesViewModel(IPowerShellModuleClient workspaces, IWorkspaceShortcutWriter? shortcuts = null, IDialogService? dialogs = null, WorkspaceStartupRequest? startupRequest = null) => (_workspaces, _shortcuts, _dialogs, _startupRequest) = (workspaces, shortcuts, dialogs, startupRequest);
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        if (_startupRequest?.WorkspaceId is Guid workspaceId)
        {
            SelectedWorkspace = Workspaces.SingleOrDefault(item => item.Id == workspaceId);
            if (SelectedWorkspace is null) { Status = L("Workspace_StatusNotFound", "Requested workspace was not found."); return; }
            await PreviewLaunchAsync();
        }
    }
    [RelayCommand] private async Task RefreshAsync() { if (IsBusy) return; IsBusy = true; try { Workspaces.Clear(); foreach (var item in await _workspaces.GetWorkspacesAsync()) Workspaces.Add(item); Status = $"{Workspaces.Count} workspace(s)."; } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } finally { IsBusy = false; } }
    [RelayCommand] private async Task PreviewLaunchAsync() { if (SelectedWorkspace is null) return; try { var preview = await _workspaces.PreviewWorkspaceLaunchAsync(SelectedWorkspace.Id); Preview = string.Join(Environment.NewLine, preview.Commands); Status = preview.RequiresTrust ? L("Workspace_StatusTrustRequired", "Explicit trust is required before this workspace can run commands.") : L("Workspace_StatusPreviewReady", "Launch preview ready."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task ApproveTrustAsync() { if (SelectedWorkspace is null) return; try { var preview = await _workspaces.PreviewWorkspaceTrustAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); var saved = await _workspaces.ApproveWorkspaceTrustAsync(preview.PreviewToken); Replace(saved); Status = L("Workspace_StatusTrustApproved", "Workspace trust approved."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task CreateAsync() { try { var item = new WorkspaceDefinition(Guid.NewGuid(), EditName.Trim(), EditInstance.Trim(), "/", [], [new WorkspaceActionGroup(Guid.NewGuid(), "launch", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "Terminal", [])])], new(), WorkspaceTrustState.Trusted); var preview = await _workspaces.PreviewWorkspaceSaveAsync(item, 0); var saved = await _workspaces.SaveWorkspaceAsync(preview.PreviewToken); Workspaces.Add(saved); SelectedWorkspace = saved; Status = L("Workspace_StatusCreated", "Workspace created."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task SaveAsync() { if (SelectedWorkspace is null) return; try { var preview = await _workspaces.PreviewWorkspaceSaveAsync(SelectedWorkspace with { DisplayName = EditName.Trim(), InstanceName = EditInstance.Trim() }, SelectedWorkspace.Revision); var saved = await _workspaces.SaveWorkspaceAsync(preview.PreviewToken); Replace(saved); Status = L("Workspace_StatusSaved", "Workspace saved."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task DuplicateAsync() { if (SelectedWorkspace is null) return; try { var preview = await _workspaces.PreviewWorkspaceDuplicateAsync(SelectedWorkspace.Id, EditName.Trim(), SelectedWorkspace.Revision); var saved = await _workspaces.DuplicateWorkspaceAsync(preview.PreviewToken); Workspaces.Add(saved); SelectedWorkspace = saved; Status = L("Workspace_StatusDuplicated", "Workspace duplicated."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task RemoveAsync() { if (SelectedWorkspace is null) return; try { var operation = await _workspaces.PreviewWorkspaceRemoveAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); Preview = string.Join(Environment.NewLine, operation.Effects); if (_dialogs is null || !await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmRemove", "Confirm workspace removal"), Preview)) return; await _workspaces.RemoveWorkspaceAsync(operation.PreviewToken); Workspaces.Remove(SelectedWorkspace); SelectedWorkspace = null; Status = L("Workspace_StatusRemoved", "Workspace removed."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task ExportAsync() { if (SelectedWorkspace is null) return; try { var operation = await _workspaces.PreviewWorkspaceExportAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); Preview = (await _workspaces.ExportWorkspaceAsync(operation.PreviewToken)).Content; Status = L("Workspace_StatusExportReady", "Export content ready for review and saving."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task LoadDefinitionEditorAsync() { if (SelectedWorkspace is null) return; try { var operation = await _workspaces.PreviewWorkspaceExportAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); DefinitionContent = (await _workspaces.ExportWorkspaceAsync(operation.PreviewToken)).Content; EditingDefinition = SelectedWorkspace; SelectedGroup = EditingDefinition.ActionGroups.FirstOrDefault(); ProjectPath = EditingDefinition.ProjectPath ?? string.Empty; MissingInstanceRemediation = EditingDefinition.MissingInstanceRemediation; CloseMode = EditingDefinition.ClosePolicy.Mode; CloseServices = string.Join(',', EditingDefinition.ClosePolicy.ServiceNames ?? []); Status = L("Workspace_StatusEditorLoaded", "Workspace definition loaded for editing."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private Task SaveDefinitionEditorAsync() { Status = "Raw workspace documents are read-only; use the structured editor."; return Task.CompletedTask; }
    [RelayCommand] private void AddGroup() { if (EditingDefinition is null) return; EditingDefinition = EditingDefinition with { ActionGroups = EditingDefinition.ActionGroups.Append(new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])])).ToArray() }; SelectedGroup = EditingDefinition.ActionGroups.Last(); }
    [RelayCommand] private void RemoveGroup() { if (EditingDefinition is null || SelectedGroup is null) return; EditingDefinition = EditingDefinition with { ActionGroups = EditingDefinition.ActionGroups.Where(x => x.Id != SelectedGroup.Id).ToArray() }; SelectedGroup = EditingDefinition.ActionGroups.FirstOrDefault(); }
    [RelayCommand] private void MoveGroupUp() { if (EditingDefinition is null || SelectedGroup is null) return; var groups=EditingDefinition.ActionGroups.ToList();var i=groups.FindIndex(x=>x.Id==SelectedGroup.Id);if(i>0){(groups[i-1],groups[i])=(groups[i],groups[i-1]);EditingDefinition=EditingDefinition with { ActionGroups=groups };} }
    [RelayCommand] private void AddAction() { if (EditingDefinition is null || SelectedGroup is null) return; var action = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", []); var group = SelectedGroup with { Actions = SelectedGroup.Actions.Append(action).ToArray() }; ReplaceGroup(group); SelectedAction = group.Actions.Single(item => item.Id == action.Id); }
    [RelayCommand] private void RemoveAction() { if (SelectedGroup is null || SelectedAction is null) return; ReplaceGroup(SelectedGroup with { Actions = SelectedGroup.Actions.Where(x => x.Id != SelectedAction.Id).ToArray() }); SelectedAction = null; }
    [RelayCommand] private void MoveActionUp() { if(SelectedGroup is null||SelectedAction is null)return;var a=SelectedGroup.Actions.ToList();var i=a.FindIndex(x=>x.Id==SelectedAction.Id);if(i>0){(a[i-1],a[i])=(a[i],a[i-1]);ReplaceGroup(SelectedGroup with{Actions=a});} }
    [RelayCommand] private void ApplyAction() { if (SelectedGroup is null || SelectedAction is null) return; try { var dependencies=string.IsNullOrWhiteSpace(ActionDependencies)?[]:ActionDependencies.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray(); var timeout=string.IsNullOrWhiteSpace(ActionTimeoutSeconds)?(TimeSpan?)null:TimeSpan.FromSeconds(int.Parse(ActionTimeoutSeconds));var action=SelectedAction with { Name=ActionName.Trim(),Type=ActionType,Arguments=ActionArguments.Split('|',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries),DependsOn=dependencies,Timeout=timeout,FailurePolicy=ActionFailurePolicy,SafeForParallel=GroupAllowParallel };ReplaceGroup(SelectedGroup with{AllowParallel=GroupAllowParallel,Actions=SelectedGroup.Actions.Select(x=>x.Id==action.Id?action:x).ToArray()});SelectedAction=action;ValidationError=string.Empty;}catch(Exception ex){ValidationError=ex.Message;} }
    [RelayCommand] private void AddPreflight() { if(EditingDefinition is null)return;try{var check=new WorkspacePreflightCheck(PreflightKind.Trim(),PreflightValue.Trim(),PreflightRequired);EditingDefinition=EditingDefinition with { PreflightChecks=EditingDefinition.PreflightChecks.Append(check).ToArray() };ValidationError=string.Empty;}catch(Exception ex){ValidationError=ex.Message;} }
    [RelayCommand] private void RemovePreflight(WorkspacePreflightCheck? check) { if(EditingDefinition is null||check is null)return;EditingDefinition=EditingDefinition with { PreflightChecks=EditingDefinition.PreflightChecks.Where(x=>x!=check).ToArray() }; }
    [RelayCommand] private async Task SaveStructuredEditorAsync() { if(EditingDefinition is null||SelectedWorkspace is null)return;try{var definition=EditingDefinition with { ProjectPath=string.IsNullOrWhiteSpace(ProjectPath)?null:ProjectPath,MissingInstanceRemediation=MissingInstanceRemediation,ClosePolicy=new(CloseMode,CloseServices.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))};var operation=await _workspaces.PreviewWorkspaceSaveAsync(definition,SelectedWorkspace.Revision);var saved=await _workspaces.SaveWorkspaceAsync(operation.PreviewToken);Replace(saved);EditingDefinition=saved;ValidationError=string.Empty;Status=L("Workspace_StatusSaved","Workspace definition saved.");}catch(Exception ex){ValidationError=ex.Message;}}
    [RelayCommand] private async Task PreviewImportAsync() { try { _importPreview = await _workspaces.PreviewWorkspaceImportAsync(ImportContent); Preview = string.Join(Environment.NewLine, _importPreview.Commands); Status = L("Workspace_StatusImportPreview", "Import preview ready; imported commands remain untrusted."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task ImportAsync() { if (_importPreview is null) return; try { var saved = await _workspaces.ImportWorkspaceAsync(_importPreview.ImportToken); Workspaces.Add(saved); SelectedWorkspace = saved; _importPreview = null; Status = L("Workspace_StatusImported", "Workspace imported as untrusted."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task CloseAsync() { if (SelectedWorkspace is null) return; try { var preview=await _workspaces.PreviewWorkspaceCloseAsync(SelectedWorkspace.Id); Preview=string.Join(Environment.NewLine,preview.Effects); if(_dialogs is null||!await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmClose","Confirm workspace close"),Preview)) return; var result = await _workspaces.CloseWorkspaceAsync(preview.LaunchToken); Status = result.Outcome == WorkspaceActionOutcome.Succeeded ? L("Workspace_StatusClosed","Workspace close policy completed.") : result.Code; } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private void CreateShortcut() { if (SelectedWorkspace is null || _shortcuts is null) return; try { Status = string.Format(L("Workspace_StatusShortcut","Shortcut created: {0}"), _shortcuts.CreateDesktopShortcut(SelectedWorkspace.Id)); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task RetryAsync() { if(SelectedWorkspace is null||FailedActionId is not Guid actionId)return; try { var preview=await _workspaces.PreviewWorkspaceRetryAsync(SelectedWorkspace.Id,actionId);Preview=string.Join(Environment.NewLine,preview.Effects.Concat(preview.Commands));if(_dialogs is null||!await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmRetry","Confirm workspace retry"),Preview))return;await _workspaces.RetryWorkspaceAsync(preview.LaunchToken);FailedActionId=null;Status="Workspace retry started."; }catch(Exception ex){Status=MainViewModel.FormatAlertMessage(ex);} }
    [RelayCommand] private async Task LaunchAsync() { if (SelectedWorkspace is null || IsBusy) return; IsBusy = true; _operation = new(); CompletedActions = 0; try { var preview = await _workspaces.PreviewWorkspaceLaunchAsync(SelectedWorkspace.Id, _operation.Token); Preview = string.Join(Environment.NewLine, preview.Effects.Concat(preview.Commands).Concat(preview.Preconditions)); if (preview.RequiresTrust) { Status = L("Workspace_StatusTrustRequired","Approve explicit trust before command execution."); return; } if (_dialogs is null || !await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmLaunch","Confirm workspace launch"), Preview)) { Status = L("Workspace_StatusLaunchNotConfirmed","Workspace launch was not confirmed."); return; } var started = await _workspaces.LaunchWorkspaceAsync(preview.LaunchToken, _operation.Token); _operationId = started.OperationId; while (!_operation.IsCancellationRequested) { var status = await _workspaces.GetWorkspaceOperationStatusAsync(started.OperationId, _operation.Token); CompletedActions = status.Progress.Count; foreach (var action in status.Progress.Where(action => action.Outcome == WorkspaceActionOutcome.Failed)) FailedActionId = action.ActionId; if (status.IsTerminal) { var result = status.Result; Status = result?.Succeeded == true ? L("Workspace_StatusLaunchCompleted","Workspace launch completed.") : result?.Cancelled == true ? L("Workspace_StatusLaunchCancelled","Workspace launch cancelled.") : L("Workspace_StatusLaunchFailed","One or more workspace actions failed."); return; } await Task.Delay(250, _operation.Token); } } catch (OperationCanceledException) { Status = L("Workspace_StatusLaunchCancelled","Workspace launch cancelled."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } finally { _operationId = null; _operation.Dispose(); _operation = null; IsBusy = false; } }
    [RelayCommand] private async Task CancelAsync() { if (_operationId is not null) await _workspaces.StopWorkspaceOperationAsync(_operationId); _operation?.Cancel(); }
    partial void OnSelectedWorkspaceChanged(WorkspaceDefinition? value) { if (value is not null) { EditName = value.DisplayName; EditInstance = value.InstanceName; } }
    partial void OnSelectedGroupChanged(WorkspaceActionGroup? value) { if(value is not null) GroupAllowParallel=value.AllowParallel; }
    partial void OnSelectedActionChanged(WorkspaceAction? value) { if(value is not null){ActionName=value.Name;ActionType=value.Type;ActionArguments=string.Join('|',value.Arguments);ActionDependencies=string.Join(',',value.DependsOn??[]);ActionTimeoutSeconds=value.Timeout?.TotalSeconds.ToString()??string.Empty;ActionFailurePolicy=value.FailurePolicy;} }
    private void ReplaceGroup(WorkspaceActionGroup group) { if(EditingDefinition is null)return;EditingDefinition=EditingDefinition with { ActionGroups=EditingDefinition.ActionGroups.Select(x=>x.Id==group.Id?group:x).ToArray() };SelectedGroup=group; }
    private static string L(string key,string fallback) => Resources.ResourceManager.GetString(key) ?? fallback;
    private void Replace(WorkspaceDefinition item) { var i = Workspaces.IndexOf(Workspaces.Single(x => x.Id == item.Id)); Workspaces[i] = item; SelectedWorkspace = item; }
}
