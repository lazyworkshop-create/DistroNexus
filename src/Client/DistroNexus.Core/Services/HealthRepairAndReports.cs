using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class HealthRepairService : IHealthRepairService
{
    private readonly IReadOnlyDictionary<string, IRepairAction> _actions;
    private readonly IRecoveryOfferService? _recoveryOffers;
    private readonly Dictionary<string, (HealthFinding Finding, RepairPreview Preview)> _previews = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    public HealthRepairService(IEnumerable<IRepairAction> actions, IRecoveryOfferService? recoveryOffers = null) { _actions = actions.ToDictionary(x => x.Id, StringComparer.Ordinal); _recoveryOffers = recoveryOffers; }

    public async Task<RecoveryOffer> GetRecoveryOfferAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(finding.InstanceName)) return new(false, "", RecoveryOfferReason.DestructiveRepair, "RecoveryOffer.InstanceRequired");
        var preview = await PreviewAsync(finding, cancellationToken);
        if (preview.Safety == RepairSafety.Safe) return new(false, finding.InstanceName, RecoveryOfferReason.DestructiveRepair, "RecoveryOffer.NotRequired");
        return _recoveryOffers is null ? new(false, finding.InstanceName, RecoveryOfferReason.DestructiveRepair, "RecoveryOffer.Unavailable") : await _recoveryOffers.GetOfferAsync(finding.InstanceName, RecoveryOfferReason.DestructiveRepair, cancellationToken);
    }
    public async Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var preview = await Action(finding).PreviewAsync(finding, cancellationToken).ConfigureAwait(false);
        var token = Guid.NewGuid().ToString("N");
        preview = preview with
        {
            PreviewToken = token,
            Preconditions = (preview.Preconditions ?? []).Concat(["The finding must still identify this repair.", "Review the listed changes before continuing."]).Distinct(StringComparer.Ordinal).ToArray(),
            Reversibility = preview.Reversibility ?? (preview.BackupPath is null ? "No automatic undo is available." : "A backup is created before the change and can be restored manually."),
            UndoSteps = preview.UndoSteps ?? (preview.BackupPath is null ? [] : ["Restore the backup shown above if the result is not acceptable."])
        };
        lock (_sync) _previews[token] = (finding, preview);
        return preview;
    }
    public async Task<RepairResult> ExecuteAsync(HealthFinding finding, RepairExecutionRequest request, CancellationToken cancellationToken = default)
    {
        (HealthFinding Finding, RepairPreview Preview) saved;
        lock (_sync)
        {
            if (!_previews.Remove(request.PreviewToken, out saved!) || saved.Finding.Id != finding.Id || saved.Finding.RepairId != finding.RepairId)
                return new RepairResult(finding.RepairId ?? "unknown", false, [], Error: "DN-7002: Repair preview is missing, expired, or no longer matches the finding.");
        }
        if (saved.Preview.Safety != RepairSafety.Safe && !request.Confirmed)
            return new RepairResult(saved.Preview.RepairId, false, [], Error: "DN-7003: This repair requires explicit confirmation.");
        try
        {
            var result = await Action(finding).ExecuteAsync(finding, cancellationToken).ConfigureAwait(false);
            return result with
            {
                Results = result.Results.Concat(["Postcondition: repair action completed; rescan to verify the finding is resolved."]).ToArray(),
                Idempotency = saved.Preview.Idempotency,
                NextSteps = result.NextSteps ?? ["Rescan Health Center to verify the finding is resolved."]
            };
        }
        catch (Exception ex)
        {
            return new RepairResult(saved.Preview.RepairId, false, ["No further repair commands will be executed."], Error: "DN-7005: " + SensitiveDataRedactor.Redact(ex.Message));
        }
    }
    private IRepairAction Action(HealthFinding finding) => !string.IsNullOrWhiteSpace(finding.RepairId) && _actions.TryGetValue(finding.RepairId, out var action) ? action : throw new InvalidOperationException("No repair is available for this finding.");
}

