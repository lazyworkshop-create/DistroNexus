using System.Text.Json;
using System.Text.Json.Nodes;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Runs independent checks once per scan, isolating individual failures.</summary>
public sealed class HealthOrchestrator : IHealthOrchestrator
{
    private readonly IReadOnlyList<IHealthCheck> _checks;
    private readonly IPlatformCapabilityService _capabilities;
    private readonly IWslManagerService _manager;
    private readonly string _historyPath;
    private readonly VersionedJsonStore<List<HealthHistoryEntry>> _history;
    private readonly SemaphoreSlim _singleFlight = new(1, 1);

    public HealthOrchestrator(IEnumerable<IHealthCheck> checks, IPlatformCapabilityService capabilities, IWslManagerService manager, string? historyPath = null)
    {
        _checks = checks.OrderBy(c => c.Descriptor.Id, StringComparer.Ordinal).ToArray();
        _capabilities = capabilities; _manager = manager;
        _historyPath = historyPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus", "health-history.json");
        // Health history used to be an unversioned JSON array.  Keep reading that format so an
        // upgrade never discards a user's recent diagnostic evidence, but write every new value
        // through the same atomic/revisioned store used by Settings and Backup.
        _history = new VersionedJsonStore<List<HealthHistoryEntry>>(_historyPath, schemaVersion: 1,
            // The pre-v2.3.0 array was emitted with camel-case JSON names.  Read it with the
            // same case-insensitive contract as VersionedJsonStore so migration preserves its
            // timestamps and counts rather than silently filtering default values as expired.
            legacyReader: node => JsonSerializer.Deserialize<List<HealthHistoryEntry>>(node.ToJsonString(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? []);
    }

    public async Task<HealthScanResult> ScanAsync(IProgress<HealthFinding>? progress = null, CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try { await _singleFlight.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return new HealthScanResult(Guid.NewGuid(), started, DateTimeOffset.UtcNow, [], true); }
        try
        {
            PlatformCapabilitySnapshot host; IReadOnlyList<WslInstance> instances;
            try { host = await _capabilities.GetHostSnapshotAsync(cancellationToken: cancellationToken).ConfigureAwait(false); instances = await _manager.GetInstancesAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return new HealthScanResult(Guid.NewGuid(), started, DateTimeOffset.UtcNow, [], true); }
            var context = new HealthCheckContext(host, instances);
            var findings = new List<HealthFinding>();
            using var gate = new SemaphoreSlim(4, 4);
            var tasks = _checks.Select(async check =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // A check must never infer that a missing or stale platform capability is
                    // usable.  In particular this avoids invoking WSL helpers on hosts where
                    // WSL was not detected.  Surface the reason as a deterministic finding
                    // instead of silently skipping the check.
                    var unavailable = check.Descriptor.Prerequisites
                        .Select(id => (Id: id, Result: Capability(host, id)))
                        .Where(x => x.Result is null || !x.Result.IsSupported)
                        .ToArray();
                    if (unavailable.Length != 0)
                    {
                        var detail = string.Join("; ", unavailable.Select(x => x.Id + ": " + (x.Result?.ReasonCode ?? "not probed")));
                        var finding = new HealthFinding($"{check.Descriptor.Id}.prerequisite", HealthSeverity.Information,
                            check.Descriptor.Scope, "Health check unavailable", detail,
                            Evidence: unavailable.ToDictionary(x => x.Id.ToString(), x => x.Result?.Status.ToString() ?? "Unavailable"));
                        lock (findings) { findings.Add(finding); progress?.Report(finding); }
                        return;
                    }
                    var result = await check.CheckAsync(context, cancellationToken).ConfigureAwait(false);
                    lock (findings) foreach (var finding in result.Findings) { findings.Add(finding); progress?.Report(finding); }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    var finding = new HealthFinding($"{check.Descriptor.Id}.failed", HealthSeverity.Information, check.Descriptor.Scope,
                        "Health check unavailable", SensitiveDataRedactor.Redact(ex.Message), Evidence: new Dictionary<string, string> { ["check"] = check.Descriptor.Id });
                    lock (findings) { findings.Add(finding); progress?.Report(finding); }
                }
                finally { gate.Release(); }
            });
            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch (OperationCanceledException) { return new HealthScanResult(Guid.NewGuid(), started, DateTimeOffset.UtcNow, Order(findings), true); }
            var scan = new HealthScanResult(Guid.NewGuid(), started, DateTimeOffset.UtcNow, Order(findings));
            await AppendHistoryAsync(scan, cancellationToken).ConfigureAwait(false);
            return scan;
        }
        finally { _singleFlight.Release(); }
    }

    private static CapabilityResult? Capability(PlatformCapabilitySnapshot snapshot, CapabilityId id) =>
        snapshot.Capabilities.TryGetValue(id, out var capability) ? capability :
        snapshot.OptionalDependencies.TryGetValue(id, out capability) ? capability : null;

    public async Task<IReadOnlyList<HealthHistoryEntry>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        try { await _singleFlight.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return []; }
        try { return await ReadHistoryAsync(cancellationToken).ConfigureAwait(false); }
        finally { _singleFlight.Release(); }
    }
    private async Task<IReadOnlyList<HealthHistoryEntry>> ReadHistoryAsync(CancellationToken cancellationToken)
    {
        var document = await _history.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (document.Error == StoreErrorKind.NotFound) return [];
        if (!document.Succeeded) return [];
        return document.Value!.Value.Where(x => x.CompletedAt >= DateTimeOffset.UtcNow.AddDays(-7)).OrderByDescending(x => x.CompletedAt).ToArray();
    }

    private async Task AppendHistoryAsync(HealthScanResult scan, CancellationToken cancellationToken)
    {
        var entry = new HealthHistoryEntry(scan.CompletedAt, scan.Findings.Count(x => x.Severity == HealthSeverity.Healthy), scan.Findings.Count(x => x.Severity == HealthSeverity.Information), scan.Findings.Count(x => x.Severity == HealthSeverity.Warning), scan.Findings.Count(x => x.Severity == HealthSeverity.Critical));
        // Another HealthOrchestrator may share this history path (for example a second app
        // window). Optimistic revisions ensure no successful scan is silently overwritten.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var current = await _history.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (current.Error is not (StoreErrorKind.None or StoreErrorKind.NotFound)) return;
            var values = current.Value?.Value ?? [];
            var revision = current.Value?.Revision ?? 0;
            var retained = values.Append(entry).Where(x => x.CompletedAt >= DateTimeOffset.UtcNow.AddDays(-7)).OrderByDescending(x => x.CompletedAt).ToList();
            var write = await _history.WriteAsync(retained, revision, cancellationToken).ConfigureAwait(false);
            if (write.Succeeded) return;
            if (write.Error != StoreErrorKind.RevisionConflict) return;
        }
    }
    private static IReadOnlyList<HealthFinding> Order(IEnumerable<HealthFinding> source) => source.OrderByDescending(x => x.Severity).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
}
