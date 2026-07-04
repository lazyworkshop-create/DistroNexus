using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using System.Diagnostics;
using System.IO;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// ViewModel for the WSL Global Configuration editor section in the Settings page.
/// Covers requirement E-01.
/// </summary>
public partial class WslConfigSectionViewModel : ObservableObject
{
    private readonly IWslConfigService _wslConfigService;
    private readonly IWslManagerService _wslManager;
    private readonly IDialogService _dialogService;
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

    public bool CanSave => !HasMemoryError && !HasProcessorsError && !HasSwapError;

    public WslConfigSectionViewModel(
        IWslConfigService wslConfigService,
        IWslManagerService wslManager,
        IDialogService dialogService)
    {
        _wslConfigService = wslConfigService ?? throw new ArgumentNullException(nameof(wslConfigService));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task LoadAsync()
    {
        if (_initialized) return;
        _initialized = true;

        IsLoading = true;
        try
        {
            var (ramMb, cpuCount) = await _wslConfigService.GetHostSpecsAsync();
            _hostRamMb = ramMb;
            _hostCpuCount = cpuCount;
            HostInfo = string.Format(Properties.Resources.WslConfig_HostInfo, ramMb, cpuCount);

            var config = await _wslConfigService.GetWslConfigAsync();
            Memory = config.Memory ?? string.Empty;
            Processors = config.Processors.HasValue ? config.Processors.Value.ToString() : string.Empty;
            Swap = config.Swap ?? string.Empty;
            LocalhostForwarding = config.LocalhostForwarding ?? true;
            NetworkingMode = config.NetworkingMode ?? "NAT";
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
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAndRestartAsync()
    {
        var config = new WslConfig
        {
            Memory     = string.IsNullOrWhiteSpace(Memory) ? null : Memory.Trim(),
            Processors = int.TryParse(Processors, out int p) ? p : null,
            Swap       = string.IsNullOrWhiteSpace(Swap) ? null : Swap.Trim(),
            LocalhostForwarding = LocalhostForwarding,
            NetworkingMode = string.IsNullOrWhiteSpace(NetworkingMode) ? null : NetworkingMode.Trim()
        };

        IsLoading = true;
        try
        {
            await _wslConfigService.SetWslConfigAsync(config);

            bool restart = await _dialogService.ShowConfirmAsync(
                Properties.Resources.WslConfig_SaveAndRestart,
                Properties.Resources.WslConfig_RestartConfirm);

            if (restart)
                await _wslManager.ShutdownWslAsync();

            await _dialogService.ShowAlertAsync(
                Properties.Resources.WslConfig_SaveAndRestart,
                Properties.Resources.WslConfig_SaveComplete);
        }
        catch (WslOperationException ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
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
        }
    }

    [RelayCommand]
    private void OpenRawFile()
    {
        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

        try
        {
            Process.Start(new ProcessStartInfo(configPath) { UseShellExecute = true });
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
}
