using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Stateless monitoring bridge capability. It never starts a distribution and persists only protected opaque grants.</summary>
public sealed class MonitoringAutomationService
{
    private const int MaxProcesses = 20;
    private const int MaxPorts = 128;
    private readonly MonitoringService _monitoring;
    private readonly IProcessRunner _runner;
    private readonly MonitoringGrantStore _grants;

    public MonitoringAutomationService(MonitoringService monitoring, IProcessRunner runner, string root)
    {
        _monitoring = monitoring;
        _runner = runner;
        _grants = new MonitoringGrantStore(root);
    }

    public async Task<MonitoringSnapshotResult> GetSnapshotAsync(WslInstance instance, TimeSpan interval, CancellationToken ct = default)
    {
        await using var session = _monitoring.CreateSession(instance, interval);
        await session.StartAsync(ct).ConfigureAwait(false);
        MonitoringSample sample;
        if (!session.IsRunning)
            sample = session.Samples.LastOrDefault() ?? throw new InvalidOperationException("Monitor.InstanceStopped");
        else
        {
            await using var iterator = session.StreamAsync(ct).GetAsyncEnumerator(ct);
            if (!await iterator.MoveNextAsync().ConfigureAwait(false)) throw new InvalidOperationException("Monitor.ProbeUnavailable");
            sample = iterator.Current;
        }
        await session.StopAsync().ConfigureAwait(false);
        var publicSample = Sanitize(sample);
        var (token, expires) = await _grants.IssueSnapshotAsync(instance.Name, publicSample.Processes, ct).ConfigureAwait(false);
        return new MonitoringSnapshotResult(publicSample, token, expires);
    }

    public async Task<MonitoringProcessActionPreview> PreviewAsync(string snapshotToken, int processId, MonitoringProcessAction action, CancellationToken ct = default)
    {
        if (processId <= 1 || action is not (MonitoringProcessAction.Terminate or MonitoringProcessAction.Kill or MonitoringProcessAction.Renice)) throw new InvalidOperationException("Monitor.ProcessActionInvalid");
        var snapshot = await _grants.ResolveSnapshotAsync(snapshotToken, ct).ConfigureAwait(false);
        var process = snapshot.Processes.SingleOrDefault(p => p.Pid == processId) ?? throw new InvalidOperationException("Monitor.ProcessNotFound");
        if (action == MonitoringProcessAction.Kill && !await _grants.HasTermEligibilityAsync(snapshot.InstanceName, process, snapshotToken, ct).ConfigureAwait(false)) throw new InvalidOperationException("Monitor.KillRequiresTermAndReprobe");
        var (token, expires) = await _grants.IssuePreviewAsync(snapshot.InstanceName, process, action, snapshotToken, ct).ConfigureAwait(false);
        var message = action == MonitoringProcessAction.Kill
            ? $"Force kill PID {processId} only after TERM has timed out and re-probe confirms it remains running."
            : $"{action} PID {processId}";
        return new MonitoringProcessActionPreview(token, processId, action, message, process.RequiresAdditionalWarning || action == MonitoringProcessAction.Kill, expires);
    }

