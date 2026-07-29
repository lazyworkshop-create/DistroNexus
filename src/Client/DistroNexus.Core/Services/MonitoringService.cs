using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class MonitoringService(IProcessRunner runner, IWslConfigurationService configuration, IMonitoringWarningSink? warningSink = null) : IMonitoringService
{
    public MonitoringService(IProcessRunner runner) : this(runner, new EmptyConfigurationService()) { }
    public IMonitoringSession CreateSession(WslInstance instance, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (!instance.IsRunning) return new UnavailableMonitoringSession("Monitor.InstanceStopped", instance.Size);
        if (interval is not ({ TotalSeconds: 1 } or { TotalSeconds: 2 } or { TotalSeconds: 5 } or { TotalSeconds: 10 }))
            throw new ArgumentOutOfRangeException(nameof(interval), "Monitoring intervals must be 1, 2, 5, or 10 seconds.");
        return new MonitoringSession(instance, interval, runner, configuration, warningSink);
    }
}

internal sealed class EmptyConfigurationService : IWslConfigurationService
{
    public Task<ConfigurationDocument<WslConfigurationSettings>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string>()), LosslessIniDocument.Parse([]), [], 0, "", RestartScope.None, ""));
    public Task<ConfigurationPreview> PreviewAsync(IReadOnlyDictionary<string, string?> values, string expectedFingerprint, IReadOnlySet<string> availableCapabilities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ConfigurationSaveResult> SaveAsync(IReadOnlyDictionary<string, string?> values, string expectedFingerprint, IReadOnlySet<string>? availableCapabilities = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class UnavailableMonitoringSession : IMonitoringSession
{
    private readonly MonitoringSample[] _samples;
    public UnavailableMonitoringSession(string reason, long vhdxPhysicalBytes)
    {
        UnavailableReason = reason;
        _samples = [new MonitoringSample(DateTimeOffset.UtcNow, null, null, null, null, null, null, null, null, null, null, null,
            vhdxPhysicalBytes > 0 ? vhdxPhysicalBytes : null, null, [], new Dictionary<string, string> { ["runtime"] = reason })];
    }
    public IReadOnlyList<MonitoringSample> Samples => _samples;
    public bool IsRunning => false;
    public string? UnavailableReason { get; }
    public event EventHandler<MonitoringSample>? SampleAvailable { add { } remove { } }
    public async IAsyncEnumerable<MonitoringSample> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var sample in _samples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return sample;
        }

        await Task.CompletedTask;
    }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public Task SetThresholdsAsync(MonitoringThresholds thresholds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task<ProcessActionPreview> PreviewProcessActionAsync(MonitoredProcess process, MonitoringProcessAction action, CancellationToken cancellationToken = default) => throw new InvalidOperationException(UnavailableReason);
    public Task<ProcessActionResult> ExecuteProcessActionAsync(ProcessActionPreview preview, CancellationToken cancellationToken = default) => Task.FromResult(new ProcessActionResult(false, UnavailableReason ?? "Monitor.Unavailable"));
}

internal sealed class MonitoringSession : IMonitoringSession
{
    private const int Capacity = 300;
    // Fixed, allow-listed probe: it contains no user-derived command or argument.
    private const string Probe = "LC_ALL=C; cat /proc/stat; echo __DN_MEM__; cat /proc/meminfo; echo __DN_DISK__; cat /proc/diskstats; echo __DN_NET__; cat /proc/net/dev; echo __DN_FS__; df -Pk /; echo __DN_PORTS__; if command -v ss >/dev/null 2>&1; then ss -H -lntup 2>/dev/null || echo __DN_PORTS_UNAVAILABLE__; else echo __DN_PORTS_UNAVAILABLE__; fi; echo __DN_PROC__; ps -eo pid,lstart,user,pcpu,pmem,args --sort=-pcpu | head -n 21";
    private readonly WslInstance _instance; private readonly TimeSpan _interval; private readonly IProcessRunner _runner; private readonly IWslConfigurationService _configuration; private readonly IMonitoringWarningSink? _warningSink;
    private readonly List<MonitoringSample> _samples = []; private readonly object _sync = new(); private readonly ConcurrentDictionary<Guid, ProcessActionPreview> _previews = [];
    private readonly ConcurrentDictionary<(int Pid, long Start), byte> _killEligible = [];
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Channel<MonitoringSample> _stream = Channel.CreateBounded<MonitoringSample>(new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = true, SingleReader = false });
    private CancellationTokenSource? _stop; private Task? _loop; private int _probing; private MonitoringThresholds _thresholds = MonitoringThresholds.Default; private HostResourceLimits? _limits;
    private readonly Func<CancellationToken, ValueTask<bool>>? _waitForNextTickAsync;
    public MonitoringSession(WslInstance instance, TimeSpan interval, IProcessRunner runner, IWslConfigurationService configuration, IMonitoringWarningSink? warningSink, Func<CancellationToken, ValueTask<bool>>? waitForNextTickAsync = null)
    {
        (_instance, _interval, _runner, _configuration, _warningSink, _waitForNextTickAsync) = (instance, interval, runner, configuration, warningSink, waitForNextTickAsync);
    }
    public IReadOnlyList<MonitoringSample> Samples { get { lock (_sync) return _samples.ToArray(); } }
    public bool IsRunning => Volatile.Read(ref _loop) is { IsCompleted: false };
    public string? UnavailableReason { get; private set; }
    public event EventHandler<MonitoringSample>? SampleAvailable;
    public Task SetThresholdsAsync(MonitoringThresholds thresholds, CancellationToken cancellationToken = default)
    {
        if (!thresholds.IsValid) throw new ArgumentOutOfRangeException(nameof(thresholds));
        _thresholds = thresholds; return Task.CompletedTask;
    }
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loop is { IsCompleted: false }) return;
            _stop?.Dispose();
            _stop = null;
            _loop = null;
            if (!_instance.IsRunning) { UnavailableReason = "Monitor.InstanceStopped"; return; }
            var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _stop = stop;
            _loop = RunAsync(stop.Token);
        }
        finally { _lifecycle.Release(); }
    }
    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            var stop = _stop;
            var loop = _loop;
            if (stop is null) { _warningSink?.Update(_instance.Name, []); return; }
            stop.Cancel();
            try { if (loop is not null) await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            finally
            {
                if (ReferenceEquals(_stop, stop)) { _stop = null; _loop = null; }
                stop.Dispose();
                _warningSink?.Update(_instance.Name, []);
            }
        }
        finally { _lifecycle.Release(); }
    }
    public async IAsyncEnumerable<MonitoringSample> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var sample in _stream.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return sample;
    }
    private async Task RunAsync(CancellationToken ct)
    {
        // Reading .wslconfig is host-only and does not start the distribution.
        try { _limits = ToLimits((await _configuration.ReadAsync(ct).ConfigureAwait(false)).Settings.Values); } catch { _limits = null; }
        using var timer = new PeriodicTimer(_interval);
        do
        {
            if (Interlocked.CompareExchange(ref _probing, 1, 0) == 0)
            {
                try { if (!await ProbeOnceAsync(ct).ConfigureAwait(false)) return; }
                finally { Volatile.Write(ref _probing, 0); }
            }
        } while (await WaitForNextTickAsync(timer, ct).ConfigureAwait(false));
    }
    private ValueTask<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct) => _waitForNextTickAsync?.Invoke(ct) ?? timer.WaitForNextTickAsync(ct);
    private async Task<bool> ProbeOnceAsync(CancellationToken ct)
    {
        // A session is tied to one activation.  It must not revive itself if the instance later
        // starts again; selecting the tab explicitly creates the next session.
        if (!await ConfirmRunningAsync(ct).ConfigureAwait(false)) { PublishUnavailable(); return false; }
        var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", _instance.Name, "--exec", "sh", "-lc", Probe], TimeSpan.FromSeconds(Math.Max(5, _interval.TotalSeconds * 2)), 128 * 1024, 32 * 1024), ct).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut || result.Cancelled) { UnavailableReason = "Monitor.ProbeUnavailable"; _warningSink?.Update(_instance.Name, []); return true; }
        var sample = MonitoringParser.Parse(result.StandardOutput, DateTimeOffset.UtcNow, Samples.LastOrDefault(), _limits, _thresholds, _instance.Size);
        lock (_sync) { if (_samples.Count == Capacity) _samples.RemoveAt(0); _samples.Add(sample); }
        _warningSink?.Update(_instance.Name, sample.Warnings ?? []);
        UnavailableReason = sample.UnavailableMetrics.Count != 0 ? "Monitor.PartiallyUnavailable" : null;
        Publish(sample);
        return true;
    }
    public async Task<ProcessActionPreview> PreviewProcessActionAsync(MonitoredProcess process, MonitoringProcessAction action, CancellationToken cancellationToken = default)
    {
        if (!await ConfirmRunningAsync(cancellationToken).ConfigureAwait(false)) throw new InvalidOperationException(UnavailableReason ?? "Monitor.InstanceStopped");
        if (process.Pid <= 1) throw new InvalidOperationException("Monitor.ProtectedProcess");
        if (action == MonitoringProcessAction.Kill && !_killEligible.ContainsKey((process.Pid, process.StartTimeTicks))) throw new InvalidOperationException("Monitor.KillRequiresTermAndReprobe");
        var message = action == MonitoringProcessAction.Kill ? $"Force kill PID {process.Pid} only after TERM has timed out and re-probe confirms it remains running." : $"{action} PID {process.Pid} ({process.Command})";
        var p = new ProcessActionPreview(Guid.NewGuid(), _instance.Name, process, action, action == MonitoringProcessAction.Renice ? 5 : null, message, process.RequiresAdditionalWarning || action == MonitoringProcessAction.Kill);
        _previews[p.Token] = p; return p;
    }
    public async Task<ProcessActionResult> ExecuteProcessActionAsync(ProcessActionPreview preview, CancellationToken ct = default)
    {
        if (!_previews.TryRemove(preview.Token, out var recorded) || recorded != preview || recorded.DistributionName != _instance.Name) return new(false, "Monitor.PreviewExpired");
        var command = CommandFor(recorded); if (command is null) return new(false, "Monitor.UnsupportedAction");
        // Never let a process action implicitly start a distribution.  The state check and the
        // exact PID/start-time identity check must remain adjacent to the eventual signal.
        if (!await ConfirmRunningAsync(ct).ConfigureAwait(false)) return new(false, UnavailableReason ?? "Monitor.InstanceStopped");
        if (!await MatchesIdentityAsync(recorded.Process, ct).ConfigureAwait(false))
        {
            _killEligible.TryRemove((recorded.Process.Pid, recorded.Process.StartTimeTicks), out _);
            return new(false, UnavailableReason is not null ? UnavailableReason : "Monitor.ProcessIdentityChanged");
        }
        // The identity probe is deliberately the final WSL operation before sending TERM/KILL/renice.
        var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", _instance.Name, "--exec", .. command], TimeSpan.FromSeconds(5), 4096, 4096), ct).ConfigureAwait(false);
        // A launcher result is trustworthy only when it completed normally.  In particular, an
        // exit code of zero after cancellation, a timeout, truncated output, or a start failure
        // cannot prove that TERM/KILL/renice was delivered, so it must never unlock guidance.
        if (!IsHealthySignalResult(result)) return new(false, "Monitor.ProcessSignalFailed", result.StandardError);
        if (recorded.Action == MonitoringProcessAction.Kill) { _killEligible.TryRemove((recorded.Process.Pid, recorded.Process.StartTimeTicks), out _); return new(true, "Monitor.ProcessSignalSent"); }
        if (recorded.Action != MonitoringProcessAction.Terminate) return new(true, "Monitor.ProcessSignalSent");
        // TERM is never silently escalated. Re-probe after the bounded wait and make KILL a new preview/confirmation.
        await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
        if (await MatchesIdentityAsync(recorded.Process, ct).ConfigureAwait(false))
        {
            _killEligible[(recorded.Process.Pid, recorded.Process.StartTimeTicks)] = 0;
            return new(true, "Monitor.TermSentProcessStillRunning", "TERM was sent; the process is still running. Preview and confirm KILL separately if appropriate.");
        }
        _killEligible.TryRemove((recorded.Process.Pid, recorded.Process.StartTimeTicks), out _);
        return
            new(true, "Monitor.ProcessTerminated");
    }
    private async Task<bool> MatchesIdentityAsync(MonitoredProcess process, CancellationToken ct)
    {
        var identity = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", _instance.Name, "--exec", "ps", "-o", "lstart=", "-p", process.Pid.ToString(CultureInfo.InvariantCulture)], TimeSpan.FromSeconds(5), 4096, 4096), ct).ConfigureAwait(false);
        return identity.ExitCode == 0 && ParseStart(identity.StandardOutput) == process.StartTimeTicks;
    }
    private static bool IsHealthySignalResult(ProcessResult result) =>
        result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && !result.OutputTruncated && result.Failure == ProcessFailureKind.None;
    private async Task<bool> ConfirmRunningAsync(CancellationToken ct)
    {
        // The model can be stale when WSL has stopped externally.  This read-only command never
        // starts a distribution, and it is deliberately issued immediately before every --exec boundary.
        if (!_instance.IsRunning) { UnavailableReason = "Monitor.InstanceStopped"; return false; }
        var state = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--list", "--running", "--quiet"], TimeSpan.FromSeconds(5), 16 * 1024, 4 * 1024), ct).ConfigureAwait(false);
        if (state.ExitCode != 0 || state.TimedOut || state.Cancelled || state.Failure != ProcessFailureKind.None)
        {
            UnavailableReason = "Monitor.RuntimeStateUnavailable";
            return false;
        }
        if (!state.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(_instance.Name, StringComparer.OrdinalIgnoreCase))
        {
            UnavailableReason = "Monitor.InstanceStopped";
            return false;
        }
        return true;
    }
    private void PublishUnavailable()
    {
        _warningSink?.Update(_instance.Name, []);
        var sample = new MonitoringSample(DateTimeOffset.UtcNow, null, null, null, null, null, null, null, null, null, null, null,
            _instance.Size > 0 ? _instance.Size : null, null, [], new Dictionary<string, string> { ["runtime"] = UnavailableReason ?? "Monitor.InstanceStopped" });
        lock (_sync) { if (_samples.Count == Capacity) _samples.RemoveAt(0); _samples.Add(sample); }
        Publish(sample);
    }
    private void Publish(MonitoringSample sample)
    {
        _stream.Writer.TryWrite(sample);
        SampleAvailable?.Invoke(this, sample);
    }
    private static IReadOnlyList<string>? CommandFor(ProcessActionPreview p) => p.Action switch
    {
        MonitoringProcessAction.Terminate => ["kill", "-s", "TERM", p.Process.Pid.ToString(CultureInfo.InvariantCulture)],
        MonitoringProcessAction.Kill => ["kill", "-s", "KILL", p.Process.Pid.ToString(CultureInfo.InvariantCulture)],
        MonitoringProcessAction.Renice when p.NiceValue is >= -20 and <= 19 => ["renice", p.NiceValue.Value.ToString(CultureInfo.InvariantCulture), "-p", p.Process.Pid.ToString(CultureInfo.InvariantCulture)], _ => null
    };
    private static HostResourceLimits ToLimits(IReadOnlyDictionary<string, string> values) => new(ParseSize(values.GetValueOrDefault("wsl2.memory")), ParseSize(values.GetValueOrDefault("wsl2.swap")), int.TryParse(values.GetValueOrDefault("wsl2.processors"), out var p) && p > 0 ? p : null);
    private static long? ParseSize(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var m = System.Text.RegularExpressions.Regex.Match(value.Trim(), "^(?<n>\\d+)(?<u>KB|MB|GB|TB)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase); if (!m.Success || !long.TryParse(m.Groups["n"].Value, out var n)) return null; return m.Groups["u"].Value.ToUpperInvariant() switch { "KB" => n * 1024L, "MB" => n * 1024 * 1024, "GB" => n * 1024 * 1024 * 1024, "TB" => n * 1024 * 1024 * 1024 * 1024, _ => n }; }
    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); _stream.Writer.TryComplete(); _lifecycle.Dispose(); _warningSink?.Update(_instance.Name, []); }
    internal static long ParseStart(string value) => DateTime.TryParseExact(value.Trim(), "ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) ? date.Ticks : -1;
}

