using System.Text.Json;
using System.Text.Json.Nodes;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Core.Services;

/// <summary>Validates and removes only DistroNexus tag and fixed backup-schedule entries.</summary>
public sealed class LifecycleMetadataCleanup(string root, IProcessRunner? processes = null) : ILifecycleMetadataCleanup
{
    private readonly string _root = Path.GetFullPath(root);
    private readonly IProcessRunner? _processes = processes;
    public async Task CleanupRemovedInstanceAsync(string name, CancellationToken ct = default)
    {
        await RemoveTagAsync(name, ct); await RemoveSchedulesAsync(name, ct); if (_processes is not null) { var task = await _processes.RunAsync(new DistroNexus.Core.Models.ProcessRequest("schtasks.exe", ["/Delete", "/TN", TaskName(name), "/F"], TimeSpan.FromMinutes(1)), ct); if (task.ExitCode is not (0 or 1)) throw new InvalidOperationException("Lifecycle.ScheduleCleanupFailed"); }
    }
    private async Task RemoveTagAsync(string name, CancellationToken ct)
    {
        var path = Path.Combine(_root, "settings.json"); if (!File.Exists(path)) return;
        var doc = JsonNode.Parse(await File.ReadAllTextAsync(path, ct))?.AsObject() ?? throw new InvalidOperationException("Lifecycle.MetadataInvalid"); var settings = doc["value"] as JsonObject ?? doc;
        if (settings["instanceTags"] is JsonObject tags && tags.Remove(name)) await File.WriteAllTextAsync(path, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
    }
    private async Task RemoveSchedulesAsync(string name, CancellationToken ct)
    {
        var path = Path.Combine(_root, "backup-schedules.json"); if (!File.Exists(path)) return;
        var schedules = JsonNode.Parse(await File.ReadAllTextAsync(path, ct))?.AsArray() ?? throw new InvalidOperationException("Lifecycle.MetadataInvalid");
        foreach (var item in schedules.ToArray()) if (item?["InstanceName"]?.GetValue<string>() is { } value && string.Equals(value, name, StringComparison.OrdinalIgnoreCase)) schedules.Remove(item);
        await File.WriteAllTextAsync(path, schedules.ToJsonString(), ct);
    }
    private static string TaskName(string name) => "DistroNexus_Backup_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name))).Substring(0, 16);
}
