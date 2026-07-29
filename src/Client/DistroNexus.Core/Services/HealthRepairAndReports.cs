using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Principal;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class HealthRepairService : IHealthRepairService
{
    private readonly IReadOnlyDictionary<string, IRepairAction> _actions;
    private readonly IRecoveryOfferService? _recoveryOffers;
    private readonly Dictionary<string, (HealthFinding Finding, RepairPreview Preview)> _previews = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly HealthRepairGrantStore? _durableGrants;
    private readonly IHealthOrchestrator? _health;
    public HealthRepairService(IEnumerable<IRepairAction> actions, IRecoveryOfferService? recoveryOffers = null, string? durableGrantRoot = null, IHealthOrchestrator? health = null) { _actions = actions.ToDictionary(x => x.Id, StringComparer.Ordinal); _recoveryOffers = recoveryOffers; _health = health; if (durableGrantRoot is not null) _durableGrants = new HealthRepairGrantStore(durableGrantRoot); }

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
        if (_durableGrants is null) lock (_sync) _previews[token] = (finding, preview);
        else await _durableGrants.IssueAsync(token, finding, preview, cancellationToken).ConfigureAwait(false);
        return preview;
    }
    public async Task<RepairResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken)) return new RepairResult("unknown", false, [], Error: "DN-7002: Repair preview is missing, expired, or invalid.");
        if (_durableGrants is null) return new RepairResult("unknown", false, [], Error: "DN-7002: Repair preview must be executed by the issuing process.");
        try
        {
            var saved = await _durableGrants.ConsumeAsync(previewToken, cancellationToken).ConfigureAwait(false);
            if (!_actions.ContainsKey(saved.Finding.RepairId ?? string.Empty)) return new RepairResult("unknown", false, [], Error: "DN-7002: Repair preview is no longer eligible.");
            if (_health is not null && _actions[saved.Finding.RepairId!] is not DesktopOnlyRepairAction)
            {
                var current = await _health.ScanAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!current.Findings.Any(x => x.Id == saved.Finding.Id && x.RepairId == saved.Finding.RepairId && CanonicalFinding(x) == saved.FindingCanonical))
                    return new RepairResult(saved.Preview.RepairId, false, [], Error: "DN-7002: Repair finding changed; scan and preview again.");
            }
            var result = await _actions[saved.Finding.RepairId!].ExecuteAsync(saved.Finding, cancellationToken).ConfigureAwait(false);
            return result with { Results = result.Results.Concat(["Postcondition: repair action completed; rescan to verify the finding is resolved."]).ToArray(), Idempotency = saved.Preview.Idempotency, NextSteps = result.NextSteps ?? ["Rescan Health Center to verify the finding is resolved."] };
        }
        catch (InvalidOperationException ex) { return new RepairResult("unknown", false, [], Error: "DN-7002: " + SensitiveDataRedactor.Redact(ex.Message)); }
        catch (Exception ex) { return new RepairResult("unknown", false, [], Error: "DN-7005: " + SensitiveDataRedactor.Redact(ex.Message)); }
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
    private static string CanonicalFinding(HealthFinding finding) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(finding))));
}