/// <summary>Configuration repair intentionally only previews known safe edits; saving remains user-confirmed in the editor.</summary>
public sealed class OpenSettingsRepairAction : IRepairAction
{
    private readonly IHealthNavigationBroker _navigation;
    public string Id { get; }
    public OpenSettingsRepairAction(string id, IHealthNavigationBroker? navigation = null) => (Id, _navigation) = (id, navigation ?? NullHealthNavigationBroker.Instance);
    public Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default) => Task.FromResult(new RepairPreview(Id, "Open related settings", RepairSafety.Safe, RepairIdempotency.Idempotent, ["Open DistroNexus configuration settings."], []));
    public Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        _navigation.Request("settings", finding);
        return Task.FromResult(new RepairResult(Id, true, ["The Settings navigation request was sent. Edit and confirm the proposed configuration there."], PostconditionSatisfied: true));
    }
}

public sealed class NullHealthNavigationBroker : IHealthNavigationBroker
{
    public static NullHealthNavigationBroker Instance { get; } = new();
    public void Request(string target, HealthFinding finding) { }
}

/// <summary>
/// Represents a reviewed repair whose implementation deliberately belongs to the Desktop host.
/// Non-Desktop callers receive a typed, actionable result instead of an unregistered-action error.
/// </summary>
public sealed class DesktopOnlyRepairAction : IRepairAction
{
    private readonly string _title;
    private readonly RepairSafety _safety;
    public string Id { get; }

    public DesktopOnlyRepairAction(string id, string title, RepairSafety safety)
        => (Id, _title, _safety) = (id, title, safety);

    public Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RepairPreview(Id, _title, _safety, RepairIdempotency.Idempotent,
            ["This repair is available only from the DistroNexus Desktop application."], [],
            Preconditions: ["DN-7004: Desktop navigation or an approved elevation broker is required."]));

    public Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RepairResult(Id, false,
            ["No host change was started by this non-Desktop caller."],
            Error: "DN-7004: This repair is Desktop-only because it requires Desktop navigation or the approved elevation broker.",
            NextSteps: ["Open DistroNexus Desktop, review the repair preview, and explicitly confirm it there."]));
}

/// <summary>Removes only the malformed managed key identified by a Health finding, then verifies the saved document.</summary>
public sealed class GlobalConfigurationRepairAction : IRepairAction
{
    private readonly IWslConfigurationService _configuration;
    public string Id => "config.global.known-values";
    public GlobalConfigurationRepairAction(IWslConfigurationService configuration) => _configuration = configuration;
    public async Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var document = await _configuration.ReadAsync(cancellationToken).ConfigureAwait(false);
        var line = finding.Evidence is not null && finding.Evidence.TryGetValue("line", out var value) && int.TryParse(value, out var parsed) ? parsed : -1;
        var token = document.Source.Tokens.FirstOrDefault(x => x.Line == line && x.Key is not null);
        if (token?.Key is null) throw new InvalidOperationException("DN-7002: The configuration finding no longer identifies a repairable managed setting.");
        return new RepairPreview(Id, "Correct known global configuration value", RepairSafety.RequiresConfirmation, RepairIdempotency.Idempotent,
            [$"Remove invalid managed setting {token.Section}.{token.Key} from .wslconfig."], ["IWslConfigurationService.SaveAsync (atomic write)"], BackupPath: "Created on save", Preconditions: ["The source fingerprint must still match.", "A timestamped configuration backup is created before the write."]);
    }
    public async Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var document = await _configuration.ReadAsync(cancellationToken).ConfigureAwait(false);
        var line = finding.Evidence is not null && finding.Evidence.TryGetValue("line", out var value) && int.TryParse(value, out var parsed) ? parsed : -1;
        var token = document.Source.Tokens.FirstOrDefault(x => x.Line == line && x.Key is not null);
        if (token?.Key is null) return new RepairResult(Id, false, [], Error: "DN-7002: The configuration setting could not be safely identified.");
        var values = document.Settings.Values.ToDictionary(x => x.Key, x => (string?)x.Value, StringComparer.OrdinalIgnoreCase);
        values.Remove($"{token.Section}.{token.Key}"); values.Remove(token.Key);
        var save = await _configuration.SaveAsync(values, document.Fingerprint, cancellationToken: cancellationToken).ConfigureAwait(false);
        var after = await _configuration.ReadAsync(cancellationToken).ConfigureAwait(false);
        var fixedFinding = !after.Diagnostics.Any(x => x.Line == line && x.Severity == ConfigurationDiagnosticSeverity.Error);
        return new RepairResult(Id, fixedFinding, [$"Updated .wslconfig: removed {token.Section}.{token.Key}.", "Postcondition: configuration was re-read and the original line diagnostic is absent."], save.BackupPath, fixedFinding ? null : "DN-7006: Postcondition failed; restore the timestamped backup.", fixedFinding);
    }
}

