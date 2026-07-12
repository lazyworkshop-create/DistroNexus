using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using DistroNexus.Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for a single port mapping row in the Network tab grid.
/// </summary>
public class PortMappingViewModel
{
    public string Protocol       { get; init; } = string.Empty;
    public string LocalAddress   { get; init; } = string.Empty;
    public int    Port           { get; init; }
    public string ProcessName    { get; init; } = string.Empty;
    public bool   HasWindowsProxy { get; init; }
    public string AddressFamily { get; init; } = string.Empty;
    public bool HasWindowsCollision { get; init; }
    public string ConflictGuidance { get; init; } = string.Empty;
    public string CopyText => $"{LocalAddress}:{Port}";
}

/// <summary>
/// ViewModel for the Network tab of InstanceDetailDialog.
/// Displays WSL IP address and port mappings (C-02).
/// </summary>
public partial class NetworkTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly INetworkService _networkService;
    private readonly IDialogService _dialogService;
    private readonly INetworkDiagnosticsService _diagnostics;
    private readonly IFirewallOperationBroker _firewall;
    private readonly INetworkConfigurationService _networkConfiguration;
    private readonly INetworkStatusAdapter _networkStatus;
    private readonly IBrowserLauncher _browserLauncher;

    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _instanceIp = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PortMappingViewModel> _portMappings = [];

    [ObservableProperty]
    private bool _showStoppedPlaceholder;
    [ObservableProperty] private string _probeHost = "localhost";
    [ObservableProperty] private int _probePort = 80;
    [ObservableProperty] private NetworkProbeKind _probeKind = NetworkProbeKind.TcpEndpoint;
    [ObservableProperty] private string _probeResult = string.Empty;
    [ObservableProperty] private string _firewallResult = string.Empty;
    [ObservableProperty] private ObservableCollection<WslNetworkingMode> _availableModes = [];
    [ObservableProperty] private WslNetworkingMode _selectedNetworkingMode = WslNetworkingMode.Nat;
    [ObservableProperty] private string _networkingModeEvidence = string.Empty;
    [ObservableProperty] private string _networkingModeRestartImpact = string.Empty;
    [ObservableProperty] private bool _isNetworkingModeAvailable;
    [ObservableProperty] private string _firewallStatus = string.Empty;
    [ObservableProperty] private string _collisionStatus = string.Empty;
    [ObservableProperty] private string _firewallRuleId = string.Empty;
    [ObservableProperty] private string _ownedFirewallRules = string.Empty;
    [ObservableProperty] private bool? _dnsTunnelingEnabled;
    [ObservableProperty] private bool? _autoProxyEnabled;
    [ObservableProperty] private bool? _firewallEnabled;
    [ObservableProperty] private bool? _hostAddressLoopbackEnabled;
    [ObservableProperty] private bool? _bestEffortDnsParsingEnabled;
    [ObservableProperty] private string? _ignoredPorts;
    [ObservableProperty] private string _networkSettingsEvidence = string.Empty;
    public bool IsNetworkSettingsAvailable => IsNetworkingModeAvailable;
    public IReadOnlyList<NetworkProbeKind> ProbeKinds { get; } = Enum.GetValues<NetworkProbeKind>();

    public WslInstanceViewModel Instance => _instance;

    public NetworkTabViewModel(
        WslInstanceViewModel instance,
        INetworkService networkService,
        IDialogService dialogService,
        INetworkDiagnosticsService diagnostics,
        IFirewallOperationBroker firewall,
        INetworkConfigurationService networkConfiguration,
        INetworkStatusAdapter networkStatus,
        IBrowserLauncher browserLauncher)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _firewall = firewall ?? throw new ArgumentNullException(nameof(firewall));
        _networkConfiguration = networkConfiguration ?? throw new ArgumentNullException(nameof(networkConfiguration));
        _networkStatus = networkStatus ?? throw new ArgumentNullException(nameof(networkStatus));
        _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await RefreshNetworkAsync();
        await RefreshNetworkingModesAsync();
        var settings = await _networkConfiguration.ReadSettingsAsync() ?? new NetworkSettings();
        DnsTunnelingEnabled = settings.DnsTunneling; AutoProxyEnabled = settings.AutoProxy; FirewallEnabled = settings.Firewall; HostAddressLoopbackEnabled = settings.HostAddressLoopback; BestEffortDnsParsingEnabled = settings.BestEffortDnsParsing; IgnoredPorts = settings.IgnoredPorts;
        await RefreshOwnedFirewallRulesAsync();
    }

    [RelayCommand]
    private async Task RefreshNetworkAsync()
    {
        if (!Instance.IsRunning)
        {
            ShowStoppedPlaceholder = true;
            return;
        }

        ShowStoppedPlaceholder = false;
        IsLoading = true;
        _instance.IsBusy = true;
        try
        {
            var ip = await _networkService.GetInstanceIpAddressAsync(_instance.Name);
            InstanceIp = ip ?? string.Empty;

            var mappings = await _networkService.GetPortMappingsAsync(_instance.Name);
            var collisions = await _networkStatus.GetPortCollisionsAsync(mappings);
            var collisionByPort = collisions.ToDictionary(x => (x.Port, x.Protocol), x => x);
            PortMappings = new ObservableCollection<PortMappingViewModel>(
                mappings.Select(m => new PortMappingViewModel
                {
                    Protocol       = m.Protocol,
                    LocalAddress   = m.LocalAddress,
                    Port           = m.Port,
                    ProcessName    = m.ProcessName,
                    HasWindowsProxy = m.HasWindowsProxy,
                    AddressFamily = m.AddressFamily,
                    HasWindowsCollision = collisionByPort.TryGetValue((m.Port, m.Protocol), out var collision) && collision.IsCollision,
                    ConflictGuidance = collisionByPort.TryGetValue((m.Port, m.Protocol), out collision) ? collision.Detail : m.ConflictGuidance ?? string.Empty
                }));
            var firewall = await _networkStatus.GetFirewallStatusAsync();
            FirewallStatus = $"{firewall.Availability}: {firewall.Detail}";
            CollisionStatus = string.Join(Environment.NewLine, collisions.Select(x => $"{x.Protocol}/{x.Port}: {x.Detail}"));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsLoading = false;
            _instance.IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyAddress(PortMappingViewModel? row)
    {
        if (row is null) return;
        try { Clipboard.SetText(row.CopyText); }
        catch { /* ignore clipboard failures */ }
    }

    [RelayCommand]
    private void OpenInBrowser(PortMappingViewModel? row)
    {
        if (row is null) return;
        var uri = SafeBrowserUri.FromPortMapping(new PortMapping { LocalAddress = row.LocalAddress, Port = row.Port });
        if (uri is null) { NetworkSettingsEvidence = R("Network_ErrorUnsafeBrowserAddress"); return; }
        try { _browserLauncher.Open(uri); }
        catch (Exception ex) { NetworkSettingsEvidence = ex.Message; }
    }

    [RelayCommand]
    private async Task RunProbeAsync()
    {
        var result = await _diagnostics.ProbeAsync(new NetworkProbeRequest(ProbeKind, ProbeHost, ProbeKind == NetworkProbeKind.Dns ? null : ProbePort, DistributionName: _instance.Name));
        ProbeResult = $"{result.Outcome}: {result.Detail}";
    }

    [RelayCommand]
    private async Task PreviewFirewallAsync()
    {
        try
        {
            var preview = await _firewall.PreviewCreateAsync(new FirewallRuleRequest(FirewallDirection.Inbound, FirewallProtocol.Tcp, ProbePort, ["Private"]));
            if (!await _dialogService.ShowConfirmAsync(R("Network_ConfirmFirewallTitle"), string.Join(Environment.NewLine, preview.Effects))) return;
            var result = await _firewall.CreateAsync(preview);
            FirewallResult = result.Guidance ?? result.OutcomeCode;
        }
        catch (Exception ex) { FirewallResult = ex.Message; }
    }

    [RelayCommand]
    private async Task PreviewRemoveFirewallAsync()
    {
        try
        {
            var preview = await _firewall.PreviewRemoveAsync(FirewallRuleId);
            if (!await _dialogService.ShowConfirmAsync(R("Network_ConfirmFirewallRemovalTitle"), string.Join(Environment.NewLine, preview.Effects))) return;
            var result = await _firewall.RemoveAsync(preview);
            FirewallResult = result.Guidance ?? result.OutcomeCode;
            await RefreshOwnedFirewallRulesAsync();
        }
        catch (Exception ex) { FirewallResult = ex.Message; }
    }

    private async Task RefreshOwnedFirewallRulesAsync()
    {
        var rules = await _firewall.ListOwnedAsync() ?? [];
        OwnedFirewallRules = string.Join(Environment.NewLine, rules.Select(x => x.RuleId));
    }

    [RelayCommand]
    private async Task RefreshNetworkingModesAsync()
    {
        var supported = new List<WslNetworkingMode>(); var notes = new List<string>();
        foreach (var mode in Enum.GetValues<WslNetworkingMode>())
        {
            var guidance = await _networkConfiguration.GetGuidanceAsync(mode);
            if (guidance.IsSupported) supported.Add(mode); else notes.AddRange(guidance.CompatibilityNotes);
        }
        AvailableModes = new ObservableCollection<WslNetworkingMode>(supported);
        IsNetworkingModeAvailable = supported.Count > 0;
        OnPropertyChanged(nameof(IsNetworkSettingsAvailable));
        if (supported.Count > 0 && !supported.Contains(SelectedNetworkingMode)) SelectedNetworkingMode = supported[0];
        NetworkingModeEvidence = string.Join(Environment.NewLine, notes.Distinct());
    }

    [RelayCommand]
    private async Task PreviewAndApplyNetworkingModeAsync()
    {
        try
        {
            var preview = await _networkConfiguration.PreviewModeAsync(SelectedNetworkingMode);
            NetworkingModeRestartImpact = preview.Configuration.RestartScope == RestartScope.Wsl ? R("Network_RestartRequired") : R("Network_NoRestartRequired");
            var message = string.Join(Environment.NewLine, preview.Guidance.CompatibilityNotes.Append(NetworkingModeRestartImpact).Append(preview.Configuration.DesiredRaw));
            if (!await _dialogService.ShowConfirmAsync(R("Network_ConfirmModeTitle"), message)) return;
            var result = await _networkConfiguration.ApplyModeAsync(SelectedNetworkingMode, preview.Token);
            NetworkingModeEvidence = result.RestartScope == RestartScope.Wsl ? R("Network_RestartRequired") : R("Network_ModeApplied");
            await RefreshNetworkingModesAsync();
        }
        catch (Exception ex) { NetworkingModeEvidence = ex.Message; }
    }

    [RelayCommand]
    private async Task PreviewAndApplyNetworkSettingsAsync()
    {
        try
        {
            var settings = new NetworkSettings(DnsTunnelingEnabled, AutoProxyEnabled, FirewallEnabled, HostAddressLoopbackEnabled, BestEffortDnsParsingEnabled, IgnoredPorts);
            var preview = await _networkConfiguration.PreviewSettingsAsync(settings);
            var restart = preview.Configuration.RestartScope == RestartScope.Wsl ? R("Network_RestartRequired") : R("Network_NoRestartRequired");
            var message = string.Join(Environment.NewLine, preview.Configuration.ChangedSettings.Append(restart).Append(preview.Configuration.DesiredRaw));
            if (!await _dialogService.ShowConfirmAsync(R("Network_ConfirmSettingsTitle"), message)) return;
            var result = await _networkConfiguration.ApplySettingsAsync(settings, preview.Token);
            NetworkSettingsEvidence = result.RestartScope == RestartScope.Wsl ? R("Network_SettingsAppliedRestart") : R("Network_SettingsApplied");
        }
        catch (Exception ex) { NetworkSettingsEvidence = ex.Message; }
    }

    private static string R(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
}