    public async Task<ProcessActionResult> ExecuteAsync(string previewToken, CancellationToken ct = default)
    {
        var preview = await _grants.ConsumePreviewAsync(previewToken, ct).ConfigureAwait(false);
        if (!await IsRunningAsync(preview.InstanceName, ct).ConfigureAwait(false)) return new(false, "Monitor.InstanceStopped");
        if (!await MatchesIdentityAsync(preview.InstanceName, preview.Process, ct).ConfigureAwait(false)) return new(false, "Monitor.ProcessIdentityChanged");
        var command = preview.Action switch
        {
            MonitoringProcessAction.Terminate => new[] { "kill", "-s", "TERM", preview.Process.Pid.ToString() },
            MonitoringProcessAction.Kill => new[] { "kill", "-s", "KILL", preview.Process.Pid.ToString() },
            MonitoringProcessAction.Renice => new[] { "renice", "5", "-p", preview.Process.Pid.ToString() },
            _ => null
        };
        if (command is null) return new(false, "Monitor.UnsupportedAction");
        var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", preview.InstanceName, "--exec", .. command], TimeSpan.FromSeconds(5), 4096, 4096), ct).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut || result.Cancelled || result.OutputTruncated || result.Failure != ProcessFailureKind.None) return new(false, "Monitor.ProcessSignalFailed");
        if (preview.Action == MonitoringProcessAction.Terminate)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            if (await MatchesIdentityAsync(preview.InstanceName, preview.Process, ct).ConfigureAwait(false))
            {
                await _grants.IssueTermEligibilityAsync(preview.InstanceName, preview.Process, ct).ConfigureAwait(false);
                return new(true, "Monitor.TermSentProcessStillRunning", "TERM was sent; preview and confirm KILL separately if appropriate.");
            }
            return new(true, "Monitor.ProcessTerminated");
        }
        if (preview.Action == MonitoringProcessAction.Kill) await _grants.RemoveTermEligibilityAsync(preview.InstanceName, preview.Process, ct).ConfigureAwait(false);
        return new(true, "Monitor.ProcessSignalSent");
    }

    private async Task<bool> IsRunningAsync(string name, CancellationToken ct)
    {
        var state = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--list", "--running", "--quiet"], TimeSpan.FromSeconds(5), 16 * 1024, 4096), ct).ConfigureAwait(false);
        return state.ExitCode == 0 && !state.TimedOut && !state.Cancelled && state.Failure == ProcessFailureKind.None && state.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(name, StringComparer.OrdinalIgnoreCase);
    }
    private async Task<bool> MatchesIdentityAsync(string name, MonitoredProcess process, CancellationToken ct)
    {
        var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", name, "--exec", "ps", "-o", "lstart=", "-p", process.Pid.ToString()], TimeSpan.FromSeconds(5), 4096, 4096), ct).ConfigureAwait(false);
        return result.ExitCode == 0 && MonitoringSession.ParseStart(result.StandardOutput) == process.StartTimeTicks;
    }
    private static MonitoringSample Sanitize(MonitoringSample sample) => sample with
    {
        Processes = sample.Processes.Take(MaxProcesses).Select(p => p with { Command = SafeCommand(p.Command), ListeningPorts = p.ListeningPorts.Take(16).ToArray() }).ToArray(),
        ListeningPorts = sample.ListeningPorts?.Take(MaxPorts).Select(p => p with { LocalAddress = p.LocalAddress.Length > 128 ? p.LocalAddress[..128] : p.LocalAddress }).ToArray(),
        CounterState = null
    };
    private static string SafeCommand(string value)
    {
        var sanitized = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return sanitized.Length <= 256 ? sanitized : sanitized[..256];
    }
}

