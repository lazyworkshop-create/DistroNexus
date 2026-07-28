using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels.Tabs;

public partial class ServicesTabViewModel : ObservableObject
{
    public sealed record JournalSeverityOption(string Value, string Display);
    private readonly WslInstanceViewModel _instance;
    private readonly IPowerShellModuleClient _services;
    private readonly IDialogService _dialogs;
    private bool _initialized;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _unavailableReason = string.Empty;
    [ObservableProperty] private ObservableCollection<SystemdServiceInfo> _items = [];
    [ObservableProperty] private SystemdScope _selectedScope = SystemdScope.System;
    [ObservableProperty] private bool _runningOnly;
    [ObservableProperty] private bool _failedOnly;
    [ObservableProperty] private bool _stoppedOnly;
    [ObservableProperty] private bool _enabledOnly;
    [ObservableProperty] private bool _userServicesOnly;
    [ObservableProperty] private string _journalSearch = string.Empty;
    [ObservableProperty] private int _journalLineLimit = 200;
    [ObservableProperty] private string _journalSeverity = "All";
    [ObservableProperty] private ObservableCollection<SystemdJournalEntry> _journal = [];
    [ObservableProperty] private string _details = string.Empty;
    [ObservableProperty] private SystemdServiceInfo? _selectedService;
    public bool IsAvailable => string.IsNullOrEmpty(UnavailableReason);
    public IReadOnlyList<SystemdScope> AvailableScopes { get; } = [SystemdScope.System, SystemdScope.User];
    public IReadOnlyList<JournalSeverityOption> JournalSeverities { get; } =
    [new("All", R("ServicesTab_JournalAll")), new("Info", R("ServicesTab_JournalInfo")), new("Warning", R("ServicesTab_JournalWarning")), new("Error", R("ServicesTab_JournalError"))];

    public ServicesTabViewModel(WslInstanceViewModel instance, IPowerShellModuleClient services, IDialogService dialogs) => (_instance, _services, _dialogs) = (instance, services, dialogs);
    public async Task InitializeAsync() { if (_initialized) return; _initialized = true; await RefreshAsync(); }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true; UnavailableReason = string.Empty;
        try
        {
            IReadOnlyList<SystemdScope> scopes = UserServicesOnly ? new[] { SystemdScope.User } : new[] { SelectedScope };
            var source = (await Task.WhenAll(scopes.Select(x => _services.GetSystemdServicesAsync(_instance.Name, x)))).SelectMany(x => x);
            Items = new ObservableCollection<SystemdServiceInfo>(source.Where(x => !RunningOnly || x.ActiveState == "active").Where(x => !StoppedOnly || x.ActiveState is "inactive" or "failed").Where(x => !FailedOnly || x.ActiveState == "failed").Where(x => !EnabledOnly || x.EnabledState == "enabled").Where(x => !UserServicesOnly || x.Scope == SystemdScope.User));
        }
        catch (Exception ex) { UnavailableReason = ex.Message; Items = []; }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsAvailable)); }
    }

    [RelayCommand]
    private Task StartAsync(SystemdServiceInfo? item) => RunActionAsync(item, SystemdAction.Start);
    [RelayCommand]
    private Task StopAsync(SystemdServiceInfo? item) => RunActionAsync(item, SystemdAction.Stop);
    [RelayCommand]
    private Task RestartAsync(SystemdServiceInfo? item) => RunActionAsync(item, SystemdAction.Restart);
    [RelayCommand] private Task EnableAsync(SystemdServiceInfo? item) => RunActionAsync(item, SystemdAction.Enable);
    [RelayCommand] private Task DisableAsync(SystemdServiceInfo? item) => RunActionAsync(item, SystemdAction.Disable);
    [RelayCommand] private Task ReloadAsync(SystemdServiceInfo? item) => RunActionAsync(item, SystemdAction.Reload);
    [RelayCommand]
    private async Task LoadDetailsAsync(SystemdServiceInfo? item)
    {
        if (item is null) return;
        SelectedService = item;
        var details = await _services.GetSystemdServiceDetailsAsync(_instance.Name, item.Name, item.Scope);
        Details = details is null ? string.Empty : $"{details.UnitFilePath}{Environment.NewLine}{string.Join(Environment.NewLine, details.Dependencies)}";
        var entries = await _services.GetSystemdServiceJournalAsync(_instance.Name, item.Name, item.Scope, JournalSearch, JournalLineLimit);
        Journal = new ObservableCollection<SystemdJournalEntry>(entries.Where(x => JournalSeverity == "All" || string.Equals(x.Severity, JournalSeverity, StringComparison.OrdinalIgnoreCase)));
    }
    partial void OnSelectedScopeChanged(SystemdScope value) => _ = RefreshAsync();
    partial void OnRunningOnlyChanged(bool value) => _ = RefreshAsync();
    partial void OnFailedOnlyChanged(bool value) => _ = RefreshAsync();
    partial void OnStoppedOnlyChanged(bool value) => _ = RefreshAsync();
    partial void OnEnabledOnlyChanged(bool value) => _ = RefreshAsync();
    partial void OnUserServicesOnlyChanged(bool value) => _ = RefreshAsync();
    partial void OnJournalSeverityChanged(string value) { if (SelectedService is not null) _ = LoadDetailsAsync(SelectedService); }
    private async Task RunActionAsync(SystemdServiceInfo? item, SystemdAction action)
    {
        if (item is null) return;
        try
        {
            var preview = await _services.GetSystemdServicePreviewAsync(_instance.Name, item.Name, action, item.Scope);
            var message = string.Join(Environment.NewLine, preview.Effects.Concat(preview.Preconditions));
            if (!await _dialogs.ShowConfirmAsync(R("ServicesTab_ConfirmActionTitle"), message)) return;
            var outcome = await _services.InvokeSystemdServiceAsync(preview.PreviewToken);
            if (!outcome.Succeeded) await _dialogs.ShowAlertAsync(R("ServicesTab_ActionFailedTitle"), outcome.Guidance ?? outcome.OutcomeCode);
            await RefreshAsync();
        }
        catch (Exception ex) { await _dialogs.ShowAlertAsync(R("ServicesTab_ActionFailedTitle"), ex.Message); }
    }
    [RelayCommand]
    private void CopyJournal()
    {
        try { System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, Journal.Select(x => x.Message))); } catch { }
    }
    private static string R(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
}