/// <summary>Repairs a single invalid managed /etc/wsl.conf key and verifies the result by re-reading it.</summary>
public sealed class InstanceConfigurationRepairAction : IRepairAction
{
    private readonly IDistributionConfigurationService _configuration;
    public string Id => "config.instance.known-values";
    public InstanceConfigurationRepairAction(IDistributionConfigurationService configuration) => _configuration = configuration;
    public async Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(finding.InstanceName)) throw new InvalidOperationException("DN-7002: The instance configuration finding has no distribution.");
        var document = await _configuration.ReadAsync(finding.InstanceName, cancellationToken).ConfigureAwait(false);
        var token = Token(document, finding);
        if (token?.Key is null) throw new InvalidOperationException("DN-7002: The instance configuration finding no longer identifies a repairable setting.");
        return new RepairPreview(Id, "Correct known distribution configuration value", RepairSafety.RequiresConfirmation, RepairIdempotency.Idempotent,
            [$"Remove invalid managed setting {token.Section}.{token.Key} from /etc/wsl.conf."], ["IDistributionConfigurationService.SaveAsync (atomic helper write)"], BackupPath: "Created on save", Preconditions: ["The source fingerprint must still match.", "A timestamped configuration backup is created before the write."]);
    }
    public async Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(finding.InstanceName)) return new RepairResult(Id, false, [], Error: "DN-7002: The instance configuration finding has no distribution.");
        var document = await _configuration.ReadAsync(finding.InstanceName, cancellationToken).ConfigureAwait(false);
        var token = Token(document, finding);
        if (token?.Key is null) return new RepairResult(Id, false, [], Error: "DN-7002: The instance configuration setting could not be safely identified.");
        var values = document.Settings.Values.ToDictionary(x => x.Key, x => (string?)x.Value, StringComparer.OrdinalIgnoreCase);
        values.Remove($"{token.Section}.{token.Key}"); values.Remove(token.Key);
        var save = await _configuration.SaveAsync(finding.InstanceName, values, document.Fingerprint, cancellationToken).ConfigureAwait(false);
        var after = await _configuration.ReadAsync(finding.InstanceName, cancellationToken).ConfigureAwait(false);
        var fixedFinding = !after.Diagnostics.Any(x => x.Line == token.Line && x.Severity == ConfigurationDiagnosticSeverity.Error);
        return new RepairResult(Id, fixedFinding, [$"Updated /etc/wsl.conf: removed {token.Section}.{token.Key}.", "Postcondition: configuration was re-read and the original line diagnostic is absent."], save.BackupPath, fixedFinding ? null : "DN-7006: Postcondition failed; restore the timestamped backup.", fixedFinding);
    }
    private static ConfigurationToken? Token<T>(ConfigurationDocument<T> document, HealthFinding finding) where T : class
    {
        var line = finding.Evidence is not null && finding.Evidence.TryGetValue("line", out var value) && int.TryParse(value, out var parsed) ? parsed : -1;
        return document.Source.Tokens.FirstOrDefault(x => x.Line == line && x.Key is not null);
    }
}