internal sealed class MonitoringGrantStore
{
    private const int MaxRecords = 128;
    private readonly string _directory;
    public MonitoringGrantStore(string root) => _directory = Path.Combine(root, "monitoring-grants");
    public Task<(string, DateTimeOffset)> IssueSnapshotAsync(string instance, IReadOnlyList<MonitoredProcess> processes, CancellationToken ct) => IssueAsync(new Grant("snapshot", instance, processes, null, null, DateTimeOffset.UtcNow.AddMinutes(2)), ct);
    public Task<(string, DateTimeOffset)> IssuePreviewAsync(string instance, MonitoredProcess process, MonitoringProcessAction action, string snapshotToken, CancellationToken ct) => IssueAsync(new Grant("preview", instance, [process], action, Hash(snapshotToken), DateTimeOffset.UtcNow.AddMinutes(2)), ct);
    public Task IssueTermEligibilityAsync(string instance, MonitoredProcess process, CancellationToken ct) => IssueNamedAsync("term-" + instance + "-" + process.Pid + "-" + process.StartTimeTicks, new Grant("term", instance, [process], null, null, DateTimeOffset.UtcNow.AddMinutes(2)), ct);
    public Task RemoveTermEligibilityAsync(string instance, MonitoredProcess process, CancellationToken ct) => DeleteNamedAsync("term-" + instance + "-" + process.Pid + "-" + process.StartTimeTicks, ct);
    public async Task<bool> HasTermEligibilityAsync(string instance, MonitoredProcess process, string snapshotToken, CancellationToken ct)
    {
        try
        {
            var term = await ReadNamedAsync("term-" + instance + "-" + process.Pid + "-" + process.StartTimeTicks, ct);
            var snapshot = await ReadAsync(snapshotToken, false, ct);
            // A KILL preview must be based on a snapshot created after the TERM result. The expiry
            // is issued from the same clock and makes this check durable across bridge processes.
            return term.Kind == "term" && snapshot.Kind == "snapshot" && snapshot.ExpiresAt > term.ExpiresAt;
        }
        catch { return false; }
    }
    public async Task<(string InstanceName, IReadOnlyList<MonitoredProcess> Processes)> ResolveSnapshotAsync(string token, CancellationToken ct) { var g = await ReadAsync(token, false, ct); if (g.Kind != "snapshot") throw Invalid("Monitor.SnapshotGrantInvalid"); return (g.InstanceName, g.Processes); }
    public async Task<(string InstanceName, MonitoredProcess Process, MonitoringProcessAction Action)> ConsumePreviewAsync(string token, CancellationToken ct) { var g = await ReadAsync(token, true, ct); if (g.Kind != "preview" || g.Action is null || g.Processes.Count != 1) throw Invalid("Monitor.PreviewInvalid"); return (g.InstanceName, g.Processes[0], g.Action.Value); }
    private async Task<(string, DateTimeOffset)> IssueAsync(Grant grant, CancellationToken ct) { var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); await IssueNamedAsync(token, grant, ct); return (token, grant.ExpiresAt); }
    private async Task IssueNamedAsync(string name, Grant grant, CancellationToken ct) { await using var l = await LockAsync(ct); Sweep(); if (GrantFiles().Count() >= MaxRecords) throw Invalid("Monitor.GrantInvalid"); var path = PathFor(name); var data = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grant), null, DataProtectionScope.CurrentUser); await using var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true); await f.WriteAsync(data, ct); await f.FlushAsync(ct); }
    private async Task<Grant> ReadAsync(string token, bool consume, CancellationToken ct) { if (!ValidToken(token)) throw Invalid(consume ? "Monitor.PreviewInvalid" : "Monitor.SnapshotGrantInvalid"); return await ReadPathAsync(PathFor(token), consume, ct); }
    private async Task<Grant> ReadNamedAsync(string name, CancellationToken ct) => await ReadPathAsync(PathFor(name), false, ct);
    private async Task<Grant> ReadPathAsync(string path, bool consume, CancellationToken ct)
    {
        await using var l = await LockAsync(ct);
        var source = path;
        if (consume)
        {
            var used = path + ".used";
            if (!File.Exists(path))
            {
                if (!File.Exists(used)) throw Invalid("Monitor.GrantInvalid");
                if (IsExpired(used)) { TryDelete(used); throw Invalid("Monitor.GrantInvalid"); }
                throw Invalid("Monitor.PreviewReplayed");
            }
            File.Move(path, used);
            source = used;
        }
        else if (!File.Exists(path)) throw Invalid("Monitor.GrantInvalid");
        try { var g = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(await File.ReadAllBytesAsync(source, ct), null, DataProtectionScope.CurrentUser)) ?? throw Invalid("Monitor.GrantInvalid"); if (g.ExpiresAt <= DateTimeOffset.UtcNow) { TryDelete(source); throw Invalid("Monitor.GrantExpired"); } return g; } catch (CryptographicException) { TryDelete(source); throw Invalid("Monitor.GrantInvalid"); } catch (JsonException) { TryDelete(source); throw Invalid("Monitor.GrantInvalid"); }
    }
    private async Task DeleteNamedAsync(string name, CancellationToken ct) { await using var l = await LockAsync(ct); TryDelete(PathFor(name)); }
    private async Task<FileStream> LockAsync(CancellationToken ct) { Directory.CreateDirectory(_directory); var p = Path.Combine(_directory, ".lock"); for (var i = 0;; i++) try { return new FileStream(p, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch (IOException) when (i < 100) { await Task.Delay(20, ct); } }
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    private string PathFor(string key) => Path.Combine(_directory, Hash(key) + ".grant");
    private IEnumerable<string> GrantFiles() => Directory.EnumerateFiles(_directory, "*.grant*");
    private bool IsExpired(string path)
    {
        try { var grant = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)); return grant is null || grant.ExpiresAt <= DateTimeOffset.UtcNow; }
        catch { return true; }
    }
    private void Sweep() { foreach (var p in GrantFiles()) if (IsExpired(p)) TryDelete(p); }
    private static bool ValidToken(string token) => token.Length == 64 && token.All(Uri.IsHexDigit);
    private static void TryDelete(string p) { try { File.Delete(p); } catch (IOException) { } }
    private static InvalidOperationException Invalid(string code) => new(code);
    private sealed record Grant(string Kind, string InstanceName, IReadOnlyList<MonitoredProcess> Processes, MonitoringProcessAction? Action, string? SnapshotHash, DateTimeOffset ExpiresAt);
}
