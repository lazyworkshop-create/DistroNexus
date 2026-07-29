using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using System.IO;
using System.Collections.ObjectModel;
using DistroNexus.Core.Services;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// ViewModel for the WSL Global Configuration editor section in the Settings page.
/// Covers requirement E-01.
/// </summary>
public partial class WslConfigSectionViewModel : ObservableObject
{
    private readonly IPowerShellModuleClient _moduleClient;
    private readonly IDialogService _dialogService;
    private IReadOnlySet<string> _availableCapabilities = new HashSet<string>();
    public ObservableCollection<ConfigurationSettingFieldViewModel> Fields { get; } = [];
    [ObservableProperty] private string _currentRaw = string.Empty;
    [ObservableProperty] private string _desiredRaw = string.Empty;
    [ObservableProperty] private string _pendingRestart = string.Empty;
    private bool _initialized;

    // ── Form fields ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryError))]
    [NotifyPropertyChangedFor(nameof(HasMemoryError))]
    [NotifyPropertyChangedFor(nameof(ShowHighMemoryWarning))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _memory = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProcessorsError))]
    [NotifyPropertyChangedFor(nameof(HasProcessorsError))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _processors = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwapError))]
    [NotifyPropertyChangedFor(nameof(HasSwapError))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _swap = string.Empty;

    [ObservableProperty]
    private bool _localhostForwarding = true;

    [ObservableProperty]
    private string _networkingMode = "NAT";

    // ── Host info ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _hostInfo = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    private long _hostRamMb;
    private int _hostCpuCount;

    // ── Validation ────────────────────────────────────────────────────────────

    public string? MemoryError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Memory)) return null;
            if (!TryParseMemoryMb(Memory, out _))
                return Properties.Resources.WslConfig_InvalidMemory;
            return null;
        }
    }

    public bool HasMemoryError => MemoryError is not null;

    public string? ProcessorsError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Processors)) return null;
            if (!int.TryParse(Processors, out int p) || p < 1 || (_hostCpuCount > 0 && p > _hostCpuCount))
                return Properties.Resources.WslConfig_InvalidProcessors;
            return null;
        }
    }

    public bool HasProcessorsError => ProcessorsError is not null;

    public string? SwapError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Swap)) return null;
            // "0" means disabled, otherwise must be parseable
            if (Swap.Trim() == "0") return null;
            if (!TryParseMemoryMb(Swap, out _))
                return Properties.Resources.WslConfig_InvalidSwap;
            return null;
        }
    }

    public bool HasSwapError => SwapError is not null;

    public bool ShowHighMemoryWarning
    {
        get
        {
            if (_hostRamMb <= 0) return false;
            if (!TryParseMemoryMb(Memory, out long memMb)) return false;
            return memMb > _hostRamMb * 0.8;
        }
    }

    public bool CanSave => !IsLoading && Fields.Any(f => f.IsDirty && f.IsSupported) && Fields.All(f => string.IsNullOrEmpty(f.ValidationError));

    public WslConfigSectionViewModel(
        IPowerShellModuleClient moduleClient,
        IDialogService dialogService)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task LoadAsync()
    {
        if (_initialized) return;
        _initialized = true;

        IsLoading = true;
        try
        {
            var snapshot = await _moduleClient.GetGlobalConfigurationAsync();
            var ramMb = snapshot.HostRamMb; var cpuCount = snapshot.HostCpuCount;
            _hostRamMb = ramMb;
            _hostCpuCount = cpuCount;
            HostInfo = string.Format(Properties.Resources.WslConfig_HostInfo, ramMb, cpuCount);

            snapshot.Values.TryGetValue("wsl2.memory", out var memory); Memory = memory ?? string.Empty;
            snapshot.Values.TryGetValue("wsl2.processors", out var processors); Processors = processors ?? string.Empty;
            snapshot.Values.TryGetValue("wsl2.swap", out var swap); Swap = swap ?? string.Empty;
            LocalhostForwarding = snapshot.Values.TryGetValue("wsl2.localhostForwarding", out var forwarding) ? bool.TryParse(forwarding, out var boolForwarding) && boolForwarding : true;
            snapshot.Values.TryGetValue("wsl2.networkingMode", out var networkingMode); NetworkingMode = networkingMode ?? "NAT";
            _availableCapabilities = snapshot.Capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase);
            CurrentRaw = DesiredRaw = snapshot.DisplayPreview;
            PendingRestart = L("Configuration_PendingWslRestart");
            Fields.Clear();
            foreach (var definition in WslConfigurationSchema.Global)
            {
                var id = $"{definition.Section}.{definition.Key}";
                var supported = definition.RequiredCapability is null || _availableCapabilities.Contains(definition.RequiredCapability);
                snapshot.Values.TryGetValue(id, out var current);
                var field = new ConfigurationSettingFieldViewModel(definition, current, supported,
                    supported ? string.Empty : L("Configuration_UnsupportedReason"), L("Configuration_Experimental"));
                field.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ConfigurationSettingFieldViewModel.Desired)) FieldChanged(field); };
                Fields.Add(field);
            }
            OnPropertyChanged(nameof(CanSave)); SaveAndMarkPendingRestartCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) when (IsConfigurationFailure(ex))
        {
            await ShowConfigurationFailureAsync(ex, "read");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAndMarkPendingRestartAsync()
    {
        IsLoading = true;
        try
        {
            var changes = Fields.Where(f => f.IsDirty && f.IsSupported).ToDictionary(f => f.Id,
                f => string.IsNullOrWhiteSpace(f.Desired) ? null : f.Desired, StringComparer.OrdinalIgnoreCase);
            if (changes.Count == 0) return;
            var preview = await _moduleClient.GetGlobalConfigurationPreviewAsync(changes);
            DesiredRaw = preview.DisplayPreview;
            var running = (await _moduleClient.GetInstancesAsync()).Where(i => i.IsRunning).Select(i => i.Name).ToArray();
            var message = string.Format(L("Configuration_SavePreview"),
                string.Join(", ", preview.ChangedSettings),
                running.Length == 0 ? L("Configuration_NoRunningInstances") : string.Join(", ", running),
                preview.DisplayPreview);
            if (!await _dialogService.ShowConfirmAsync(L("WslConfig_SaveAndMarkPendingRestart"), message)) return;
            var saved = await _moduleClient.SetGlobalConfigurationAsync(preview.PreviewToken);
            CurrentRaw = DesiredRaw;
            foreach (var field in Fields.Where(f => changes.ContainsKey(f.Id))) field.CommitDesired();
            PendingRestart = saved.PendingRestart ? L("Configuration_PendingWslRestart") : string.Empty;
            OnPropertyChanged(nameof(CanSave)); SaveAndMarkPendingRestartCommand.NotifyCanExecuteChanged();

            await _dialogService.ShowAlertAsync(
                L("WslConfig_SaveAndMarkPendingRestart"),
                L("WslConfig_SavePendingRestartComplete"));
        }
        catch (Exception ex) when (IsConfigurationFailure(ex))
        {
            await ShowConfigurationFailureAsync(ex, "write");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenRawFileAsync()
    {
        try
        {
            await _moduleClient.OpenWslConfigFileAsync();
        }
        catch (Exception ex)
        {
            // Fire-and-forget alert via async void (UI thread call)
            _ = _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Parses memory string like "4GB" / "512MB" / "4096" into megabytes.</summary>
    private static bool TryParseMemoryMb(string input, out long mb)
    {
        mb = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();

        if (input.EndsWith("gb", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(input[..^2], out double gb)) return false;
            mb = (long)(gb * 1024);
            return mb > 0;
        }
        if (input.EndsWith("mb", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(input[..^2], out mb)) return false;
            return mb > 0;
        }
        // raw number — treat as MB
        return long.TryParse(input, out mb) && mb > 0;
    }

    private static string L(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;

    private void FieldChanged(ConfigurationSettingFieldViewModel field)
    {
        field.ValidationError = string.Empty;
        if (!string.IsNullOrWhiteSpace(field.Desired))
        {
            var candidate = LosslessIniDocument.Empty().WithValue(field.Definition.Section, field.Definition.Key, field.Desired);
            var error = WslConfigurationSchema.Validate(candidate, WslConfigurationSchema.Global, _availableCapabilities).FirstOrDefault();
            if (error is not null) field.ValidationError = LocalizeDiagnostic(error);
        }
        OnPropertyChanged(nameof(CanSave)); SaveAndMarkPendingRestartCommand.NotifyCanExecuteChanged();
        _ = RefreshDesiredPreviewAsync();
    }

    private async Task RefreshDesiredPreviewAsync()
    {
        var changes = Fields.Where(f => f.IsDirty && f.IsSupported).ToDictionary(f => f.Id, f => string.IsNullOrWhiteSpace(f.Desired) ? null : f.Desired, StringComparer.OrdinalIgnoreCase);
        if (changes.Count == 0) { DesiredRaw = CurrentRaw; return; }
        try { DesiredRaw = (await _moduleClient.GetGlobalConfigurationPreviewAsync(changes)).DisplayPreview; }
        catch (Exception ex) when (IsConfigurationFailure(ex)) { await ShowConfigurationFailureAsync(ex, "write"); }
    }

    private static string LocalizeDiagnostic(ConfigurationDiagnostic diagnostic) => diagnostic.Code switch
    {
        "config.invalidValue" => L("Configuration_InvalidValue"),
        "config.unsupported" => L("Configuration_UnsupportedReason"),
        "config.malformed" => L("Configuration_Malformed"),
        _ => diagnostic.Message
    };

    private async Task ShowConfigurationFailureAsync(Exception exception, string operation)
    {
        var mapped = ConfigurationErrorMapper.ToOperationException(exception, operation);
        await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle,
            string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(mapped)));
    }

    private static bool IsConfigurationFailure(Exception exception) => exception is ConfigurationConflictException
        or ConfigurationValidationException or ConfigurationTransportException or IOException or WslOperationException or WslException;
}
