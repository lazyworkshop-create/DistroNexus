using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Owns bounded, durable package-download job state. Every read/modify/write is serialized across bridge processes.</summary>
public sealed class PackageDownloadJobService : IPackageDownloadJobService
{
    private readonly ICatalogService _catalog;
    private readonly IDownloadService _downloads;
    private readonly string _root, _storePath, _grantStorePath, _user, _mutexName;
    private readonly Dictionary<string, CancellationTokenSource> _running = new(StringComparer.Ordinal);
    private bool _recovered;
    private const int MaxJobs = 200;
    private const int MaxGrants = 512;

    public PackageDownloadJobService(ICatalogService catalog, IDownloadService downloads, string root)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); _downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
        _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root))); _storePath = Path.Combine(_root, "package-download-jobs.protected"); _grantStorePath = Path.Combine(_root, "package-download-grants.protected");
        _user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Package.JobUnavailable");
        _mutexName = "Local\\DistroNexus.PackageJobs." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_root.ToUpperInvariant() + "|" + _user)));
    }

    public async Task<PackageJobStartPreviewResult> PreviewStartAsync(string packageId, CancellationToken cancellationToken = default)
    {
        packageId = packageId?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(packageId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")) return new(null, null, packageId, "", "Package.JobPackageNotFound");
        var package = await _catalog.GetDistributionByIdAsync(packageId, cancellationToken).ConfigureAwait(false);
        if (package is null) return new(null, null, packageId, "", "Package.JobPackageNotFound");
        var provenance = await _catalog.GetPackageDownloadProvenanceAsync(package, cancellationToken).ConfigureAwait(false);
        if (!TryCreateMaterial(package, provenance, out var material)) return new(null, null, packageId, "", "Package.JobPackageMetadataInvalid");
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5); var token = Token(64);
        await InStoreLockAsync(async () => { var jobs = await ReadJobsAsync(cancellationToken); if (Recover(jobs)) await WriteJobsAsync(jobs, cancellationToken); var grants = await ReadGrantsAsync(cancellationToken); Cleanup(grants); grants.Active[token] = new Grant(token, _user, expiry, "start", null, material.PackageId, package.Name[..Math.Min(256, package.Name.Length)], material.Fingerprint, material.Url, material.Sha256, material.Size, material.Provenance, material.CacheFileName, material.Version); Trim(grants); await WriteGrantsAsync(grants, cancellationToken); }, cancellationToken);
        return new(token, expiry, package.Id, package.Name[..Math.Min(256, package.Name.Length)], "Package.JobPreviewReady");
    }

    public Task<PackageJobStartResult> StartAsync(string previewToken, CancellationToken cancellationToken = default) => InStoreLockAsync(async () =>
    {
        var recoveryJobs = await ReadJobsAsync(cancellationToken); if (Recover(recoveryJobs)) await WriteJobsAsync(recoveryJobs, cancellationToken);
        var grants = await ReadGrantsAsync(cancellationToken); var grant = Consume(grants, previewToken, "start"); await WriteGrantsAsync(grants, cancellationToken);
        if (grant is null) return new PackageJobStartResult(null, GrantFailure(grants, previewToken));
        var current = await _catalog.GetDistributionByIdAsync(grant.PackageId!, cancellationToken).ConfigureAwait(false);
        if (current is null) return new PackageJobStartResult(null, "Package.JobStateChanged");
        var currentProvenance = await _catalog.GetPackageDownloadProvenanceAsync(current, cancellationToken).ConfigureAwait(false);
        if (!TryCreateMaterial(current, currentProvenance, out var currentMaterial) || !Matches(grant, currentMaterial)) return new PackageJobStartResult(null, "Package.JobStateChanged");
        var jobs = await ReadJobsAsync(cancellationToken); var existing = jobs.FirstOrDefault(x => x.Owner == _user && x.Fingerprint == grant.Fingerprint && x.State is "Queued" or "Running");
        if (existing is not null) { await WriteJobsAsync(jobs, cancellationToken); return new(existing.JobId, "Package.ExistingActive"); }
        var id = Token(32); jobs.Add(new StoredJob(id, grant.PackageId!, grant.PackageLabel!, grant.Fingerprint!, "Queued", 0, "Package.Queued", _user, grant.Url!, grant.Sha256!, grant.Size, grant.CacheFileName!)); TrimJobs(jobs); await WriteJobsAsync(jobs, cancellationToken); _ = RunTransferAsync(id); return new(id, "Package.Created");
    }, cancellationToken);

    public Task<IReadOnlyList<PackageDownloadJob>> ListAsync(CancellationToken cancellationToken = default) => InStoreLockAsync(async () =>
    { var jobs = await ReadJobsAsync(cancellationToken); if (Recover(jobs)) await WriteJobsAsync(jobs, cancellationToken); return (IReadOnlyList<PackageDownloadJob>)jobs.Where(x => x.Owner == _user).Take(MaxJobs).Select(ToPublic).ToArray(); }, cancellationToken);

    public Task<PackageJobActionPreviewResult> PreviewActionAsync(string jobId, string action, CancellationToken cancellationToken = default) => InStoreLockAsync(async () =>
    {
        var recoveryJobs = await ReadJobsAsync(cancellationToken); if (Recover(recoveryJobs)) await WriteJobsAsync(recoveryJobs, cancellationToken);
        if (!IsId(jobId) || action is not ("cancel" or "retry" or "clear")) return new PackageJobActionPreviewResult(null, null, jobId, "Package.JobUnavailable");
        var jobs = await ReadJobsAsync(cancellationToken); var job = jobs.SingleOrDefault(x => x.JobId == jobId && x.Owner == _user);
        if (job is null || !CanAction(job, action)) return new PackageJobActionPreviewResult(null, null, jobId, "Package.JobStateChanged");
        var grants = await ReadGrantsAsync(cancellationToken); Cleanup(grants); var token = Token(64); var expiry = DateTimeOffset.UtcNow.AddMinutes(5); grants.Active[token] = new Grant(token, _user, expiry, action, jobId, null, null, null, null, null, 0); Trim(grants); await WriteGrantsAsync(grants, cancellationToken); return new(token, expiry, jobId, "Package.JobPreviewReady");
    }, cancellationToken);

    public Task<PackageJobActionResult> ExecuteActionAsync(string previewToken, CancellationToken cancellationToken = default) => InStoreLockAsync(async () =>
    {
        var recoveryJobs = await ReadJobsAsync(cancellationToken); if (Recover(recoveryJobs)) await WriteJobsAsync(recoveryJobs, cancellationToken);
        var grants = await ReadGrantsAsync(cancellationToken); var grant = Consume(grants, previewToken, null); await WriteGrantsAsync(grants, cancellationToken);
        if (grant is null) return new PackageJobActionResult("", GrantFailure(grants, previewToken));
        var jobs = await ReadJobsAsync(cancellationToken); var job = jobs.SingleOrDefault(x => x.JobId == grant.JobId && x.Owner == _user); if (job is null || !CanAction(job, grant.Action)) return new(grant.JobId ?? "", "Package.JobStateChanged");
        if (grant.Action == "cancel") { if (_running.Remove(job.JobId, out var c)) c.Cancel(); job.State = "Cancelled"; job.OutcomeCode = "Package.Cancelled"; await WriteJobsAsync(jobs, cancellationToken); return new(job.JobId, "Package.Cancelled"); }
        if (grant.Action == "clear") { jobs.Remove(job); await WriteJobsAsync(jobs, cancellationToken); return new(job.JobId, "Package.Cleared"); }
        job.State = "Queued"; job.ProgressPercent = 0; job.OutcomeCode = "Package.Queued"; await WriteJobsAsync(jobs, cancellationToken); _ = RunTransferAsync(job.JobId); return new(job.JobId, "Package.Retried");
    }, cancellationToken);

    private async Task RunTransferAsync(string jobId)
    {
        var cts = new CancellationTokenSource(); StoredJob? job = null;
        await InStoreLockAsync(async () => { var jobs = await ReadJobsAsync(CancellationToken.None); job = jobs.SingleOrDefault(x => x.JobId == jobId && x.Owner == _user && x.State == "Queued"); if (job is not null) { _running[jobId] = cts; job.State = "Running"; await WriteJobsAsync(jobs, CancellationToken.None); } }, CancellationToken.None);
        if (job is null) { cts.Dispose(); return; }
        var destination = Path.Combine(_root, "cache", job.CacheFileName); var ok = false;
        try { var progress = new Progress<(long BytesRead, long TotalBytes)>(p => _ = UpdateProgressAsync(jobId, p.BytesRead, p.TotalBytes)); ok = await _downloads.DownloadFileAsync(job.Url, destination, progress, cts.Token).ConfigureAwait(false); ok = ok && File.Exists(destination) && new FileInfo(destination).Length == job.Size && await _downloads.VerifyChecksumAsync(destination, job.Sha256, cts.Token).ConfigureAwait(false); } catch (OperationCanceledException) { }
        await InStoreLockAsync(async () => { var jobs = await ReadJobsAsync(CancellationToken.None); var current = jobs.SingleOrDefault(x => x.JobId == jobId); if (current is not null) { if (!ok) { var partial = Path.Combine(_root, "cache", current.CacheFileName); if (File.Exists(partial)) File.Delete(partial); } if (current.State != "Cancelled") { current.State = ok ? "Completed" : "Failed"; current.ProgressPercent = ok ? 100 : current.ProgressPercent; current.OutcomeCode = ok ? "Package.Completed" : "Package.Failed"; await WriteJobsAsync(jobs, CancellationToken.None); } } _running.Remove(jobId); }, CancellationToken.None); cts.Dispose();
    }
    private Task UpdateProgressAsync(string id, long bytes, long total) => InStoreLockAsync(async () => { var jobs = await ReadJobsAsync(CancellationToken.None); var job = jobs.SingleOrDefault(x => x.JobId == id && x.State == "Running"); if (job is not null) { job.ProgressPercent = total > 0 ? Math.Clamp((int)(bytes * 100 / total), 0, 99) : 0; await WriteJobsAsync(jobs, CancellationToken.None); } }, CancellationToken.None);
    private bool Recover(List<StoredJob> jobs) { if (_recovered) return false; foreach (var job in jobs.Where(j => j.Owner == _user && j.State is "Queued" or "Running")) { job.State = "Interrupted"; job.OutcomeCode = "Package.Interrupted"; var partial = Path.Combine(_root, "cache", job.CacheFileName); if (File.Exists(partial)) File.Delete(partial); } _recovered = true; return true; }
    private async Task<T> InStoreLockAsync<T>(Func<Task<T>> action, CancellationToken ct) { using var mutex = new Semaphore(1, 1, _mutexName); await Task.Run(() => { WaitHandle.WaitAny([mutex, ct.WaitHandle]); ct.ThrowIfCancellationRequested(); }, ct); try { return await action(); } finally { mutex.Release(); } }
    private Task InStoreLockAsync(Func<Task> action, CancellationToken ct) => InStoreLockAsync(async () => { await action(); return 0; }, ct);
    private async Task<List<StoredJob>> ReadJobsAsync(CancellationToken ct) { if (!File.Exists(_storePath)) return []; try { var clear = ProtectedData.Unprotect(await File.ReadAllBytesAsync(_storePath, ct), Encoding.UTF8.GetBytes(_user), DataProtectionScope.CurrentUser); return JsonSerializer.Deserialize<List<StoredJob>>(clear) ?? []; } catch { return []; } }
    private async Task WriteJobsAsync(List<StoredJob> jobs, CancellationToken ct) { Directory.CreateDirectory(_root); var tmp = _storePath + "." + Guid.NewGuid().ToString("N") + ".tmp"; await File.WriteAllBytesAsync(tmp, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(jobs), Encoding.UTF8.GetBytes(_user), DataProtectionScope.CurrentUser), ct); File.Move(tmp, _storePath, true); }
    private async Task<GrantStore> ReadGrantsAsync(CancellationToken ct) { if (!File.Exists(_grantStorePath)) return new(); try { var clear = ProtectedData.Unprotect(await File.ReadAllBytesAsync(_grantStorePath, ct), Encoding.UTF8.GetBytes(_user), DataProtectionScope.CurrentUser); return JsonSerializer.Deserialize<GrantStore>(clear) ?? new(); } catch { return new(); } }
    private async Task WriteGrantsAsync(GrantStore grants, CancellationToken ct) { Directory.CreateDirectory(_root); var tmp = _grantStorePath + "." + Guid.NewGuid().ToString("N") + ".tmp"; await File.WriteAllBytesAsync(tmp, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grants), Encoding.UTF8.GetBytes(_user), DataProtectionScope.CurrentUser), ct); File.Move(tmp, _grantStorePath, true); }
    private Grant? Consume(GrantStore s, string token, string? action) { if (!IsToken(token) || !s.Active.Remove(token, out var g)) return null; s.Consumed[token] = g.ExpiresAt; return g.Owner == _user && g.ExpiresAt >= DateTimeOffset.UtcNow && (action is null || action == g.Action) ? g : null; }
    private static void Cleanup(GrantStore s) { var now = DateTimeOffset.UtcNow; foreach (var key in s.Active.Where(x => x.Value.ExpiresAt < now).Select(x => x.Key).ToArray()) s.Active.Remove(key); foreach (var key in s.Consumed.Where(x => x.Value < now).Select(x => x.Key).ToArray()) s.Consumed.Remove(key); }
    private static void Trim(GrantStore s) { while (s.Active.Count > MaxGrants) s.Active.Remove(s.Active.OrderBy(x => x.Value.ExpiresAt).First().Key); while (s.Consumed.Count > MaxGrants) s.Consumed.Remove(s.Consumed.OrderBy(x => x.Value).First().Key); }
    private static void TrimJobs(List<StoredJob> jobs) { if (jobs.Count > MaxJobs) jobs.RemoveRange(0, jobs.Count - MaxJobs); }
    private static string GrantFailure(GrantStore s, string? token)
    {
        if (token is null || !IsToken(token)) return "Package.JobGrantInvalid";
        if (s.Consumed.ContainsKey(token)) return "Package.JobGrantReplayed";
        return s.Active.TryGetValue(token, out var grant) && grant.ExpiresAt < DateTimeOffset.UtcNow
            ? "Package.JobGrantExpired"
            : "Package.JobGrantInvalid";
    }
    private static bool CanAction(StoredJob j, string a) => a == "cancel" ? j.State is "Queued" or "Running" : a == "retry" ? j.State is "Failed" or "Cancelled" or "Interrupted" : a == "clear" && j.State is not ("Queued" or "Running");
    private static bool IsId(string? s) => s?.Length == 32 && s.All(Uri.IsHexDigit); private static bool IsToken(string? s) => s?.Length == 64 && s.All(Uri.IsHexDigit); private static bool IsSha(string? s) => s?.Length == 64 && s.All(Uri.IsHexDigit);
    private static string Token(int chars) => Convert.ToHexString(RandomNumberGenerator.GetBytes(chars / 2)).ToLowerInvariant(); private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    private static bool TryCreateMaterial(DistroPackage package, string? provenance, out PackageMaterial material)
    {
        material = default!;
        if (!Uri.TryCreate(package.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !IsSha(package.Sha256) || package.FileSize <= 0 || string.IsNullOrWhiteSpace(package.Id) || string.IsNullOrWhiteSpace(package.Version) || string.IsNullOrWhiteSpace(package.Name) || string.IsNullOrWhiteSpace(provenance)) return false;
        var normalizedUrl = uri.AbsoluteUri;
        var normalizedHash = package.Sha256.ToUpperInvariant();
        var cacheFileName = Hash($"{package.Id}|{package.Version}|{provenance}")[..32].ToLowerInvariant() + ".package";
        material = new(package.Id, package.Version, normalizedUrl, normalizedHash, package.FileSize, provenance, cacheFileName, Hash($"{package.Id}|{package.Version}|{normalizedUrl}|{normalizedHash}|{package.FileSize}|{provenance}|{cacheFileName}"));
        return true;
    }
    private static bool Matches(Grant grant, PackageMaterial material) =>
        string.Equals(grant.PackageId, material.PackageId, StringComparison.Ordinal) && string.Equals(grant.Version, material.Version, StringComparison.Ordinal) &&
        string.Equals(grant.Url, material.Url, StringComparison.Ordinal) && string.Equals(grant.Sha256, material.Sha256, StringComparison.Ordinal) &&
        grant.Size == material.Size && string.Equals(grant.Provenance, material.Provenance, StringComparison.Ordinal) &&
        string.Equals(grant.CacheFileName, material.CacheFileName, StringComparison.Ordinal) && string.Equals(grant.Fingerprint, material.Fingerprint, StringComparison.Ordinal);
    private static PackageDownloadJob ToPublic(StoredJob x) => new(x.JobId, x.PackageId, x.PackageLabel, x.State, Math.Clamp(x.ProgressPercent, 0, 100), x.OutcomeCode);
    private sealed record PackageMaterial(string PackageId, string Version, string Url, string Sha256, long Size, string Provenance, string CacheFileName, string Fingerprint);
    private sealed record Grant(string Token, string Owner, DateTimeOffset ExpiresAt, string Action, string? JobId, string? PackageId, string? PackageLabel, string? Fingerprint, string? Url, string? Sha256, long Size, string? Provenance = null, string? CacheFileName = null, string? Version = null);
    private sealed class GrantStore { public Dictionary<string, Grant> Active { get; set; } = new(StringComparer.Ordinal); public Dictionary<string, DateTimeOffset> Consumed { get; set; } = new(StringComparer.Ordinal); }
    private sealed class StoredJob { public StoredJob() { } public StoredJob(string id, string packageId, string label, string fingerprint, string state, int progress, string outcome, string owner, string url, string sha256, long size, string cacheFileName) { JobId=id; PackageId=packageId; PackageLabel=label; Fingerprint=fingerprint; State=state; ProgressPercent=progress; OutcomeCode=outcome; Owner=owner; Url=url; Sha256=sha256; Size=size; CacheFileName=cacheFileName; } public string JobId { get; set; }=""; public string PackageId { get; set; }=""; public string PackageLabel { get; set; }=""; public string Fingerprint { get; set; }=""; public string State { get; set; }=""; public int ProgressPercent { get; set; } public string OutcomeCode { get; set; }=""; public string Owner { get; set; }=""; public string Url { get; set; }=""; public string Sha256 { get; set; }=""; public long Size { get; set; } public string CacheFileName { get; set; }=""; }
}
