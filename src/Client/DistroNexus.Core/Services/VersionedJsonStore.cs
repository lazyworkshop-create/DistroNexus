using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class VersionedJsonStore<T> : IVersionedJsonStore<T>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path;
    private readonly int _schemaVersion;
    private readonly Func<JsonNode, T>? _legacyReader;
    private readonly IReadOnlyDictionary<int, Func<T, T>> _migrations;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private SemaphoreSlim Gate => Gates.GetOrAdd(_path, _ => new SemaphoreSlim(1, 1));

    public VersionedJsonStore(string path, int schemaVersion = 1, Func<JsonNode, T>? legacyReader = null,
        IReadOnlyDictionary<int, Func<T, T>>? migrations = null)
    {
        _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        _schemaVersion = schemaVersion > 0 ? schemaVersion : throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        _legacyReader = legacyReader;
        _migrations = migrations ?? new Dictionary<int, Func<T, T>>();
    }

    public async Task<StoreResult<VersionedDocument<T>>> ReadAsync(CancellationToken ct = default)
    { await Gate.WaitAsync(ct); try { return await ReadCoreAsync(ct); } finally { Gate.Release(); } }

    public async Task<StoreResult<VersionedDocument<T>>> WriteAsync(T value, long expectedRevision, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var current = await ReadCoreAsync(ct);
            if (current.Error is not (StoreErrorKind.None or StoreErrorKind.NotFound)) return current;
            var revision = current.Value?.Revision ?? 0;
            if (revision != expectedRevision)
                return StoreResult<VersionedDocument<T>>.Failure(StoreErrorKind.RevisionConflict, $"Expected revision {expectedRevision}, found {revision}.");
            var updatedAt = DateTimeOffset.UtcNow;
            var extension = current.Value?.ExtensionData?.DeepClone().AsObject();
            var document = new VersionedDocument<T>(_schemaVersion, revision + 1, updatedAt, value, extension);
            var root = extension ?? new JsonObject();
            root["schemaVersion"] = _schemaVersion; root["revision"] = document.Revision;
            root["updatedAt"] = updatedAt; root["value"] = JsonSerializer.SerializeToNode(value, _options);
            var directory = Path.GetDirectoryName(_path)!; Directory.CreateDirectory(directory);
            var temp = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            var backup = _path + ".bak";
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString(_options));
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                { await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct); stream.Flush(true); }
                if (File.Exists(_path)) File.Replace(temp, _path, backup, true); else File.Move(temp, _path);
                return StoreResult<VersionedDocument<T>>.Success(document);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return StoreResult<VersionedDocument<T>>.Failure(StoreErrorKind.IoFailure, ex.Message); }
            finally { try { File.Delete(temp); } catch (IOException) { } }
        }
        finally { Gate.Release(); }
    }

    private async Task<StoreResult<VersionedDocument<T>>> ReadCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return StoreResult<VersionedDocument<T>>.Failure(StoreErrorKind.NotFound, "Document does not exist.");
        var primary = await ParseAsync(_path, ct);
        if (primary.Error == StoreErrorKind.InvalidDocument && File.Exists(_path + ".bak"))
        {
            var recovered = await ParseAsync(_path + ".bak", ct);
            if (recovered.Succeeded) return recovered;
        }
        return primary;
    }

    private async Task<StoreResult<VersionedDocument<T>>> ParseAsync(string path, CancellationToken ct)
    {
        try
        {
            var rootNode = JsonNode.Parse(await File.ReadAllTextAsync(path, ct)) ?? throw new JsonException("Document is empty.");
            if (rootNode is not JsonObject root || root["schemaVersion"] is null)
            {
                if (_legacyReader is null) throw new JsonException("schemaVersion is required.");
                return StoreResult<VersionedDocument<T>>.Success(new(1, 0, DateTimeOffset.MinValue, _legacyReader(rootNode)));
            }
            var schema = root["schemaVersion"]!.GetValue<int>();
            if (schema > _schemaVersion) return StoreResult<VersionedDocument<T>>.Failure(StoreErrorKind.NewerSchema, $"Schema {schema} is newer than supported schema {_schemaVersion}.");
            var revision = root["revision"]?.GetValue<long>() ?? throw new JsonException("revision is required.");
            var updatedAt = root["updatedAt"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;
            var valueNode = root["value"] ?? throw new JsonException("value is required.");
            var value = JsonSerializer.Deserialize<T>(valueNode.ToJsonString(), _options) ?? throw new JsonException("value is required.");
            for (var version = schema; version < _schemaVersion; version++)
                value = _migrations.TryGetValue(version, out var migration) ? migration(value) : throw new JsonException($"No migration from schema {version}.");
            var extension = root.DeepClone().AsObject();
            extension.Remove("schemaVersion"); extension.Remove("revision"); extension.Remove("updatedAt"); extension.Remove("value");
            return StoreResult<VersionedDocument<T>>.Success(new(schema, revision, updatedAt, value, extension));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { return StoreResult<VersionedDocument<T>>.Failure(StoreErrorKind.InvalidDocument, ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return StoreResult<VersionedDocument<T>>.Failure(StoreErrorKind.IoFailure, ex.Message); }
    }
}
