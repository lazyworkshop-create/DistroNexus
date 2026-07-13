using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Win32;
using System.Windows;
using System.Text.RegularExpressions;

namespace DistroNexus.Desktop.ViewModels;

public sealed partial class HealthFindingViewModel : ObservableObject
{
    public HealthFinding Finding { get; }
    public string Severity => Finding.Severity.ToString();
    // Checks deliberately keep stable, English diagnostic contracts.  Presentation owns the
    // localized display text so reports and persisted findings do not depend on the UI culture.
    public string Title => HealthFindingText.Title(Finding);
    public string Detail => HealthFindingText.Detail(Finding);
    public string Scope => HealthFindingText.Scope(Finding);
    public bool CanRepair => !string.IsNullOrWhiteSpace(Finding.RepairId);
    public HealthFindingViewModel(HealthFinding finding) => Finding = finding;
}

public sealed partial class DiagnosticLogSelectionViewModel : ObservableObject
{
    public string Id { get; }
    [ObservableProperty] private bool _isSelected;
    public DiagnosticLogSelectionViewModel(string id) => Id = id;
}

public partial class HealthCenterViewModel : ObservableObject, IDisposable
{
    private readonly IHealthOrchestrator _health;
    private readonly IHealthRepairService _repairs;
    private readonly IDiagnosticReportService _reports;
    private readonly IDiagnosticLogProvider _logs;
    private readonly IPlatformCapabilityService? _capabilities;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _reportCts;
    private CancellationTokenSource? _repairCts;
    public ObservableCollection<HealthFindingViewModel> Findings { get; } = [];
    public IReadOnlyList<HealthSeverity> Severities { get; } = Enum.GetValues<HealthSeverity>();
    [ObservableProperty] private HealthSeverity? _selectedSeverity;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isReporting;
    [ObservableProperty] private bool _isRepairing;
    [ObservableProperty] private bool _isHealthAvailable = true;
    [ObservableProperty] private string _healthAvailabilityReason = string.Empty;
    [ObservableProperty] private string _operationPhase = string.Empty;
    [ObservableProperty] private string _status = Properties.Resources.ResourceManager.GetString("Health_Ready") ?? "Ready";
    [ObservableProperty] private DateTimeOffset? _lastRun;
    [ObservableProperty] private string _diagnosticPreview = string.Empty;
    // Export is deliberately bound to the exact redacted snapshot the user reviewed.  A new
    // selection, format, or preview invalidates this token rather than exporting fresh data.
    private string? _diagnosticPreviewToken;
    [ObservableProperty] private DiagnosticReportFormat _diagnosticFormat = DiagnosticReportFormat.Markdown;
    [ObservableProperty] private bool _redactDiagnostic = true;
    [ObservableProperty] private string _repairDetails = string.Empty;
    public ObservableCollection<string> AvailableLogIds { get; } = [];
    public ObservableCollection<string> SelectedLogIds { get; } = [];
    public ObservableCollection<DiagnosticLogSelectionViewModel> DiagnosticLogs { get; } = [];
    public string? StatusCode => Regex.Match(Status, @"DN-\d{4}", RegexOptions.CultureInvariant).Success ? Regex.Match(Status, @"DN-\d{4}", RegexOptions.CultureInvariant).Value : null;
    public bool HasStatusCode => StatusCode is not null;