/// <summary>DPAPI-protected same-SID single-use repair grants, persisted for independent module processes.</summary>
internal sealed class HealthRepairGrantStore
{
    private const int MaxRecords = 64;
    private readonly string _directory;
    private readonly Func<string> _sid;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;
    private readonly TimeProvider _clock;
    private static string Sid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Current user identity is unavailable.");
    public HealthRepairGrantStore(string root, Func<string>? sid = null, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null, TimeProvider? clock = null) { _directory = Path.Combine(root, "health-repair-grants"); _sid = sid ?? Sid; _protect = protect ?? (x => ProtectedData.Protect(x, null, DataProtectionScope.CurrentUser)); _unprotect = unprotect ?? (x => ProtectedData.Unprotect(x, null, DataProtectionScope.CurrentUser)); _clock = clock ?? TimeProvider.System; }
    public async Task IssueAsync(string token, HealthFinding finding, RepairPreview preview, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); Sweep();
        if (Directory.EnumerateFiles(_directory, "*.grant").Count() >= MaxRecords) throw new InvalidOperationException("Repair preview store is full.");
        var grant = new Grant(_sid(), Canonical(finding), finding, preview, _clock.GetUtcNow().AddMinutes(10));
        await File.WriteAllBytesAsync(PathFor(token), _protect(JsonSerializer.SerializeToUtf8Bytes(grant)), ct).ConfigureAwait(false);
    }
    public async Task<Grant> ConsumeAsync(string token, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); var path = PathFor(token); Sweep(path);
        if (!File.Exists(path)) throw new InvalidOperationException("Repair preview is missing or replayed.");
        try { var grant = JsonSerializer.Deserialize<Grant>(_unprotect(await File.ReadAllBytesAsync(path, ct))) ?? throw new InvalidOperationException("Repair preview is invalid."); File.Delete(path); if (grant.ExpiresAt <= _clock.GetUtcNow() || grant.Sid != _sid() || grant.FindingCanonical != Canonical(grant.Finding)) throw new InvalidOperationException("Repair preview is expired or invalid."); return grant; }
        catch (CryptographicException) { TryDelete(path); throw new InvalidOperationException("Repair preview is invalid."); }
        catch (JsonException) { TryDelete(path); throw new InvalidOperationException("Repair preview is invalid."); }
    }
    private async Task<FileStream> LockAsync(CancellationToken ct) { Directory.CreateDirectory(_directory); var p=Path.Combine(_directory,".lock"); for(var i=0;;i++) try{return new FileStream(p,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);} catch(IOException) when(i<100){await Task.Delay(20,ct);} }
    private string PathFor(string token) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private void Sweep(string? except = null) { foreach(var p in Directory.EnumerateFiles(_directory,"*.grant")) { if (string.Equals(p,except,StringComparison.OrdinalIgnoreCase)) continue; try { var g=JsonSerializer.Deserialize<Grant>(_unprotect(File.ReadAllBytes(p))); if(g is null || g.ExpiresAt<=_clock.GetUtcNow()) TryDelete(p); } catch { TryDelete(p); } } }
    private static string Canonical(HealthFinding finding) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(finding))));
    private static void TryDelete(string p) { try { File.Delete(p); } catch(IOException) { } }
    internal sealed record Grant(string Sid, string FindingCanonical, HealthFinding Finding, RepairPreview Preview, DateTimeOffset ExpiresAt);
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
    private readonly string _exportDirectory;
    private readonly DiagnosticSnapshotGrantStore _snapshots;
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMinutes(10);
    public DiagnosticReportService(IHealthOrchestrator health, IPlatformCapabilityService capabilities, IDiagnosticLogProvider logs, IStructuredErrorProvider errors, string? exportDirectory = null)
    {
        (_health, _capabilities, _logs, _errors) = (health, capabilities, logs, errors);
        _exportDirectory = Path.GetFullPath(exportDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus", "Diagnostics"));
        _snapshots = new DiagnosticSnapshotGrantStore(Path.Combine(Directory.GetParent(_exportDirectory)?.FullName ?? _exportDirectory, "diagnostic-snapshot-grants"));
    }
    public async Task<DiagnosticReportPreview> PreviewAsync(DiagnosticReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Redact) throw new InvalidOperationException("DN-7007: Diagnostic reports must use redaction.");
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
        // The public export contract is redacted; secrets, paths, and user identifiers never leave Core.
        content = SensitiveDataRedactor.Redact(content);
        var token = Guid.NewGuid().ToString("N");
        await _snapshots.IssueAsync(token, request.Format, selected, content, DateTimeOffset.UtcNow.Add(SnapshotLifetime), cancellationToken).ConfigureAwait(false);
        return new DiagnosticReportPreview(request.Format, content, ["versions", "capabilities", "findings", "recent structured errors", "selected logs"], token,
            new DiagnosticReportSelectionMetadata(true, selected));
    }
    public async Task<string> ExportAsync(DiagnosticReportRequest request, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PreviewToken)) throw new InvalidOperationException("DN-7008: Preview the diagnostic report before exporting it.");
        if (!request.Redact) throw new InvalidOperationException("DN-7007: Diagnostic reports must use redaction.");
        await ExportSnapshotAsync(request.PreviewToken, path, request.Format, cancellationToken).ConfigureAwait(false);
        return Path.GetFileName(path);
    }
    /// <summary>Exports a cached preview without trusting a caller-supplied format or selection.</summary>
    public async Task<DiagnosticReportExportResult> ExportAsync(DiagnosticReportExportRequest request, CancellationToken cancellationToken = default)
    {
        await ExportSnapshotAsync(request.PreviewToken, request.DestinationFileName, expectedFormat: null, cancellationToken).ConfigureAwait(false);
        return new DiagnosticReportExportResult(request.DestinationFileName);
    }
    private async Task<string> ExportSnapshotAsync(string previewToken, string path, DiagnosticReportFormat? expectedFormat, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previewToken)) throw new InvalidOperationException("DN-7008: Preview the diagnostic report before exporting it.");
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(path, Path.GetFileName(path), StringComparison.Ordinal))
            throw new InvalidOperationException("DN-7007: Diagnostic destination must be a file name in the DistroNexus diagnostic export directory.");
        var format = await _snapshots.PeekFormatAsync(previewToken, cancellationToken).ConfigureAwait(false);
        var expected = format == DiagnosticReportFormat.Json ? ".json" : ".md";
        if (!string.Equals(Path.GetExtension(path), expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("DN-7007: Export path extension does not match report format.");
        if (expectedFormat is not null && format != expectedFormat) throw new InvalidOperationException("DN-7008: The diagnostic preview format no longer matches the requested export.");
        var preview = await _snapshots.ConsumeAsync(previewToken, cancellationToken).ConfigureAwait(false);
        var fullPath = Path.Combine(_exportDirectory, path);
        Directory.CreateDirectory(_exportDirectory);
        await File.WriteAllTextAsync(fullPath, preview.Content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false); return fullPath;
    }
    private static string Markdown(object payload) => "# DistroNexus diagnostic report\n\n```json\n" + JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + "\n```\n";
}

