using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>Renders only the fixed, reviewed compaction result returned by the module.</summary>
public partial class DiskTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IPowerShellModuleClient _moduleClient;
    private readonly IDialogService _dialogService;
    private bool _initialized;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isCompacting;
    [ObservableProperty] private string _phaseText = string.Empty;
    [ObservableProperty] private bool _showResult;
    [ObservableProperty] private string _beforeSizeDisplay = string.Empty;
    [ObservableProperty] private string _afterSizeDisplay = string.Empty;
    [ObservableProperty] private string _savedSizeDisplay = string.Empty;
    [ObservableProperty] private string _estimateKind = "Unknown";
    [ObservableProperty] private string _warningsDisplay = string.Empty;
    [ObservableProperty] private string _outcomeCode = string.Empty;

    public WslInstanceViewModel Instance => _instance;
    public bool IsWslV1 => !_instance.IsWslV2;

    public DiskTabViewModel(WslInstanceViewModel instance, IPowerShellModuleClient moduleClient, IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CompactDiskAsync(CancellationToken cancellationToken) => RunCompactionAsync(cancellationToken);

    public async Task RunCompactionAsync(CancellationToken cancellationToken = default)
    {
        if (!_instance.IsWslV2 || IsCompacting) return;
        try
        {
            IsCompacting = true;
            _instance.IsBusy = true;
            ShowResult = false;
            PhaseText = Properties.Resources.DiskTab_PhaseCompact;
            var result = await _moduleClient.CompactInstanceAsync(_instance.Name, cancellationToken);
            OutcomeCode = result.OutcomeCode;
            if (!result.Succeeded) throw new InvalidOperationException(result.OutcomeCode);

            BeforeSizeDisplay = FormatBytes(result.BeforeBytes);
            AfterSizeDisplay = FormatBytes(result.AfterBytes);
            SavedSizeDisplay = FormatBytes(result.SavedBytes);
            EstimateKind = result.SavedBytes.HasValue ? "Measured" : "Unknown";
            WarningsDisplay = result.RecoveryAction == "None" ? string.Empty : result.RecoveryAction;
            if (result.AfterBytes is long after) _instance.UpdateDiskSize(after);
            ShowResult = true;
        }
        catch (OperationCanceledException) { OutcomeCode = "Lifecycle.CompactionCancelled"; }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            PhaseText = string.Empty;
            IsCompacting = false;
            _instance.IsBusy = false;
        }
    }

    private static string FormatBytes(long? bytes) => bytes is null ? "Unknown" : FormatBytes(bytes.Value);
    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var order = 0; double size = bytes;
        while (size >= 1024 && order < units.Length - 1) { size /= 1024; order++; }
        return $"{size:0.##} {units[order]}";
    }
}