    public HealthCenterViewModel(IHealthOrchestrator health, IHealthRepairService repairs, IDiagnosticReportService reports, IDiagnosticLogProvider logs, IPlatformCapabilityService? capabilities = null)
    {
        (_health, _repairs, _reports, _logs, _capabilities) = (health, repairs, reports, logs, capabilities);
        foreach (var id in logs.AllowedLogIds.Order(StringComparer.Ordinal))
        {
            AvailableLogIds.Add(id);
            DiagnosticLogs.Add(new DiagnosticLogSelectionViewModel(id));
        }
    }
    public bool IsBusy => IsScanning || IsReporting || IsRepairing;
    public bool CanRunHealthActions => IsHealthAvailable && !IsBusy;
    public IEnumerable<HealthFindingViewModel> VisibleFindings => SelectedSeverity is null ? Findings : Findings.Where(x => x.Finding.Severity == SelectedSeverity);
    partial void OnSelectedSeverityChanged(HealthSeverity? value) => OnPropertyChanged(nameof(VisibleFindings));
    partial void OnStatusChanged(string value) { OnPropertyChanged(nameof(StatusCode)); OnPropertyChanged(nameof(HasStatusCode)); }
    partial void OnIsScanningChanged(bool value) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanRunHealthActions)); }
    partial void OnIsReportingChanged(bool value) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanRunHealthActions)); }
    partial void OnIsRepairingChanged(bool value) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanRunHealthActions)); }
    partial void OnIsHealthAvailableChanged(bool value) => OnPropertyChanged(nameof(CanRunHealthActions));
    partial void OnDiagnosticFormatChanged(DiagnosticReportFormat value) => _diagnosticPreviewToken = null;
    partial void OnRedactDiagnosticChanged(bool value) => _diagnosticPreviewToken = null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_capabilities is null) return;
        try
        {
            OperationPhase = L("Health_CheckingCapabilities", "Checking Health prerequisites…");
            var snapshot = await _capabilities.GetHostSnapshotAsync(cancellationToken: cancellationToken);
            if (!snapshot.Capabilities.TryGetValue(CapabilityId.Wsl, out var wsl) || !wsl.IsSupported)
            {
                IsHealthAvailable = false;
                HealthAvailabilityReason = string.Format(L("Health_PrerequisiteUnavailable", "Health scanning is unavailable: {0}"), wsl?.ReasonCode ?? L("Health_WslNotDetected", "WSL was not detected."));
                Status = HealthAvailabilityReason;
            }
            else { IsHealthAvailable = true; HealthAvailabilityReason = string.Empty; }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { IsHealthAvailable = false; HealthAvailabilityReason = HealthFindingText.Error(MainViewModel.FormatAlertMessage(ex)); Status = HealthAvailabilityReason; }
        finally { OperationPhase = string.Empty; }
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        if (!CanRunHealthActions) return;
        await InitializeAsync();
        if (!IsHealthAvailable || IsBusy) return;
        _scanCts = new CancellationTokenSource(); IsScanning = true; OperationPhase = L("Health_ScanPhase", "Running health checks…"); Status = L("Health_Scanning", "Scanning…"); Findings.Clear();
        try
        {
            var progress = new Progress<HealthFinding>(finding => { Findings.Add(new HealthFindingViewModel(finding)); OperationPhase = string.Format(L("Health_ScanFindingPhase", "Evaluating: {0}"), finding.Title); OnPropertyChanged(nameof(VisibleFindings)); });
            var scan = await _health.ScanAsync(progress, _scanCts.Token);
            LastRun = scan.CompletedAt; Status = scan.WasCancelled ? L("Health_Cancelled", "Scan cancelled") : string.Format(L("Health_Complete", "Scan complete: {0} findings"), Findings.Count);
        }
        catch (OperationCanceledException) { Status = L("Health_Cancelled", "Scan cancelled"); }
        catch (Exception ex) { Status = HealthFindingText.Error(MainViewModel.FormatAlertMessage(ex)); }
        finally { _scanCts.Dispose(); _scanCts = null; IsScanning = false; OperationPhase = string.Empty; }
    }
    [RelayCommand] private void CancelScan() => _scanCts?.Cancel();
    [RelayCommand]
    private void CopyStatusCode()
    {
        if (!string.IsNullOrWhiteSpace(StatusCode)) Clipboard.SetText(StatusCode);
    }
    [RelayCommand]
    private void CopyDiagnosticDetail(string? detail)
    {
        if (!string.IsNullOrWhiteSpace(detail)) Clipboard.SetText(SensitiveDataRedactor.Redact(detail));
    }
    [RelayCommand]
    private void ToggleDiagnosticLog(string? logId)
    {
        if (string.IsNullOrWhiteSpace(logId) || !AvailableLogIds.Contains(logId)) return;
        var selected = !SelectedLogIds.Contains(logId);
        if (selected) SelectedLogIds.Add(logId); else SelectedLogIds.Remove(logId);
        var item = DiagnosticLogs.FirstOrDefault(x => x.Id == logId);
        if (item is not null) item.IsSelected = selected;
        _diagnosticPreviewToken = null;
    }
    public bool IsDiagnosticLogSelected(string logId) => SelectedLogIds.Contains(logId);
    [RelayCommand]
    private async Task RepairAsync(HealthFindingViewModel? finding)
    {
        if (finding is null || !finding.CanRepair) return;
        if (!CanRunHealthActions) return;
        _repairCts = new CancellationTokenSource();
        IsRepairing = true;
        OperationPhase = L("Health_RepairPhase", "Preparing and applying repair…");
        try
        {
            var preview = await _repairs.PreviewAsync(finding.Finding, _repairCts.Token);
            if (string.IsNullOrWhiteSpace(preview.PreviewToken)) { Status = "DN-7002: " + L("Health_RepairPreviewUnavailable", "Repair preview was unavailable."); return; }
            RepairDetails = Describe(preview);
            var recoveryOffer = await _repairs.GetRecoveryOfferAsync(finding.Finding, _repairCts.Token);
            if (recoveryOffer?.IsAvailable == true && MessageBox.Show(L("Recovery_OfferRepair", "A recovery point is available before this repair."), L("Recovery_OfferTitle", "Optional recovery point"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            { Status = L("Health_RepairCancelled", "Repair cancelled."); return; }
            var confirmed = preview.Safety == RepairSafety.Safe || MessageBox.Show(RepairDetails, preview.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
            if (!confirmed) { Status = L("Health_RepairCancelled", "Repair cancelled."); return; }
            OperationPhase = L("Health_RepairExecutingPhase", "Applying repair…");
            var result = await _repairs.ExecuteAsync(finding.Finding, new RepairExecutionRequest(preview.PreviewToken, true), _repairCts.Token);
            RepairDetails = string.Join(Environment.NewLine, result.Results.Concat(result.NextSteps ?? []).Concat(result.Error is null ? [] : [result.Error]));
            Status = result.Succeeded ? string.Join(" ", result.Results) : HealthFindingText.Error(result.Error ?? "DN-7005");
        }
        catch (OperationCanceledException) { Status = L("Health_RepairCancelled", "Repair cancelled."); }
        catch (Exception ex) { Status = HealthFindingText.Error(MainViewModel.FormatAlertMessage(ex)); }
        finally { _repairCts.Dispose(); _repairCts = null; IsRepairing = false; OperationPhase = string.Empty; }
    }
    [RelayCommand] private void CancelRepair() => _repairCts?.Cancel();
    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        if (!CanRunHealthActions) return;
        _reportCts = new CancellationTokenSource(); IsReporting = true; OperationPhase = L("Health_ReportPreviewPhase", "Preparing redacted diagnostic preview…");
        try
        {
            var preview = await _reports.PreviewAsync(new DiagnosticReportRequest(DiagnosticFormat, RedactDiagnostic, SelectedLogIds), _reportCts.Token);
            _diagnosticPreviewToken = preview.SnapshotToken;
            DiagnosticPreview = preview.Content;
            Status = string.Format(L("Health_DiagnosticGenerated", "Diagnostic preview generated ({0} characters, redacted)."), preview.Content.Length);
        }
        catch (OperationCanceledException) { Status = L("Health_ReportCancelled", "Diagnostic report operation cancelled."); }
        catch (Exception ex) { Status = HealthFindingText.Error(MainViewModel.FormatAlertMessage(ex)); }
        finally { _reportCts.Dispose(); _reportCts = null; IsReporting = false; OperationPhase = string.Empty; }
    }
    [RelayCommand]
    private async Task SaveDiagnosticsAsync()
    {
        if (string.IsNullOrWhiteSpace(_diagnosticPreviewToken))
        {
            Status = "DN-7008: " + L("Health_DiagnosticPreviewRequired", "Preview the diagnostic report before saving it.");
            return;
        }
        var extension = DiagnosticFormat == DiagnosticReportFormat.Json ? ".json" : ".md";
        var dialog = new SaveFileDialog { Filter = DiagnosticFormat == DiagnosticReportFormat.Json ? "JSON report (*.json)|*.json" : "Markdown report (*.md)|*.md", DefaultExt = extension, AddExtension = true, FileName = "distronexus-diagnostics" + extension };
        if (dialog.ShowDialog() != true) return;
        if (!CanRunHealthActions) return;
        _reportCts = new CancellationTokenSource(); IsReporting = true; OperationPhase = L("Health_ReportExportPhase", "Saving redacted diagnostic report…");
        try
        {
            var path = await _reports.ExportAsync(new DiagnosticReportRequest(DiagnosticFormat, RedactDiagnostic, SelectedLogIds, _diagnosticPreviewToken), dialog.FileName, _reportCts.Token);
            _diagnosticPreviewToken = null; // Report snapshots are single-use.
            Status = string.Format(L("Health_DiagnosticSaved", "Diagnostic report saved: {0}"), path);
        }
        catch (OperationCanceledException) { Status = L("Health_ReportCancelled", "Diagnostic report operation cancelled."); }
        catch (Exception ex) { Status = HealthFindingText.Error(MainViewModel.FormatAlertMessage(ex)); }
        finally { _reportCts.Dispose(); _reportCts = null; IsReporting = false; OperationPhase = string.Empty; }
    }
    [RelayCommand] private void CancelReport() => _reportCts?.Cancel();
    public void Dispose() { _scanCts?.Cancel(); _scanCts?.Dispose(); _reportCts?.Cancel(); _reportCts?.Dispose(); _repairCts?.Cancel(); _repairCts?.Dispose(); }
    private static string Describe(RepairPreview preview) => string.Join(Environment.NewLine,
        preview.Changes.Concat(preview.Commands).Concat(preview.Preconditions ?? []).Concat(["Reversibility: " + (preview.Reversibility ?? "not specified")]).Concat(preview.UndoSteps ?? []));
    private static string L(string key, string fallback) => Properties.Resources.ResourceManager.GetString(key) ?? fallback;
}

internal static class HealthFindingText
{
    public static string Title(HealthFinding finding) => R(Category(finding) + "Title", finding.Title);
    public static string Detail(HealthFinding finding) => R(Category(finding) + "Detail", finding.Detail);
    public static string Scope(HealthFinding finding) => finding.Scope == HealthScope.Instance
        ? string.Format(R("Health_ScopeInstance", "Instance: {0}"), finding.InstanceName ?? R("Health_ScopeInstanceUnknown", "unknown"))
        : R("Health_ScopeHost", "Host");

    public static string Error(string message)
    {
        var match = Regex.Match(message ?? string.Empty, @"DN-(7\d{3})", RegexOptions.CultureInvariant);
        if (!match.Success) return message;
        return R("Health_Error_DN" + match.Groups[1].Value, message);
    }

    private static string Category(HealthFinding finding) => finding.Id switch
    {
        var id when id.StartsWith("host.wsl.", StringComparison.Ordinal) => "Health_FindingWsl",
        var id when id.StartsWith("host.kernel.", StringComparison.Ordinal) => "Health_FindingKernel",
        var id when id.StartsWith("host.wslg.", StringComparison.Ordinal) => "Health_FindingWslg",
        var id when id.StartsWith("network.", StringComparison.Ordinal) => "Health_FindingNetwork",
        var id when id.StartsWith("proxy.", StringComparison.Ordinal) => "Health_FindingProxy",
        var id when id.StartsWith("vpn.", StringComparison.Ordinal) => "Health_FindingVpn",
        var id when id.StartsWith("template.", StringComparison.Ordinal) => "Health_FindingTemplate",
        var id when id.StartsWith("windows.", StringComparison.Ordinal) => "Health_FindingWindows",
        var id when id.StartsWith("wslconfig.", StringComparison.Ordinal) || id.StartsWith("wslconf.", StringComparison.Ordinal) => "Health_FindingConfiguration",
        _ => "Health_FindingUnknown"
    };
    private static string R(string key, string fallback) => Properties.Resources.ResourceManager.GetString(key) ?? fallback;
}
