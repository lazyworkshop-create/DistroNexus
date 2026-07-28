using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using System.IO;

namespace DistroNexus.Desktop.ViewModels.Tabs;

public partial class ConfigurationTabViewModel(WslInstanceViewModel instance,
    IPowerShellModuleClient moduleClient, IDialogService dialogs) : ObservableObject
{
    private string? _fingerprint;
    private Dictionary<string, string> _current = new(StringComparer.OrdinalIgnoreCase);
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string _rawPreview = string.Empty;
    [ObservableProperty] private string _desiredRawPreview = string.Empty;
    [ObservableProperty] private string _diagnostics = string.Empty;
    [ObservableProperty] private string _restartImpact = string.Empty;
    [ObservableProperty] private bool _isSystemdSupported = true;
    [ObservableProperty] private string _systemdUnavailableReason = string.Empty;
    [ObservableProperty] private string? _bootCommand;
    [ObservableProperty] private bool? _systemd;
    [ObservableProperty] private string? _defaultUser;
    [ObservableProperty] private bool? _automountEnabled;
    [ObservableProperty] private string? _automountRoot;
    [ObservableProperty] private string? _automountOptions;
    [ObservableProperty] private bool? _mountFsTab;
    [ObservableProperty] private bool? _metadata;
    [ObservableProperty] private string? _umask;
    [ObservableProperty] private string? _caseSensitivity;
    [ObservableProperty] private bool? _interopEnabled;
    [ObservableProperty] private bool? _appendWindowsPath;
    [ObservableProperty] private string? _hostname;
    [ObservableProperty] private bool? _generateHosts;
    [ObservableProperty] private bool? _generateResolvConf;
    public bool CanSave => !IsLoading && !IsSaving && _fingerprint is not null && string.IsNullOrEmpty(Diagnostics);

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_fingerprint is not null || IsLoading) return;
        IsLoading = true; Changed();
        try
        {
            var doc = await moduleClient.GetInstanceConfigurationAsync(instance.Name); _current = new(doc.Document, StringComparer.OrdinalIgnoreCase);
            _fingerprint = doc.Fingerprint; RawPreview = DesiredRawPreview = string.Empty;
            Diagnostics = string.Empty;
            RestartImpact = L("Configuration_InstanceRestart");
            var snapshot = await moduleClient.GetInstanceCapabilitiesAsync(instance.Name);
            var host = await moduleClient.GetHostCapabilitiesAsync();
            var hostSystemd = host.Capabilities.TryGetValue(CapabilityId.Systemd, out var result) && result.IsSupported;
            IsSystemdSupported = hostSystemd && snapshot.Instance.WslVersion == 2;
            SystemdUnavailableReason = IsSystemdSupported ? string.Empty : result is null ? L("Configuration_UnsupportedReason") : L(result.ReasonCode);
            BootCommand = V("boot.command"); Systemd = B("boot.systemd"); DefaultUser = V("user.default");
            AutomountEnabled = B("automount.enabled"); AutomountRoot = V("automount.root"); AutomountOptions = V("automount.options");
            MountFsTab = B("automount.mountFsTab"); Metadata = B("automount.metadata"); Umask = V("automount.umask"); CaseSensitivity = V("automount.case");
            InteropEnabled = B("interop.enabled"); AppendWindowsPath = B("interop.appendWindowsPath"); Hostname = V("network.hostname");
            GenerateHosts = B("network.generateHosts"); GenerateResolvConf = B("network.generateResolvConf");
        }
        catch (Exception ex) when (IsConfigurationFailure(ex))
        {
            await ShowConfigurationFailureAsync(ex, "read");
        }
        finally { IsLoading = false; Changed(); }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var values = Changes(); if (values.Count == 0) return;
        IsSaving = true; Changed();
        try
        {
            var preview = await moduleClient.PreviewInstanceConfigurationAsync(instance.Name, values); DesiredRawPreview = RawPreview;
            var affected = instance.IsRunning ? instance.Name : L("Configuration_NoRunningInstances");
            if (!await dialogs.ShowConfirmAsync(L("Tab_Configuration"), string.Format(L("Configuration_SavePreview"), string.Join(", ", preview.ChangeSummary), affected, DesiredRawPreview))) return;
            var recoveryOffer = await moduleClient.GetInstanceConfigurationRecoveryOfferAsync(instance.Name);
            if (recoveryOffer.OfferState == "Available" && !await dialogs.ShowConfirmAsync(L("Tab_Configuration"), L("Recovery_OfferConfiguration"))) return;
            var result = await moduleClient.SaveInstanceConfigurationAsync(preview.PreviewToken); _fingerprint = null; RawPreview = DesiredRawPreview;
            _current = CurrentDesired();
            await dialogs.ShowAlertAsync(L("Tab_Configuration"), string.Format(L("Configuration_SaveCompleteWithBackup"), result.BackupCreated ? result.RecoveryAction : string.Empty, RestartImpact));
        }
        catch (ConfigurationConflictException ex) { await ShowConfigurationFailureAsync(ex, "write"); _fingerprint = null; }
        catch (ConfigurationValidationException ex) { await ShowConfigurationFailureAsync(ex, "write"); }
        catch (Exception ex) when (IsConfigurationFailure(ex)) { await ShowConfigurationFailureAsync(ex, "write"); }
        finally { IsSaving = false; Changed(); }
    }

    private Dictionary<string, string?> Changes()
    {
        var desired = CurrentDesired(); var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in WslConfigurationSchema.Distribution)
        {
            var id = $"{definition.Section}.{definition.Key}"; _current.TryGetValue(id, out var old); desired.TryGetValue(id, out var value);
            if (!string.Equals(old, value, StringComparison.Ordinal)) result[id] = string.IsNullOrWhiteSpace(value) ? null : value;
        }
        if (!IsSystemdSupported) result.Remove("boot.systemd");
        return result;
    }
    private Dictionary<string, string> CurrentDesired()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(d, "boot.command", BootCommand); Add(d, "boot.systemd", S(Systemd)); Add(d, "user.default", DefaultUser);
        Add(d, "automount.enabled", S(AutomountEnabled)); Add(d, "automount.root", AutomountRoot); Add(d, "automount.options", AutomountOptions);
        Add(d, "automount.mountFsTab", S(MountFsTab)); Add(d, "automount.metadata", S(Metadata)); Add(d, "automount.umask", Umask); Add(d, "automount.case", CaseSensitivity);
        Add(d, "interop.enabled", S(InteropEnabled)); Add(d, "interop.appendWindowsPath", S(AppendWindowsPath)); Add(d, "network.hostname", Hostname);
        Add(d, "network.generateHosts", S(GenerateHosts)); Add(d, "network.generateResolvConf", S(GenerateResolvConf)); return d;
    }
    private string? V(string key) => _current.TryGetValue(key, out var v) ? v : null;
    private bool? B(string key) => bool.TryParse(V(key), out var v) ? v : null;
    private static string? S(bool? value) => value?.ToString().ToLowerInvariant();
    private static void Add(Dictionary<string, string> d, string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) d[key] = value; }
    private void Changed() { OnPropertyChanged(nameof(CanSave)); SaveCommand.NotifyCanExecuteChanged(); }
    private async Task ShowConfigurationFailureAsync(Exception exception, string operation)
    {
        var mapped = ConfigurationErrorMapper.ToOperationException(exception, operation, instance.Name);
        var message = DistroNexus.Desktop.ViewModels.MainViewModel.FormatAlertMessage(mapped);
        if (exception is ConfigurationValidationException validation)
            Diagnostics = message + Environment.NewLine + string.Join(Environment.NewLine,
                validation.Diagnostics.Select(d => string.Format(L("Configuration_LineDiagnostic"), d.Line, LocalizeDiagnostic(d))));
        await dialogs.ShowAlertAsync(exception is ConfigurationConflictException ? L("Configuration_Conflict") : L("ErrorTitle"), message);
    }
    private static bool IsConfigurationFailure(Exception exception) => exception is ConfigurationTransportException
        or IOException
        or WslOperationException
        or WslException;
    private static string L(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
    private static string LocalizeDiagnostic(ConfigurationDiagnostic diagnostic) => diagnostic.Code switch
    {
        "config.invalidValue" => L("Configuration_InvalidValue"),
        "config.invalidUser" => L("Configuration_InvalidUser"),
        "config.userNotFound" => L("Configuration_UserNotFound"),
        "config.unsupported" => L("Configuration_UnsupportedReason"),
        "config.malformed" => L("Configuration_Malformed"),
        _ => diagnostic.Message
    };
}
