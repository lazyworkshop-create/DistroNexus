using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistroNexus.Desktop.Properties;

namespace DistroNexus.Desktop.ViewModels;

public sealed partial class WorkspacesViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspaces;
    private readonly IWorkspaceShortcutWriter? _shortcuts;
    private readonly IDialogService? _dialogs;
    private readonly WorkspaceStartupRequest? _startupRequest;
    private static readonly JsonSerializerOptions DefinitionJson = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    private CancellationTokenSource? _operation;
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
    public WorkspacesViewModel(IWorkspaceService workspaces, IWorkspaceShortcutWriter? shortcuts = null, IDialogService? dialogs = null, WorkspaceStartupRequest? startupRequest = null) => (_workspaces, _shortcuts, _dialogs, _startupRequest) = (workspaces, shortcuts, dialogs, startupRequest);
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
    [RelayCommand] private async Task RefreshAsync() { if (IsBusy) return; IsBusy = true; try { Workspaces.Clear(); foreach (var item in await _workspaces.ListAsync()) Workspaces.Add(item); Status = $"{Workspaces.Count} workspace(s)."; } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } finally { IsBusy = false; } }
    [RelayCommand] private async Task PreviewLaunchAsync() { if (SelectedWorkspace is null) return; try { var preview = await _workspaces.PreviewLaunchAsync(SelectedWorkspace.Id); Preview = string.Join(Environment.NewLine, preview.Commands); Status = preview.RequiresTrust ? L("Workspace_StatusTrustRequired", "Explicit trust is required before this workspace can run commands.") : L("Workspace_StatusPreviewReady", "Launch preview ready."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task ApproveTrustAsync() { if (SelectedWorkspace is null) return; try { var saved = await _workspaces.ApproveTrustAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); Replace(saved); Status = L("Workspace_StatusTrustApproved", "Workspace trust approved."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task CreateAsync() { try { var item = new WorkspaceDefinition(Guid.NewGuid(), EditName.Trim(), EditInstance.Trim(), "/", [], [new WorkspaceActionGroup(Guid.NewGuid(), "launch", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "Terminal", [])])], new(), WorkspaceTrustState.Trusted); var saved = await _workspaces.SaveAsync(item, 0); Workspaces.Add(saved); SelectedWorkspace = saved; Status = L("Workspace_StatusCreated", "Workspace created."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task SaveAsync() { if (SelectedWorkspace is null) return; try { var saved = await _workspaces.SaveAsync(SelectedWorkspace with { DisplayName = EditName.Trim(), InstanceName = EditInstance.Trim() }, SelectedWorkspace.Revision); Replace(saved); Status = L("Workspace_StatusSaved", "Workspace saved."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task DuplicateAsync() { if (SelectedWorkspace is null) return; try { var saved = await _workspaces.DuplicateAsync(SelectedWorkspace.Id, EditName.Trim(), SelectedWorkspace.Revision); Workspaces.Add(saved); SelectedWorkspace = saved; Status = L("Workspace_StatusDuplicated", "Workspace duplicated."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task RemoveAsync() { if (SelectedWorkspace is null) return; try { var preview = string.Format(L("Workspace_RemovePreview", "Remove workspace '{0}'. This cannot be undone."), SelectedWorkspace.DisplayName); Preview = preview; if (_dialogs is null || !await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmRemove", "Confirm workspace removal"), preview)) return; await _workspaces.RemoveAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); Workspaces.Remove(SelectedWorkspace); SelectedWorkspace = null; Status = L("Workspace_StatusRemoved", "Workspace removed."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task ExportAsync() { if (SelectedWorkspace is null) return; try { Preview = await _workspaces.ExportAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); Status = L("Workspace_StatusExportReady", "Export content ready for review and saving."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task LoadDefinitionEditorAsync() { if (SelectedWorkspace is null) return; try { DefinitionContent = await _workspaces.ExportAsync(SelectedWorkspace.Id, SelectedWorkspace.Revision); EditingDefinition = SelectedWorkspace; SelectedGroup = EditingDefinition.ActionGroups.FirstOrDefault(); ProjectPath = EditingDefinition.ProjectPath ?? string.Empty; MissingInstanceRemediation = EditingDefinition.MissingInstanceRemediation; CloseMode = EditingDefinition.ClosePolicy.Mode; CloseServices = string.Join(',', EditingDefinition.ClosePolicy.ServiceNames ?? []); Status = L("Workspace_StatusEditorLoaded", "Workspace definition loaded for editing."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task SaveDefinitionEditorAsync() { if (SelectedWorkspace is null) return; try { var definition = JsonSerializer.Deserialize<WorkspaceDefinition>(DefinitionContent, DefinitionJson) ?? throw new ArgumentException("Workspace definition is invalid."); if (definition.Id != SelectedWorkspace.Id) throw new ArgumentException("Workspace identity cannot be changed."); var saved = await _workspaces.SaveAsync(definition, SelectedWorkspace.Revision); Replace(saved); Status = L("Workspace_StatusSaved", "Workspace definition saved."); } catch (JsonException ex) { Status = MainViewModel.FormatAlertMessage(new ArgumentException("Workspace definition JSON is invalid.", ex)); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private void AddGroup() { if (EditingDefinition is null) return; EditingDefinition = EditingDefinition with { ActionGroups = EditingDefinition.ActionGroups.Append(new WorkspaceActionGroup(Guid.NewGuid(), "group", false, [new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])])).ToArray() }; SelectedGroup = EditingDefinition.ActionGroups.Last(); }
    [RelayCommand] private void RemoveGroup() { if (EditingDefinition is null || SelectedGroup is null) return; EditingDefinition = EditingDefinition with { ActionGroups = EditingDefinition.ActionGroups.Where(x => x.Id != SelectedGroup.Id).ToArray() }; SelectedGroup = EditingDefinition.ActionGroups.FirstOrDefault(); }
    [RelayCommand] private void MoveGroupUp() { if (EditingDefinition is null || SelectedGroup is null) return; var groups=EditingDefinition.ActionGroups.ToList();var i=groups.FindIndex(x=>x.Id==SelectedGroup.Id);if(i>0){(groups[i-1],groups[i])=(groups[i],groups[i-1]);EditingDefinition=EditingDefinition with { ActionGroups=groups };} }
    [RelayCommand] private void AddAction() { if (EditingDefinition is null || SelectedGroup is null) return; var action = new WorkspaceAction(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", []); var group = SelectedGroup with { Actions = SelectedGroup.Actions.Append(action).ToArray() }; ReplaceGroup(group); SelectedAction = group.Actions.Single(item => item.Id == action.Id); }
    [RelayCommand] private void RemoveAction() { if (SelectedGroup is null || SelectedAction is null) return; ReplaceGroup(SelectedGroup with { Actions = SelectedGroup.Actions.Where(x => x.Id != SelectedAction.Id).ToArray() }); SelectedAction = null; }
    [RelayCommand] private void MoveActionUp() { if(SelectedGroup is null||SelectedAction is null)return;var a=SelectedGroup.Actions.ToList();var i=a.FindIndex(x=>x.Id==SelectedAction.Id);if(i>0){(a[i-1],a[i])=(a[i],a[i-1]);ReplaceGroup(SelectedGroup with{Actions=a});} }
    [RelayCommand] private void ApplyAction() { if (SelectedGroup is null || SelectedAction is null) return; try { var dependencies=string.IsNullOrWhiteSpace(ActionDependencies)?[]:ActionDependencies.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray(); var timeout=string.IsNullOrWhiteSpace(ActionTimeoutSeconds)?(TimeSpan?)null:TimeSpan.FromSeconds(int.Parse(ActionTimeoutSeconds));var action=SelectedAction with { Name=ActionName.Trim(),Type=ActionType,Arguments=ActionArguments.Split('|',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries),DependsOn=dependencies,Timeout=timeout,FailurePolicy=ActionFailurePolicy,SafeForParallel=GroupAllowParallel };WorkspaceValidation.ValidateAction(action);ReplaceGroup(SelectedGroup with{AllowParallel=GroupAllowParallel,Actions=SelectedGroup.Actions.Select(x=>x.Id==action.Id?action:x).ToArray()});SelectedAction=action;ValidationError=string.Empty;}catch(Exception ex){ValidationError=ex.Message;} }
    [RelayCommand] private void AddPreflight() { if(EditingDefinition is null)return;try{var check=new WorkspacePreflightCheck(PreflightKind.Trim(),PreflightValue.Trim(),PreflightRequired);var definition=EditingDefinition with { PreflightChecks=EditingDefinition.PreflightChecks.Append(check).ToArray() };WorkspaceValidation.ValidateDefinition(definition);EditingDefinition=definition;ValidationError=string.Empty;}catch(Exception ex){ValidationError=ex.Message;} }
    [RelayCommand] private void RemovePreflight(WorkspacePreflightCheck? check) { if(EditingDefinition is null||check is null)return;EditingDefinition=EditingDefinition with { PreflightChecks=EditingDefinition.PreflightChecks.Where(x=>x!=check).ToArray() }; }
    [RelayCommand] private async Task SaveStructuredEditorAsync() { if(EditingDefinition is null||SelectedWorkspace is null)return;try{var definition=EditingDefinition with { ProjectPath=string.IsNullOrWhiteSpace(ProjectPath)?null:ProjectPath,MissingInstanceRemediation=MissingInstanceRemediation,ClosePolicy=new(CloseMode,CloseServices.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))};WorkspaceValidation.ValidateDefinition(definition);var saved=await _workspaces.SaveAsync(definition,SelectedWorkspace.Revision);Replace(saved);EditingDefinition=saved;ValidationError=string.Empty;Status=L("Workspace_StatusSaved","Workspace definition saved.");}catch(Exception ex){ValidationError=ex.Message;}}
    [RelayCommand] private async Task PreviewImportAsync() { try { _importPreview = await _workspaces.PreviewImportAsync(ImportContent); Preview = string.Join(Environment.NewLine, _importPreview.Commands); Status = L("Workspace_StatusImportPreview", "Import preview ready; imported commands remain untrusted."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task ImportAsync() { if (_importPreview is null) return; try { var saved = await _workspaces.ImportAsync(ImportContent, _importPreview.ImportToken, 0); Workspaces.Add(saved); SelectedWorkspace = saved; _importPreview = null; Status = L("Workspace_StatusImported", "Workspace imported as untrusted."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task CloseAsync() { if (SelectedWorkspace is null) return; try { var preview=await _workspaces.PreviewCloseAsync(SelectedWorkspace.Id); Preview=string.Join(Environment.NewLine,preview.Effects); if(_dialogs is null||!await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmClose","Confirm workspace close"),Preview)) return; var result = await _workspaces.CloseAsync(SelectedWorkspace.Id, preview.Revision, preview.LaunchToken); Status = result.Outcome == WorkspaceActionOutcome.Succeeded ? L("Workspace_StatusClosed","Workspace close policy completed.") : result.Code; } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private void CreateShortcut() { if (SelectedWorkspace is null || _shortcuts is null) return; try { Status = string.Format(L("Workspace_StatusShortcut","Shortcut created: {0}"), _shortcuts.CreateDesktopShortcut(SelectedWorkspace.Id)); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task RetryAsync() { if(SelectedWorkspace is null||FailedActionId is not Guid actionId)return; try { var preview=await _workspaces.PreviewRetryAsync(SelectedWorkspace.Id,actionId);Preview=string.Join(Environment.NewLine,preview.Effects.Concat(preview.Commands));if(_dialogs is null||!await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmRetry","Confirm workspace retry"),Preview))return;var result=await _workspaces.RetryAsync(SelectedWorkspace.Id,actionId,preview.Revision,preview.LaunchToken);if(result.Outcome==WorkspaceActionOutcome.Succeeded)FailedActionId=null;Status=result.Code; }catch(Exception ex){Status=MainViewModel.FormatAlertMessage(ex);} }
    [RelayCommand] private async Task LaunchAsync() { if (SelectedWorkspace is null || IsBusy) return; IsBusy = true; _operation = new(); CompletedActions = 0; try { var preview = await _workspaces.PreviewLaunchAsync(SelectedWorkspace.Id, _operation.Token); Preview = string.Join(Environment.NewLine, preview.Effects.Concat(preview.Commands).Concat(preview.Preconditions)); if (preview.RequiresTrust) { Status = L("Workspace_StatusTrustRequired","Approve explicit trust before command execution."); return; } if (_dialogs is null || !await _dialogs.ShowConfirmAsync(L("Workspace_ConfirmLaunch","Confirm workspace launch"), Preview)) { Status = L("Workspace_StatusLaunchNotConfirmed","Workspace launch was not confirmed."); return; } var progress = new Progress<WorkspaceActionResult>(result => { CompletedActions++; ProgressText = string.Format(L("Workspace_ProgressAction","{0}: {1}"),CompletedActions,result.Code); if (result.Outcome == WorkspaceActionOutcome.Failed) FailedActionId = result.ActionId; }); var result = await _workspaces.LaunchAsync(preview.WorkspaceId, preview.Revision, preview.LaunchToken, progress, _operation.Token); FailedActionId ??= result.Actions.FirstOrDefault(x=>x.Outcome==WorkspaceActionOutcome.Failed)?.ActionId; Status = result.Succeeded ? L("Workspace_StatusLaunchCompleted","Workspace launch completed.") : result.Cancelled ? L("Workspace_StatusLaunchCancelled","Workspace launch cancelled.") : L("Workspace_StatusLaunchFailed","One or more workspace actions failed."); } catch (OperationCanceledException) { Status = L("Workspace_StatusLaunchCancelled","Workspace launch cancelled."); } catch (Exception ex) { Status = MainViewModel.FormatAlertMessage(ex); } finally { _operation.Dispose(); _operation = null; IsBusy = false; } }
    [RelayCommand] private void Cancel() => _operation?.Cancel();
    partial void OnSelectedWorkspaceChanged(WorkspaceDefinition? value) { if (value is not null) { EditName = value.DisplayName; EditInstance = value.InstanceName; } }
    partial void OnSelectedGroupChanged(WorkspaceActionGroup? value) { if(value is not null) GroupAllowParallel=value.AllowParallel; }
    partial void OnSelectedActionChanged(WorkspaceAction? value) { if(value is not null){ActionName=value.Name;ActionType=value.Type;ActionArguments=string.Join('|',value.Arguments);ActionDependencies=string.Join(',',value.DependsOn??[]);ActionTimeoutSeconds=value.Timeout?.TotalSeconds.ToString()??string.Empty;ActionFailurePolicy=value.FailurePolicy;} }
    private void ReplaceGroup(WorkspaceActionGroup group) { if(EditingDefinition is null)return;EditingDefinition=EditingDefinition with { ActionGroups=EditingDefinition.ActionGroups.Select(x=>x.Id==group.Id?group:x).ToArray() };SelectedGroup=group; }
    private static string L(string key,string fallback) => Resources.ResourceManager.GetString(key) ?? fallback;
    private void Replace(WorkspaceDefinition item) { var i = Workspaces.IndexOf(Workspaces.Single(x => x.Id == item.Id)); Workspaces[i] = item; SelectedWorkspace = item; }
}
