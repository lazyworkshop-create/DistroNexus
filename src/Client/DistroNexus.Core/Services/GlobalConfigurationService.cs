using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Owns the narrow, durable reviewed-save contract for global WSL settings.</summary>
public sealed class GlobalConfigurationService
{
    private const string SchemaRevision = "global-wslconfig-v1";
    private readonly IWslConfigurationService _configuration; private readonly IWslConfigService _host; private readonly IPlatformCapabilityService _capabilities;
    private readonly string _root; private readonly TimeProvider _clock; private readonly Func<string> _sid; private readonly Func<byte[], byte[]> _protect; private readonly Func<byte[], byte[]> _unprotect; private readonly int _maxRecords; private readonly long _maxBytes;
    public GlobalConfigurationService(IWslConfigurationService configuration, IWslConfigService host, IPlatformCapabilityService capabilities, string? root = null, TimeProvider? clock = null, Func<string>? sid = null, Func<byte[], byte[]>? protect = null, Func<byte[], byte[]>? unprotect = null, int maxRecords = 64, long maxBytes = 1_048_576)
    { if (maxRecords < 1 || maxBytes < 1024) throw new ArgumentOutOfRangeException(nameof(maxRecords)); _configuration = configuration; _host = host; _capabilities = capabilities; _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus", "global-configuration-grants"); _clock = clock ?? TimeProvider.System; _sid = sid ?? DefaultSid; _protect = protect ?? (bytes => ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); _unprotect = unprotect ?? (bytes => ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser)); _maxRecords = maxRecords; _maxBytes = maxBytes; }
    public async Task<GlobalConfigurationSnapshot> GetAsync(CancellationToken ct = default)
    { var doc = await _configuration.ReadAsync(ct); var caps = WslConfigurationSchema.MapCapabilities(await _capabilities.GetHostSnapshotAsync(false, ct)); var host = await _host.GetHostSpecsAsync(ct); return Snapshot(doc, caps, host); }
    public async Task<GlobalConfigurationPreview> PreviewAsync(IReadOnlyDictionary<string,string?> changes, CancellationToken ct = default)
    {
        var doc = await _configuration.ReadAsync(ct); var caps = WslConfigurationSchema.MapCapabilities(await _capabilities.GetHostSnapshotAsync(false, ct)); var host = await _host.GetHostSpecsAsync(ct);
        var canonical = Canonicalize(changes, caps, host.CpuCount); var preview = await _configuration.PreviewAsync(canonical, doc.Fingerprint, caps, ct);
        var token = Guid.NewGuid().ToString("N"); Persist(new Grant(token, canonical, doc.Fingerprint, caps.Order(StringComparer.Ordinal).ToArray(), SchemaRevision, _clock.GetUtcNow().AddMinutes(2), _sid()));
        return new(canonical, preview.ChangedSettings, Display(Apply(Project(doc), canonical)), preview.RestartScope == RestartScope.Wsl, token);
    }
    public async Task<GlobalConfigurationApplyResult> ExecuteAsync(string token, CancellationToken ct = default)
    {
        var grant = Consume(token) ?? throw new InvalidOperationException("DN-8004: A current global configuration preview is required.");
        var doc = await _configuration.ReadAsync(ct); var caps = WslConfigurationSchema.MapCapabilities(await _capabilities.GetHostSnapshotAsync(false, ct));
        if (!string.Equals(doc.Fingerprint, grant.Fingerprint, StringComparison.Ordinal) || !caps.SetEquals(grant.Capabilities) || grant.SchemaRevision != SchemaRevision) throw new InvalidOperationException("DN-8004: The global configuration or capabilities changed; generate a new preview.");
        var saved = await _configuration.SaveAsync(grant.Changes, grant.Fingerprint, caps, ct); return new(grant.Changes.Keys.Order(StringComparer.Ordinal).ToArray(), saved.RestartScope == RestartScope.Wsl);
    }
    private GlobalConfigurationSnapshot Snapshot(ConfigurationDocument<WslConfigurationSettings> doc, IReadOnlySet<string> caps, (long TotalRamMb, int CpuCount) host)
    { var values = Project(doc); return new(values, WslConfigurationSchema.Global.Where(x => x.RequiredCapability is null || caps.Contains(x.RequiredCapability)).Select(x => $"{x.Section}.{x.Key}").Order(StringComparer.Ordinal).ToArray(), caps.Order(StringComparer.Ordinal).ToArray(), Display(values), doc.RestartScope == RestartScope.Wsl, host.TotalRamMb, host.CpuCount); }
    private static Dictionary<string,string?> Canonicalize(IReadOnlyDictionary<string,string?> changes, IReadOnlySet<string> caps, int hostCpuCount)
    { if (changes.Count < 1 || changes.Count > WslConfigurationSchema.Global.Count) throw new ArgumentException("DN-8003: Global configuration changes are invalid."); var r = new Dictionary<string,string?>(StringComparer.Ordinal); foreach (var pair in changes) { var d = WslConfigurationSchema.Global.SingleOrDefault(x => string.Equals($"{x.Section}.{x.Key}", pair.Key, StringComparison.OrdinalIgnoreCase)); if (d is null || (d.RequiredCapability is not null && !caps.Contains(d.RequiredCapability)) || pair.Value?.Length > 512 || pair.Value?.IndexOfAny(['\0','\r','\n']) >= 0 || (string.Equals(pair.Key, "wsl2.processors", StringComparison.OrdinalIgnoreCase) && (!int.TryParse(pair.Value, out var cpu) || cpu < 1 || hostCpuCount > 0 && cpu > hostCpuCount))) throw new ArgumentException("DN-8003: Global configuration changes are invalid."); if (pair.Value is not null) { var candidate = LosslessIniDocument.Empty().WithValue(d.Section, d.Key, pair.Value.Trim()); if (WslConfigurationSchema.Validate(candidate, WslConfigurationSchema.Global, caps).Any(x => x.Severity == ConfigurationDiagnosticSeverity.Error)) throw new ArgumentException("DN-8003: Global configuration changes are invalid."); } r[$"{d.Section}.{d.Key}"] = pair.Value?.Trim(); } return r; }
    private void Persist(Grant g) { Directory.CreateDirectory(_root); var bytes = _protect(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(g))); using var gate = AcquireAdmissionLock(); CleanupAndEnsureCapacity(bytes.Length); var p = PathFor(g.Token); var t = p + ".tmp." + Guid.NewGuid().ToString("N"); try { File.WriteAllBytes(t, bytes); File.Move(t,p,false); } finally { if (File.Exists(t)) File.Delete(t); } }
    private Grant? Consume(string token) { if (string.IsNullOrWhiteSpace(token) || token.Length != 32 || token.Any(c => !Uri.IsHexDigit(c))) return null; var p=PathFor(token); var used=p+".consumed."+Guid.NewGuid().ToString("N"); try { File.Move(p,used); var g=ReadGrant(used); File.Delete(used); return g is not null && g.Token==token && g.ExpiresAt>_clock.GetUtcNow() && g.Sid==_sid()?g:null; } catch { return null; } }
    private string PathFor(string token) => Path.Combine(_root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))+".bin");
    private void CleanupAndEnsureCapacity(int incomingBytes)
    {
        var files = Directory.EnumerateFiles(_root, "*.bin").Take(_maxRecords + 1).ToArray();
        foreach (var path in files) { var grant = ReadGrant(path); if (grant is null || grant.ExpiresAt <= _clock.GetUtcNow()) File.Delete(path); }
        files = Directory.EnumerateFiles(_root, "*.bin").ToArray(); var total = files.Sum(path => new FileInfo(path).Length);
        if (files.Length >= _maxRecords || total + incomingBytes > _maxBytes) throw new InvalidOperationException("DN-8005: Global configuration preview capacity is unavailable.");
    }
    private FileStream AcquireAdmissionLock()
    {
        var lockPath = Path.Combine(_root, ".admission.lock");
        for (var attempt = 0; attempt != 100; attempt++)
        {
            try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough); }
            catch (IOException) { Thread.Sleep(10); }
        }
        throw new InvalidOperationException("DN-8005: Global configuration preview capacity is unavailable.");
    }
    private Grant? ReadGrant(string path) { try { return JsonSerializer.Deserialize<Grant>(Encoding.UTF8.GetString(_unprotect(File.ReadAllBytes(path)))); } catch { return null; } }
    private static Dictionary<string,string> Project(ConfigurationDocument<WslConfigurationSettings> doc) => doc.Settings.Values.Where(pair => WslConfigurationSchema.Global.Any(d => string.Equals($"{d.Section}.{d.Key}", pair.Key, StringComparison.OrdinalIgnoreCase))).ToDictionary(pair => CanonicalId(pair.Key), pair => pair.Value, StringComparer.Ordinal);
    private static Dictionary<string,string> Apply(IReadOnlyDictionary<string,string> source, IReadOnlyDictionary<string,string?> changes) { var values = new Dictionary<string,string>(source, StringComparer.Ordinal); foreach (var (key,value) in changes) if (value is null) values.Remove(key); else values[key]=value; return values; }
    private static string CanonicalId(string id) => WslConfigurationSchema.Global.Select(d => $"{d.Section}.{d.Key}").Single(x => string.Equals(x,id,StringComparison.OrdinalIgnoreCase));
    private static string Display(IReadOnlyDictionary<string,string> values) => string.Join(Environment.NewLine, values.OrderBy(x => x.Key, StringComparer.Ordinal).Take(32).Select(x => $"{x.Key}={x.Value}"));
    private static string DefaultSid()=>WindowsIdentity.GetCurrent().User?.Value??throw new InvalidOperationException("Current user identity is unavailable.");
    private sealed record Grant(string Token, Dictionary<string,string?> Changes, string Fingerprint, string[] Capabilities, string SchemaRevision, DateTimeOffset ExpiresAt, string Sid);
}
