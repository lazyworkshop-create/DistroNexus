using System.Text.Json;
using System.Text.Json.Nodes;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Manages per-instance tags in %APPDATA%\DistroNexus\settings.json under the "instanceTags" key.
/// </summary>
public class TagService : ITagService
{
    private readonly ILogger<TagService> _logger;
    private readonly string _appDataDir;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public TagService(ILogger<TagService> logger, string? appDataDir = null)
    {
        _logger     = logger ?? throw new ArgumentNullException(nameof(logger));
        _appDataDir = appDataDir
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DistroNexus");
    }

    private string SettingsFilePath => Path.Combine(_appDataDir, "settings.json");

    // ── ITagService ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<string>> GetTagsAsync(string instanceName, CancellationToken ct = default)
    {
        var map = await ReadTagMapAsync(ct);
        return map.TryGetValue(instanceName, out var tags) ? tags : [];
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAllTagsAsync(CancellationToken ct = default)
    {
        var map = await ReadTagMapAsync(ct);
        return map.Values.SelectMany(t => t).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc/>
    public async Task SetTagsAsync(string instanceName, IEnumerable<string> tags, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        var tagList = tags.Select(t => t.ToLowerInvariant().Trim()).Distinct().ToList();
        if (tagList.Count > 10)
            throw new WslOperationFailedException(
                $"Maximum 10 tags allowed per instance. Got {tagList.Count}.",
                DistroNexusErrorCode.TooManyTags,
                operation: "SetTags",
                instanceName: instanceName);

        var map = await ReadTagMapAsync(ct);
        map[instanceName] = tagList;
        await WriteTagMapAsync(map, ct);
        _logger.LogDebug("Set {Count} tags for instance '{Name}'", tagList.Count, instanceName);
    }

    /// <inheritdoc/>
    public async Task AddTagAsync(string instanceName, string tag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Length > 32)
            throw new WslOperationFailedException(
                $"Tag must not exceed 32 characters. Provided: {tag.Length}.",
                DistroNexusErrorCode.TooManyTags,
                operation: "AddTag",
                instanceName: instanceName);

        var normalised = tag.ToLowerInvariant().Trim();
        var map        = await ReadTagMapAsync(ct);

        if (!map.TryGetValue(instanceName, out var existing))
            existing = [];

        if (existing.Count >= 10)
            throw new WslOperationFailedException(
                $"Instance '{instanceName}' already has 10 tags (maximum). Remove a tag before adding a new one.",
                DistroNexusErrorCode.TooManyTags,
                operation: "AddTag",
                instanceName: instanceName);

        if (!existing.Contains(normalised, StringComparer.OrdinalIgnoreCase))
        {
            existing.Add(normalised);
            map[instanceName] = existing;
            await WriteTagMapAsync(map, ct);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveTagAsync(string instanceName, string tag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);
        ArgumentNullException.ThrowIfNull(tag);

        var normalised = tag.ToLowerInvariant().Trim();
        var map        = await ReadTagMapAsync(ct);

        if (map.TryGetValue(instanceName, out var existing))
        {
            existing.RemoveAll(t => string.Equals(t, normalised, StringComparison.OrdinalIgnoreCase));
            map[instanceName] = existing;
            await WriteTagMapAsync(map, ct);
        }
    }

    /// <inheritdoc/>
    public async Task RenameInstanceTagsAsync(string oldName, string newName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);

        var map = await ReadTagMapAsync(ct);
        if (map.TryGetValue(oldName, out var tags))
        {
            map.Remove(oldName);
            map[newName] = tags;
            await WriteTagMapAsync(map, ct);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteInstanceTagsAsync(string instanceName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        var map = await ReadTagMapAsync(ct);
        if (map.Remove(instanceName))
            await WriteTagMapAsync(map, ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<Dictionary<string, List<string>>> ReadTagMapAsync(CancellationToken ct)
    {
        if (!File.Exists(SettingsFilePath))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json    = await File.ReadAllTextAsync(SettingsFilePath, ct);
            var root = JsonNode.Parse(json)?.AsObject();
            // v2.2.1 stored settings as a plain object.  v2.3 can persist the same
            // document in a VersionedJsonStore envelope, whose user settings live under
            // `value`.  Read both shapes so tag data survives an in-place upgrade.
            var settings = root?["value"] as JsonObject ?? root;
            var tagsNode = settings?["instanceTags"];

            if (tagsNode is null)
                return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in tagsNode.AsObject())
            {
                var tagArray = prop.Value?.AsArray()
                    .Select(v => v?.GetValue<string>() ?? string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList() ?? [];
                result[prop.Key] = tagArray;
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read tag map from {Path}", SettingsFilePath);
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task WriteTagMapAsync(Dictionary<string, List<string>> map, CancellationToken ct)
    {
        Directory.CreateDirectory(_appDataDir);

        // Load existing settings to avoid overwriting other keys
        JsonObject root;
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var existing = await File.ReadAllTextAsync(SettingsFilePath, ct);
                root = JsonNode.Parse(existing)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        var tagsObj = new JsonObject();
        foreach (var kv in map)
        {
            var arr = new JsonArray();
            foreach (var tag in kv.Value)
                arr.Add(JsonValue.Create(tag));
            tagsObj[kv.Key] = arr;
        }

        // Keep tags alongside the settings payload when an envelope already exists. This
        // preserves schema/revision/extension fields rather than accidentally flattening a
        // v2.3 document back to the legacy shape.
        var settings = root["value"] as JsonObject ?? root;
        settings["instanceTags"] = tagsObj;

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(SettingsFilePath, json, ct);
    }
}