public static class MonitoringParser
{
    public static MonitoringSample Parse(string text, DateTimeOffset at, MonitoringSample? previous, HostResourceLimits? limits = null, MonitoringThresholds? thresholds = null, long hostVhdxPhysicalBytes = 0)
    {
        var sections = text.Replace("\r", "").Split("__DN_", StringSplitOptions.None); var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["STAT"] = sections[0] };
        foreach (var s in sections.Skip(1)) { var n = s.IndexOf("__\n", StringComparison.Ordinal); if (n > 0) map[s[..n]] = s[(n + 3)..]; }
        var unavailable = new Dictionary<string, string>(); var counters = new Dictionary<string, long>(); var cpu = Cpu(map, previous, unavailable, counters); var memTotal = Mem(map, "MemTotal", unavailable); var memFree = Mem(map, "MemAvailable", unavailable); var swapTotal = Mem(map, "SwapTotal", unavailable); var swapFree = Mem(map, "SwapFree", unavailable); var fs = Filesystem(map, unavailable); var io = Disk(map, previous, at, unavailable, counters); var net = Network(map, previous, at, unavailable, counters); var ports = Ports(map, unavailable);
        long? vhdx = hostVhdxPhysicalBytes > 0 ? hostVhdxPhysicalBytes : null;
        if (vhdx is null) unavailable["vhdxPhysical"] = "unavailable";
        // This is an estimate only: ext4 allocation and sparse VHDX allocation do not map 1:1.
        long? reclaimable = vhdx is > 0 && fs.used is >= 0 && vhdx >= fs.used ? vhdx - fs.used : null;
        if (reclaimable is null) unavailable["reclaimable"] = "unavailable";
        var processes = Processes(map, unavailable).Select(process => process with { ListeningPorts = ports.Where(port => port.ProcessId == process.Pid).Select(port => port.Port).Distinct().Order().ToArray() }).ToArray();
        var sample = new MonitoringSample(at, cpu, memTotal - memFree, memTotal, swapTotal - swapFree, swapTotal, fs.used, fs.total, io.read, io.write, net.receive, net.transmit, vhdx, reclaimable, processes, unavailable, limits, null, counters, ports);
        var warningList = Warnings(sample, thresholds ?? MonitoringThresholds.Default); return sample with { Warnings = warningList };
    }
    private static IReadOnlyList<ListeningPort> Ports(Dictionary<string, string> map, Dictionary<string, string> unavailable)
    {
        if (!map.TryGetValue("PORTS", out var value) || map.ContainsKey("PORTS_UNAVAILABLE") || value.Contains("__DN_PORTS_UNAVAILABLE__", StringComparison.Ordinal)) { unavailable["listeningPorts"] = "unavailable"; return []; }
        var ports = new List<ListeningPort>();
        foreach (var line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5 || !(fields[0].Equals("tcp", StringComparison.OrdinalIgnoreCase) || fields[0].Equals("udp", StringComparison.OrdinalIgnoreCase))) continue;
            var local = fields[4]; var separator = local.LastIndexOf(':');
            if (separator < 1 || !int.TryParse(local[(separator + 1)..], CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535) continue;
            if (ports.Count == 128) { unavailable["listeningPorts"] = "truncated"; break; }
            var processId = System.Text.RegularExpressions.Regex.Match(line, @"pid=(?<pid>\d+)");
            ports.Add(new ListeningPort(fields[0].ToUpperInvariant(), local[..separator], port, processId.Success && int.TryParse(processId.Groups["pid"].Value, CultureInfo.InvariantCulture, out var pid) ? pid : null));
        }
        return ports;
    }
    private static long? Mem(Dictionary<string,string> m,string key,Dictionary<string,string> u) { var line = m.GetValueOrDefault("MEM")?.Split('\n').FirstOrDefault(x=>x.StartsWith(key+":",StringComparison.Ordinal)); if (line is null || !long.TryParse(line.Split(' ',StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1),out var k)) { u[key]="unavailable"; return null; } return k*1024; }
    private static double? Cpu(Dictionary<string,string> m, MonitoringSample? prev, Dictionary<string,string> u, Dictionary<string,long> c) { var v=m.GetValueOrDefault("STAT")?.Split('\n').FirstOrDefault(x=>x.StartsWith("cpu "))?.Split(' ',StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(x=>long.TryParse(x,out var z)?z:0).ToArray(); if(v is null||v.Length<4){u["cpu"]="unavailable";return null;} var total=v.Sum();var idle=v[3]; long? oldTotal=Counter(prev,"cpu_total"); long? oldIdle=Counter(prev,"cpu_idle"); c["cpu_total"]=total;c["cpu_idle"]=idle; if(oldTotal is null || oldIdle is null) return null; var d=total-oldTotal.Value; return d<=0?null:Math.Clamp((d-(idle-oldIdle.Value))*100d/d,0d,100d); }
    private static (long? used,long? total) Filesystem(Dictionary<string,string> m,Dictionary<string,string> u){var l=m.GetValueOrDefault("FS")?.Split('\n').FirstOrDefault(x=>x.StartsWith("/"));var p=l?.Split(' ',StringSplitOptions.RemoveEmptyEntries);if(p is null||p.Length<5||!long.TryParse(p[1],out var t)||!long.TryParse(p[2],out var x)){u["filesystem"]="unavailable";return(null,null);}return(x*1024,t*1024);}
    private static (long? read,long? write) Disk(Dictionary<string,string> m, MonitoringSample? p, DateTimeOffset at, Dictionary<string,string> u, Dictionary<string,long> c) { long read=0,write=0; var rows=m.GetValueOrDefault("DISK")?.Split('\n') ?? []; foreach(var row in rows){var x=row.Split(' ',StringSplitOptions.RemoveEmptyEntries); if(x.Length<14||!x[2].StartsWith("sd",StringComparison.Ordinal)&&!x[2].StartsWith("vd",StringComparison.Ordinal)&&!x[2].StartsWith("nvme",StringComparison.Ordinal))continue; if(long.TryParse(x[5],out var r))read+=r*512; if(long.TryParse(x[9],out var w))write+=w*512;} return Rates(read,write,"disk_read","disk_write",p,at,u,"disk",c); }
    private static (long? receive,long? transmit) Network(Dictionary<string,string> m, MonitoringSample? p, DateTimeOffset at, Dictionary<string,string> u, Dictionary<string,long> c) { long receive=0,send=0; foreach(var row in m.GetValueOrDefault("NET")?.Split('\n').Skip(2)??[]){var x=row.Split(':',2);var n=x.ElementAtOrDefault(0)?.Trim();var v=x.ElementAtOrDefault(1)?.Split(' ',StringSplitOptions.RemoveEmptyEntries);if(n is null||n=="lo"||v is null||v.Length<9)continue;if(long.TryParse(v[0],out var r))receive+=r;if(long.TryParse(v[8],out var s))send+=s;} return Rates(receive,send,"net_receive","net_transmit",p,at,u,"network",c); }
    private static (long?,long?) Rates(long a,long b,string ka,string kb,MonitoringSample? p,DateTimeOffset at,Dictionary<string,string> u,string metric,Dictionary<string,long> c){var oa=Counter(p,ka);var ob=Counter(p,kb);var ot=Counter(p,"captured_ticks");c[ka]=a;c[kb]=b;c["captured_ticks"]=at.UtcTicks;if(oa is null||ob is null||ot is null){return(null,null);}var seconds=(at.UtcTicks-ot.Value)/(double)TimeSpan.TicksPerSecond;if(seconds<=0||a<oa||b<ob){u[metric]="counter reset";return(null,null);}return((long)((a-oa)/seconds),(long)((b-ob)/seconds));}
    private static long? Counter(MonitoringSample? s,string k)=>s?.CounterState is not null&&s.CounterState.TryGetValue(k,out var number)?number:null;
    private static IReadOnlyList<MonitoredProcess> Processes(Dictionary<string,string> m,Dictionary<string,string> u){var r=new List<MonitoredProcess>();foreach(var l in m.GetValueOrDefault("PROC")?.Split('\n').Skip(1)??[]){var p=l.Split(' ',StringSplitOptions.RemoveEmptyEntries);if(p.Length<10||!int.TryParse(p[0],out var pid)||!double.TryParse(p[7],CultureInfo.InvariantCulture,out var cpu)||!double.TryParse(p[8],CultureInfo.InvariantCulture,out var mem))continue;var start=MonitoringSession.ParseStart(string.Join(' ',p.Skip(1).Take(5)));r.Add(new(pid,start,p[6],cpu,mem,string.Join(' ',p.Skip(9)),[]));}if(r.Count==0)u["processes"]="unavailable";return r;}
    private static IReadOnlyList<MonitoringWarning> Warnings(MonitoringSample s, MonitoringThresholds t) { var x=new List<MonitoringWarning>(); if(s.CpuPercent>=t.CpuPercent)x.Add(new("cpu",s.CpuPercent.Value,t.CpuPercent,"CPU use is above the configured threshold.")); if(s.MemoryUsedBytes is not null&&s.MemoryTotalBytes>0&&s.MemoryUsedBytes*100d/s.MemoryTotalBytes>=t.MemoryPercent)x.Add(new("memory",s.MemoryUsedBytes.Value*100d/s.MemoryTotalBytes.Value,t.MemoryPercent,"Memory use is above the configured threshold.")); if(s.FilesystemUsedBytes is not null&&s.FilesystemTotalBytes>0&&s.FilesystemUsedBytes*100d/s.FilesystemTotalBytes>=t.FilesystemPercent)x.Add(new("filesystem",s.FilesystemUsedBytes.Value*100d/s.FilesystemTotalBytes.Value,t.FilesystemPercent,"Filesystem use is above the configured threshold.")); return x; }
}