/// <summary>Persists only already-redacted diagnostic snapshots, protected for the issuing Windows user.</summary>
internal sealed class DiagnosticSnapshotGrantStore
{
    private const int MaxRecords = 64;
    private const long MaxBytes = 4 * 1024 * 1024;
    private readonly string _directory;
    private readonly Func<string> _sid;
    private readonly Func<byte[], byte[]> _protect;
    private readonly Func<byte[], byte[]> _unprotect;
    private readonly TimeProvider _clock;
    private static string Sid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Current user identity is unavailable.");
    public DiagnosticSnapshotGrantStore(string root, Func<string>? sid = null, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null, TimeProvider? clock = null) { _directory = root; _sid = sid ?? Sid; _protect = protect ?? (x => ProtectedData.Protect(x, null, DataProtectionScope.CurrentUser)); _unprotect = unprotect ?? (x => ProtectedData.Unprotect(x, null, DataProtectionScope.CurrentUser)); _clock = clock ?? TimeProvider.System; }
    public async Task IssueAsync(string token, DiagnosticReportFormat format, IReadOnlyList<string> selection, string content, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); Sweep();
        if (Directory.EnumerateFiles(_directory, "*.grant").Count() >= MaxRecords || Directory.EnumerateFiles(_directory, "*.grant").Sum(x => new FileInfo(x).Length) >= MaxBytes) throw new InvalidOperationException("DN-7007: Diagnostic snapshot store is full.");
        var grant = new Grant(_sid(), format, selection.ToArray(), content, expiresAt);
        await using var file = new FileStream(PathFor(token), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        var bytes = _protect(JsonSerializer.SerializeToUtf8Bytes(grant));
        await file.WriteAsync(bytes, ct).ConfigureAwait(false); await file.FlushAsync(ct).ConfigureAwait(false);
    }
    public async Task<Grant> ConsumeAsync(string token, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); var path = PathFor(token); Sweep(path);
        if (!File.Exists(path)) throw new InvalidOperationException("DN-7008: The diagnostic preview is missing, expired, or already used.");
        try
        {
            var grant = JsonSerializer.Deserialize<Grant>(_unprotect(await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false))) ?? throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid.");
            File.Delete(path);
            if (grant.ExpiresAt <= _clock.GetUtcNow() || grant.Sid != _sid() || grant.Selection.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid or expired.");
            return grant;
        }
        catch (CryptographicException) { TryDelete(path); throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid."); }
        catch (JsonException) { TryDelete(path); throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid."); }
    }
    public async Task<DiagnosticReportFormat> PeekFormatAsync(string token, CancellationToken ct)
    {
        await using var gate = await LockAsync(ct); var path = PathFor(token); Sweep(path);
        if (!File.Exists(path)) throw new InvalidOperationException("DN-7008: The diagnostic preview is missing, expired, or already used.");
        try { var grant = JsonSerializer.Deserialize<Grant>(_unprotect(await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false))) ?? throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid."); if (grant.ExpiresAt <= _clock.GetUtcNow() || grant.Sid != _sid()) throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid or expired."); return grant.Format; }
        catch (CryptographicException) { TryDelete(path); throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid."); }
        catch (JsonException) { TryDelete(path); throw new InvalidOperationException("DN-7008: The diagnostic preview is invalid."); }
    }
    private async Task<FileStream> LockAsync(CancellationToken ct) { Directory.CreateDirectory(_directory); var p = Path.Combine(_directory, ".lock"); for (var i = 0; ; i++) try { return new FileStream(p, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch (IOException) when (i < 100) { await Task.Delay(20, ct).ConfigureAwait(false); } }
    private string PathFor(string token) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private void Sweep(string? except = null) { foreach (var p in Directory.EnumerateFiles(_directory, "*.grant")) { if (string.Equals(p, except, StringComparison.OrdinalIgnoreCase)) continue; try { var g = JsonSerializer.Deserialize<Grant>(_unprotect(File.ReadAllBytes(p))); if (g is null || g.ExpiresAt <= _clock.GetUtcNow()) TryDelete(p); } catch { TryDelete(p); } } }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } }
    internal sealed record Grant(string Sid, DiagnosticReportFormat Format, string[] Selection, string Content, DateTimeOffset ExpiresAt);
}