/// <summary>Runs a fixed, reviewed process request only after HealthRepairService has confirmed a preview.</summary>
public sealed class FixedProcessRepairAction : IRepairAction
{
    private readonly IProcessRunner _runner;
    private readonly Func<HealthFinding, ProcessRequest?> _request;
    private readonly string _title;
    private readonly RepairSafety _safety;
    private readonly RepairIdempotency _idempotency;
    private readonly IReadOnlyList<string> _changes;
    private readonly Func<HealthFinding, ProcessRequest?>? _postcondition;
    public string Id { get; }
    public FixedProcessRepairAction(string id, string title, RepairSafety safety, RepairIdempotency idempotency, IReadOnlyList<string> changes, Func<HealthFinding, ProcessRequest?> request, IProcessRunner runner, Func<HealthFinding, ProcessRequest?>? postcondition = null)
    {
        (Id, _title, _safety, _idempotency, _changes, _request, _runner, _postcondition) = (id, title, safety, idempotency, changes, request, runner, postcondition);
    }
    public Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var request = _request(finding);
        return Task.FromResult(request is null
            ? new RepairPreview(Id, _title, _safety, _idempotency, _changes, [], Preconditions: ["DN-7001: This repair is unavailable for the current finding."])
            : new RepairPreview(Id, _title, _safety, _idempotency, _changes, [Render(request)], Preconditions: ["Review affected running distributions and confirm before execution."]));
    }
    public async Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var request = _request(finding);
        if (request is null) return new RepairResult(Id, false, [], Error: "DN-7001: This repair is unavailable for the current finding.");
        var result = await _runner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None)
        {
            var recheck = _postcondition?.Invoke(finding);
            if (recheck is null)
                return new RepairResult(Id, true, ["Fixed repair command completed.", "Postcondition was not automatically verifiable; rescan Health Center to verify the finding."], PostconditionSatisfied: false);

            // This is intentionally read-only.  A failed observation is not evidence that the
            // successful repair command failed, so preserve success and describe the uncertainty.
            var observed = await _runner.RunAsync(recheck, cancellationToken).ConfigureAwait(false);
            var verified = observed.ExitCode == 0 && !observed.TimedOut && !observed.Cancelled && observed.Failure == ProcessFailureKind.None;
            return verified
                ? new RepairResult(Id, true, ["Fixed repair command completed.", "Postcondition: a read-only verification command completed successfully."], PostconditionSatisfied: true)
                : new RepairResult(Id, true, ["Fixed repair command completed.", "Postcondition could not be verified automatically; rescan Health Center to verify the finding."], PostconditionSatisfied: false,
                    NextSteps: ["No additional repair command was run after the failed read-only verification."]);
        }
        return new RepairResult(Id, false, ["Fixed repair command did not complete."], Error: "DN-7005: " + SensitiveDataRedactor.Redact(result.StandardError));
    }
    private static string Render(ProcessRequest request) => request.FileName + " " + string.Join(" ", request.Arguments);
}

/// <summary>WSL shutdown is disruptive, so its preview names the currently running instances.</summary>
public sealed class WslRestartRepairAction : IRepairAction
{
    private readonly IWslManagerService _manager;
    private readonly IProcessRunner _runner;
    public string Id => "wsl.restart";
    public WslRestartRepairAction(IWslManagerService manager, IProcessRunner runner) => (_manager, _runner) = (manager, runner);
    public async Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var running = (await _manager.GetInstancesAsync(cancellationToken).ConfigureAwait(false)).Where(x => x.IsRunning).Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
        var changes = running.Length == 0
            ? new[] { "No running WSL distributions were detected; wsl.exe --shutdown will still reset the WSL VM." }
            : new[] { "The following running distributions will be stopped: " + string.Join(", ", running), "Unsaved work in these distributions can be interrupted." };
        return new RepairPreview(Id, "Restart WSL", RepairSafety.PrivilegedOrDisruptive, RepairIdempotency.Idempotent, changes,
            ["wsl.exe --shutdown"], Preconditions: ["Review the affected running distributions and explicitly confirm this disruptive action."]);
    }
    public async Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--shutdown"], TimeSpan.FromMinutes(1)), cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None)
            return new RepairResult(Id, false, ["WSL shutdown did not complete."], Error: "DN-7005: " + SensitiveDataRedactor.Redact(result.StandardError));

        // Query the manager's instance state instead of starting a distribution (or treating a
        // successful `wsl --status` as proof of shutdown).  This is read-only and proves the
        // actual postcondition required by the disruptive action.
        IReadOnlyList<WslInstance> instances;
        try { instances = await _manager.GetInstancesAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception)
        {
            return new RepairResult(Id, true, ["WSL shutdown completed.", "Postcondition could not be observed without starting a distribution."], PostconditionSatisfied: false,
                NextSteps: ["Rescan Health Center before relying on the repaired state."]);
        }
        var running = instances.Where(x => x.IsRunning).Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
        var verified = running.Length == 0;
        return verified
            ? new RepairResult(Id, true, ["WSL shutdown completed.", "Postcondition: no registered distribution is running."], PostconditionSatisfied: true)
            : new RepairResult(Id, true, ["WSL shutdown completed, but these distributions still report running: " + string.Join(", ", running) + "."], PostconditionSatisfied: false,
                NextSteps: ["No additional repair command was run after the failed read-only verification."]);
    }
}

