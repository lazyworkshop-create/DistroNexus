using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>
/// Narrow Core-owned backup runner.  The caller supplies no host path, executable, task name,
/// command, argument list, or archive glob.  Archives are always under the product root.
/// </summary>
public sealed class FixedBackupRuntime : IFixedBackupRuntime
{
    private readonly IWslManagerService _instances; private readonly IProcessRunner _processes; private readonly string _root; private readonly string _grantRoot;
    public FixedBackupRuntime(IWslManagerService instances, IProcessRunner processes, string root) { _instances = instances; _processes = processes; _root = Path.GetFullPath(root); _grantRoot = Path.Combine(_root, "backup-grants"); }
    public async Task<IReadOnlyList<BackupScheduleSummary>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var path = SchedulePath(); if (!File.Exists(path)) return [];
        try { return JsonSerializer.Deserialize<List<BackupScheduleSummary>>(await File.ReadAllTextAsync(path, cancellationToken)) ?? []; }
        catch (JsonException) { return []; }
    }
    public Task<BackupOperationPreview> PreviewScheduleAsync(BackupScheduleRequest request, CancellationToken cancellationToken = default)
    { Validate(request.InstanceName, request.RetentionCount); ValidateSchedule(request.Frequency, request.Time); ValidateLegacyDestination(request.Destination); return IssueAsync(request.InstanceName, request.RetentionCount, "Schedule", cancellationToken, request.Frequency, request.Time); }
    public Task<BackupOperationPreview> PreviewScheduleRemovalAsync(string instanceName, CancellationToken cancellationToken = default)
    { Validate(instanceName, 1); return IssueAsync(instanceName, 1, "RemoveSchedule", cancellationToken); }
    public Task<BackupOperationPreview> PreviewBackupAsync(string instanceName, int retentionCount, CancellationToken cancellationToken = default) { Validate(instanceName, retentionCount); return IssueAsync(instanceName, retentionCount, "Backup", cancellationToken); }
    public async Task<BackupOperationResult> ExecuteAsync(string previewToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(previewToken) || previewToken.Length != 32 || previewToken.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("A current backup preview is required.");
        var grant = await ConsumeAsync(previewToken, cancellationToken);
        var registered = await _instances.GetInstancesAsync(cancellationToken); if (!registered.Any(x => string.Equals(x.Name, grant.InstanceName, StringComparison.Ordinal))) throw new InvalidOperationException("The selected instance is no longer registered.");
        if (grant.Operation == "Schedule") { await SaveScheduleAsync(grant, cancellationToken); return new(true, "Scheduled", grant.InstanceName, DateTimeOffset.UtcNow); }
        if (grant.Operation == "RemoveSchedule") { await RemoveScheduleAsync(grant.InstanceName, cancellationToken); return new(true, "ScheduleRemoved", grant.InstanceName, DateTimeOffset.UtcNow); }
        var directory = Path.Combine(_root, "backups", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(grant.InstanceName)))); Directory.CreateDirectory(directory);
        var archive = Path.Combine(directory, $"backup-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.tar");
        var stop = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--terminate", grant.InstanceName], TimeSpan.FromMinutes(1)), cancellationToken);
        if (stop.ExitCode is not (0 or null)) return new(false, "TerminateFailed", grant.InstanceName, DateTimeOffset.UtcNow);
        var export = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--export", grant.InstanceName, archive], TimeSpan.FromMinutes(30)), cancellationToken);
        if (export.ExitCode != 0) return new(false, "ExportFailed", grant.InstanceName, DateTimeOffset.UtcNow);
        foreach (var file in Directory.EnumerateFiles(directory, "backup-*.tar", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).Skip(grant.RetentionCount)) File.Delete(file);
        return new(true, "Completed", grant.InstanceName, DateTimeOffset.UtcNow);
    }
    public async Task<BackupOperationResult> RunScheduledAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scheduleId) || scheduleId.Length != 32 || scheduleId.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("The backup schedule identity is invalid.");
        var entries = await ReadScheduleEntriesAsync(cancellationToken);
        var entry = entries.SingleOrDefault(x => string.Equals(x.Id, scheduleId, StringComparison.Ordinal)) ?? throw new InvalidOperationException("The backup schedule was not found.");
        var task = await _processes.RunAsync(new ProcessRequest("schtasks.exe", ["/Query", "/TN", TaskName(entry.InstanceName), "/XML"], TimeSpan.FromMinutes(1)), cancellationToken);
        if (task.ExitCode != 0 || !task.StandardOutput.Contains(ExpectedAction(entry.Id), StringComparison.Ordinal)) throw new InvalidOperationException("The product backup task definition is invalid.");
        var preview = await PreviewBackupAsync(entry.InstanceName, entry.RetentionCount, cancellationToken);
        return await ExecuteAsync(preview.Token, cancellationToken);
    }
    public async Task<IReadOnlyList<BackupNotification>> ConsumeNotificationsAsync(CancellationToken cancellationToken = default)
    { var source = Path.Combine(_root, "pending-notifications.json"); if (!File.Exists(source)) return []; var claimed = source + ".consumed." + Guid.NewGuid().ToString("N"); try { File.Move(source, claimed); var document = JsonDocument.Parse(await File.ReadAllTextAsync(claimed, cancellationToken)); var values = new List<BackupNotification>(); if (document.RootElement.TryGetProperty("notifications", out var notifications)) foreach (var item in notifications.EnumerateArray()) { var instance = item.TryGetProperty("instance", out var i) ? i.GetString() : null; var message = item.TryGetProperty("message", out var m) ? m.GetString() : null; if (!string.IsNullOrWhiteSpace(instance) && !string.IsNullOrWhiteSpace(message)) values.Add(new(instance, "BackupFailed", message)); } return values; } finally { try { File.Delete(claimed); } catch { } } }
    private async Task<BackupOperationPreview> IssueAsync(string instanceName, int retentionCount, string operation, CancellationToken ct, string? frequency = null, TimeSpan? time = null) { var list = await _instances.GetInstancesAsync(ct); if (!list.Any(x => string.Equals(x.Name, instanceName, StringComparison.Ordinal))) throw new InvalidOperationException("The selected instance is not registered."); var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)); var expires = DateTimeOffset.UtcNow.AddMinutes(5); await WriteAsync(token, new(CurrentSid(), instanceName, retentionCount, operation, expires, frequency, time), ct); return new(token, instanceName, operation, retentionCount, expires); }
    private static void Validate(string instance, int retention) { if (string.IsNullOrWhiteSpace(instance) || instance.IndexOfAny(['\r','\n','\0']) >= 0 || retention is < 1 or > 30) throw new ArgumentException("The backup request is invalid."); }
    private static void ValidateLegacyDestination(string? destination)
    {
        if (destination is not null && (string.IsNullOrWhiteSpace(destination) || destination.IndexOfAny(['\r', '\n', '\0']) >= 0 || !Path.IsPathFullyQualified(destination)))
            throw new ArgumentException("The legacy backup destination is invalid.");
    }
    private static void ValidateSchedule(string frequency, TimeSpan time) { if (frequency is not "Daily" && !System.Text.RegularExpressions.Regex.IsMatch(frequency ?? string.Empty, "^(Weekly:(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)|Monthly:([1-9]|[12][0-9]|3[01]))$") || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) throw new ArgumentException("The backup schedule is invalid."); }
    private string SchedulePath() => Path.Combine(_root, "backup-schedules.json");
    private async Task<List<ScheduleEntry>> ReadScheduleEntriesAsync(CancellationToken ct) { var path = SchedulePath(); if (!File.Exists(path)) return []; try { return JsonSerializer.Deserialize<List<ScheduleEntry>>(await File.ReadAllTextAsync(path, ct)) ?? []; } catch (JsonException) { return []; } }
    private async Task SaveScheduleAsync(Grant grant, CancellationToken ct) { var schedules = (await ReadScheduleEntriesAsync(ct)).Where(x => !string.Equals(x.InstanceName, grant.InstanceName, StringComparison.Ordinal)).ToList(); var entry = new ScheduleEntry(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), grant.InstanceName, grant.Frequency!, grant.RetentionCount, grant.Time!.Value); schedules.Add(entry); await File.WriteAllTextAsync(SchedulePath(), JsonSerializer.Serialize(schedules), ct); await RegisterTaskAsync(entry, ct); }
    private async Task RemoveScheduleAsync(string name, CancellationToken ct) { var schedules = await ReadScheduleEntriesAsync(ct); var entry = schedules.SingleOrDefault(x => string.Equals(x.InstanceName, name, StringComparison.Ordinal)); if (entry is not null) { var task = await _processes.RunAsync(new ProcessRequest("schtasks.exe", ["/Query", "/TN", TaskName(name), "/XML"], TimeSpan.FromMinutes(1)), ct); if (task.ExitCode == 0 && !task.StandardOutput.Contains(ExpectedAction(entry.Id), StringComparison.Ordinal)) throw new InvalidOperationException("The product backup task definition is invalid."); } await File.WriteAllTextAsync(SchedulePath(), JsonSerializer.Serialize(schedules.Where(x => !string.Equals(x.InstanceName, name, StringComparison.Ordinal)).ToList()), ct); await _processes.RunAsync(new ProcessRequest("schtasks.exe", ["/Delete", "/TN", TaskName(name), "/F"], TimeSpan.FromMinutes(1)), ct); }
    private async Task RegisterTaskAsync(ScheduleEntry entry, CancellationToken ct) { var args = new List<string> { "/Create", "/TN", TaskName(entry.InstanceName), "/SC", entry.Frequency.StartsWith("Weekly", StringComparison.Ordinal) ? "WEEKLY" : entry.Frequency.StartsWith("Monthly", StringComparison.Ordinal) ? "MONTHLY" : "DAILY", "/ST", entry.Time.ToString("hh\\:mm"), "/TR", ExpectedAction(entry.Id), "/F" }; var result = await _processes.RunAsync(new ProcessRequest("schtasks.exe", args, TimeSpan.FromMinutes(1)), ct); if (result.ExitCode != 0) throw new InvalidOperationException("The product backup task could not be registered."); var actual = await _processes.RunAsync(new ProcessRequest("schtasks.exe", ["/Query", "/TN", TaskName(entry.InstanceName), "/XML"], TimeSpan.FromMinutes(1)), ct); if (actual.ExitCode != 0 || !actual.StandardOutput.Contains(ExpectedAction(entry.Id), StringComparison.Ordinal)) throw new InvalidOperationException("The product backup task definition is invalid."); }
    private static string ExpectedAction(string id)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "DistroNexus.WorkspaceBridge.exe");
        if (!File.Exists(executable)) throw new InvalidOperationException("The packaged DistroNexus WorkspaceBridge executable is unavailable.");
        return "\"" + executable + "\" --run-backup-schedule " + id;
    }
    private static string TaskName(string name) => "DistroNexus_Backup_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).Substring(0, 16);
    private async Task WriteAsync(string token, Grant grant, CancellationToken ct) { Directory.CreateDirectory(_grantRoot); await using var gate = await LockAsync(ct); Sweep(); var existing = Directory.EnumerateFiles(_grantRoot, "*.grant", SearchOption.TopDirectoryOnly).Select(path => new FileInfo(path)).ToArray(); var protectedGrant = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(grant), null, DataProtectionScope.CurrentUser); if (existing.Length >= 64 || existing.Sum(x => x.Length) + protectedGrant.Length > 1024 * 1024) throw new InvalidOperationException("Backup preview capacity is temporarily unavailable."); await File.WriteAllBytesAsync(PathFor(token), protectedGrant, ct); }
    private async Task<Grant> ConsumeAsync(string token, CancellationToken ct) { Directory.CreateDirectory(_grantRoot); await using var gate = await LockAsync(ct); var path = PathFor(token); var claimed = path + ".consumed." + Guid.NewGuid().ToString("N"); try { File.Move(path, claimed); var grant = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(await File.ReadAllBytesAsync(claimed, ct), null, DataProtectionScope.CurrentUser)) ?? throw new InvalidOperationException(); if (grant.ExpiresAt <= DateTimeOffset.UtcNow || !string.Equals(grant.Sid, CurrentSid(), StringComparison.Ordinal)) throw new InvalidOperationException(); return grant; } catch (Exception ex) when (ex is IOException or CryptographicException or JsonException) { throw new InvalidOperationException("A current backup preview is required."); } finally { try { File.Delete(claimed); } catch { } } }
    private async Task<FileStream> LockAsync(CancellationToken ct) { for (var i=0;;i++) try { return new FileStream(Path.Combine(_grantRoot, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); } catch(IOException) when(i<100) { await Task.Delay(20,ct); } }
    private string PathFor(string token) => Path.Combine(_grantRoot, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".grant");
    private void Sweep() { var cutoff = DateTime.UtcNow.AddMinutes(-10); foreach (var file in Directory.EnumerateFiles(_grantRoot, "*.consumed.*")) if (File.GetLastWriteTimeUtc(file) < cutoff) try { File.Delete(file); } catch { } foreach (var file in Directory.EnumerateFiles(_grantRoot, "*.grant")) { try { var grant = JsonSerializer.Deserialize<Grant>(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser)); if (grant is null || grant.ExpiresAt <= DateTimeOffset.UtcNow) File.Delete(file); } catch { try { File.Delete(file); } catch { } } } }
    private static string CurrentSid() => WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("A Windows user identity is required.");
    private sealed record Grant(string Sid, string InstanceName, int RetentionCount, string Operation, DateTimeOffset ExpiresAt, string? Frequency = null, TimeSpan? Time = null);
    private sealed record ScheduleEntry(string Id, string InstanceName, string Frequency, int RetentionCount, TimeSpan Time);
}