/// <summary>Represents Windows-feature remediation without impersonating an elevated process.</summary>
public sealed class ElevationRequiredRepairAction : IRepairAction
{
    private readonly IWindowsFeatureRepairBroker _broker;
    public string Id => "enable.windows-features";
    public ElevationRequiredRepairAction(IWindowsFeatureRepairBroker? broker = null) => _broker = broker ?? new UnavailableWindowsFeatureRepairBroker();
    public Task<RepairPreview> PreviewAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var features = WindowsFeatures(finding);
        return Task.FromResult(new RepairPreview(Id, "Enable required Windows features", RepairSafety.PrivilegedOrDisruptive, RepairIdempotency.Idempotent,
            features.Select(x => "Enable Windows optional feature " + x + ".").Append("Restart Windows if prompted.").ToArray(),
            features.Select(x => "DISM.exe /Online /Enable-Feature /FeatureName:" + x + " /All /NoRestart").ToArray(), Preconditions: ["An explicit elevated Windows flow is required."]));
    }
    public Task<RepairResult> ExecuteAsync(HealthFinding finding, CancellationToken cancellationToken = default) => _broker.StartAsync(finding, cancellationToken);
    internal static IReadOnlyList<string> WindowsFeatures(HealthFinding finding)
    {
        var requested = finding.Evidence is not null && finding.Evidence.TryGetValue("feature", out var feature) ? [feature] : WindowsPrerequisiteProbe.RequiredFeatures;
        return requested.Where(x => WindowsPrerequisiteProbe.RequiredFeatures.Contains(x, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
    }
}

public sealed class UnavailableWindowsFeatureRepairBroker : IWindowsFeatureRepairBroker
{
    public Task<RepairResult> StartAsync(HealthFinding finding, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RepairResult("enable.windows-features", false, ["No feature change was started because an elevated Windows consent broker is unavailable."], Error: "DN-7004: Start this repair from the elevated Windows features flow."));
}

/// <summary>Launches the reviewed Windows Features command through UAC only after the caller has
/// presented and confirmed a repair preview. A successful launch is deliberately not reported as
/// successful feature installation; Windows remains authoritative and a rescan verifies it.</summary>
public sealed class ElevatedWindowsFeatureRepairBroker : IWindowsFeatureRepairBroker
{
    private readonly IProcessRunner _runner;
    public ElevatedWindowsFeatureRepairBroker(IProcessRunner runner) => _runner = runner;
    public async Task<RepairResult> StartAsync(HealthFinding finding, CancellationToken cancellationToken = default)
    {
        var features = ElevationRequiredRepairAction.WindowsFeatures(finding);
        if (features.Count == 0) return new RepairResult("enable.windows-features", false, [], Error: "DN-7001: No reviewed Windows feature was selected for this finding.");
        // DISM accepts one /FeatureName per operation.  Keep the UAC request one-to-one with the
        // reviewed allow-list so a preview and the actual elevated commands cannot diverge.
        foreach (var feature in features)
        {
            // -PassThru plus an explicit exit propagates DISM's exit code through the UAC
            // broker.  Without it PowerShell reports only that Start-Process launched.
            var command = "$p = Start-Process -FilePath 'dism.exe' -Verb RunAs -ArgumentList @('/Online','/Enable-Feature','/FeatureName:" + feature + "','/All','/NoRestart') -Wait -PassThru; exit $p.ExitCode";
            var result = await _runner.RunAsync(new ProcessRequest("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", command], TimeSpan.FromMinutes(5)), cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.Failure != ProcessFailureKind.None)
                return new RepairResult("enable.windows-features", false, ["The elevated Windows Features consent flow did not complete for " + feature + "."], Error: "DN-7004: " + SensitiveDataRedactor.Redact(result.StandardError));
        }
        // Query feature state only; this has no elevation or mutation side effect.
        var verified = true;
        foreach (var feature in features)
        {
            var observation = await _runner.RunAsync(new ProcessRequest("dism.exe", ["/Online", "/Get-FeatureInfo", "/FeatureName:" + feature], TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);
            verified &= observation.ExitCode == 0 && !observation.TimedOut && !observation.Cancelled && observation.Failure == ProcessFailureKind.None
                && observation.StandardOutput.Contains("Enabled", StringComparison.OrdinalIgnoreCase);
        }
        var lines = features.Select(x => "The elevated Windows Features operation completed for " + x + ".").ToList();
        lines.Add(verified ? "Postcondition: requested Windows feature state was observed as enabled." : "Postcondition could not be verified automatically; restart Windows if prompted, then rescan Health Center.");
        return new RepairResult("enable.windows-features", verified, lines, PostconditionSatisfied: verified,
            NextSteps: verified ? null : ["No additional repair command was run after the failed read-only verification."]);
    }
}

public sealed class DiagnosticReportService : IDiagnosticReportService
{
    private readonly IHealthOrchestrator _health;
    private readonly IPlatformCapabilityService _capabilities;
    private readonly IDiagnosticLogProvider _logs;
    private readonly IStructuredErrorProvider _errors;
    private readonly Dictionary<string, CachedReport> _snapshots = new(StringComparer.Ordinal);
    private readonly object _snapshotSync = new();
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMinutes(10);
    public DiagnosticReportService(IHealthOrchestrator health, IPlatformCapabilityService capabilities, IDiagnosticLogProvider logs, IStructuredErrorProvider errors) => (_health, _capabilities, _logs, _errors) = (health, capabilities, logs, errors);
    public async Task<DiagnosticReportPreview> PreviewAsync(DiagnosticReportRequest request, CancellationToken cancellationToken = default)
    {
        var host = await _capabilities.GetHostSnapshotAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var scan = await _health.ScanAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var selected = (request.SelectedLogs ?? []).Distinct(StringComparer.Ordinal).ToArray();
        var forbidden = selected.Except(_logs.AllowedLogIds, StringComparer.Ordinal).ToArray();
        if (forbidden.Length != 0) throw new InvalidOperationException("DN-7007: A selected diagnostic log is not allow-listed.");
        var logs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var logId in selected) logs[logId] = await _logs.ReadAsync(logId, 16 * 1024, cancellationToken).ConfigureAwait(false);
        var errors = await _errors.GetRecentAsync(100, cancellationToken).ConfigureAwait(false);
        var payload = new { generatedAt = DateTimeOffset.UtcNow, host = new { host.Host.WindowsVersion, host.Host.Architecture, host.Host.WslVersion, host.Host.KernelVersion, host.Host.WslgVersion }, capabilities = host.Capabilities.Values.Select(x => new { x.Id, x.Status, x.ReasonCode }), findings = scan.Findings.Select(x => new { x.Id, x.Severity, x.Scope, x.Title, Detail = x.Detail, x.InstanceName }), recentErrors = errors.Select(x => new { x.OccurredAt, x.Code, x.Operation, Message = SensitiveDataRedactor.RedactSecrets(x.Message) }), logs };
        var content = request.Format == DiagnosticReportFormat.Json ? JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) : Markdown(payload);
        // Secrets are never exported. Path and user redaction remains a user-visible report option.
        content = request.Redact ? SensitiveDataRedactor.Redact(content) : SensitiveDataRedactor.RedactSecrets(content);
        var token = Guid.NewGuid().ToString("N");
        lock (_snapshotSync)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var expired in _snapshots.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) _snapshots.Remove(expired);
            _snapshots[token] = new CachedReport(request.Format, content, now.Add(SnapshotLifetime));
        }
        return new DiagnosticReportPreview(request.Format, content, ["versions", "capabilities", "findings", "recent structured errors", "selected logs"], token);
    }
    public async Task<string> ExportAsync(DiagnosticReportRequest request, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PreviewToken)) throw new InvalidOperationException("DN-7008: Preview the diagnostic report before exporting it.");
        CachedReport preview;
        lock (_snapshotSync)
        {
            if (!_snapshots.Remove(request.PreviewToken, out preview!) || preview.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("DN-7008: The diagnostic preview is missing or expired. Preview the report again before exporting.");
        }
        var fullPath = Path.GetFullPath(path);
        var expected = request.Format == DiagnosticReportFormat.Json ? ".json" : ".md";
        if (!string.Equals(Path.GetExtension(fullPath), expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("DN-7007: Export path extension does not match report format.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (preview.Format != request.Format) throw new InvalidOperationException("DN-7008: The diagnostic preview format no longer matches the requested export.");
        await File.WriteAllTextAsync(fullPath, preview.Content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false); return fullPath;
    }
    private static string Markdown(object payload) => "# DistroNexus diagnostic report\n\n```json\n" + JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + "\n```\n";
    private sealed record CachedReport(DiagnosticReportFormat Format, string Content, DateTimeOffset ExpiresAt);
}
