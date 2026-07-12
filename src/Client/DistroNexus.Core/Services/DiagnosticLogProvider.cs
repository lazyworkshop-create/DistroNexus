using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using System.Text.Json;

namespace DistroNexus.Core.Services;

/// <summary>Deliberately exposes no arbitrary file path reader. Hosts may register a selected, bounded provider.</summary>
public sealed class EmptyDiagnosticLogProvider : IDiagnosticLogProvider
{
    public IReadOnlyCollection<string> AllowedLogIds { get; } = [];
    public Task<string> ReadAsync(string logId, int maximumCharacters, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("DN-9301: Diagnostic log is not allow-listed.");
}

/// <summary>Reads only preconfigured application logs and applies a strict bounded read before redaction.</summary>
public sealed class AllowListedDiagnosticLogProvider : IDiagnosticLogProvider
{
    private readonly IReadOnlyDictionary<string, string> _logs;
    public AllowListedDiagnosticLogProvider(IReadOnlyDictionary<string, string> logs) => _logs = logs.ToDictionary(x => x.Key, x => Path.GetFullPath(x.Value), StringComparer.Ordinal);
    public IReadOnlyCollection<string> AllowedLogIds => _logs.Keys.ToArray();
    public async Task<string> ReadAsync(string logId, int maximumCharacters, CancellationToken cancellationToken = default)
    {
        if (!_logs.TryGetValue(logId, out var path) || maximumCharacters is < 1 or > 64 * 1024) throw new InvalidOperationException("DN-9301: Diagnostic log is not allow-listed or requested size is invalid.");
        if (!File.Exists(path)) return "Log is not currently available.";
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        var buffer = new char[maximumCharacters]; var read = await reader.ReadAsync(buffer.AsMemory(0, maximumCharacters), cancellationToken).ConfigureAwait(false);
        return SensitiveDataRedactor.RedactSecrets(new string(buffer, 0, read));
    }
}

/// <summary>
/// Exposes only DistroNexus's own NLog files from the configured log directory. File names are
/// converted to opaque ids and the directory is enumerated afresh for every request, so a caller
/// cannot use this provider to read an arbitrary local path.
/// </summary>
public sealed class ApplicationDiagnosticLogProvider : IDiagnosticLogProvider
{
    private const int MaximumLogFiles = 8;
    private readonly ISettingsService _settings;
    public ApplicationDiagnosticLogProvider(ISettingsService settings) => _settings = settings;

    public IReadOnlyCollection<string> AllowedLogIds => Files().Keys.Order(StringComparer.Ordinal).ToArray();

    public async Task<string> ReadAsync(string logId, int maximumCharacters, CancellationToken cancellationToken = default)
    {
        if (maximumCharacters is < 1 or > 64 * 1024 || !Files().TryGetValue(logId, out var path))
            throw new InvalidOperationException("DN-9301: Diagnostic log is not allow-listed or requested size is invalid.");
        if (IsReparsePoint(path)) throw new InvalidOperationException("DN-9301: Diagnostic log is not a regular allow-listed file.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        // Reports need recent diagnostics. Seek conservatively by bytes (UTF-8 may use up to four
        // bytes per character), then discard the partial first line before applying the character cap.
        var start = Math.Max(0, stream.Length - (long)maximumCharacters * 4);
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        if (start != 0) await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new char[maximumCharacters];
        var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        return SensitiveDataRedactor.Redact(new string(buffer, 0, count));
    }

    internal IReadOnlyDictionary<string, string> Files()
    {
        var directory = _settings.LoadSettings().LogPath;
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus", "Logs");
        try
        {
            var root = Path.GetFullPath(directory);
            if (!Directory.Exists(root) || IsReparsePoint(root)) return new Dictionary<string, string>(StringComparer.Ordinal);
            var rootedPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            return Directory.EnumerateFiles(root, "DistroNexus*.log", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath)
                .Where(path => path.StartsWith(rootedPrefix, StringComparison.OrdinalIgnoreCase) && !IsReparsePoint(path))
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(MaximumLogFiles)
                .ToDictionary(path => "app:" + Path.GetFileNameWithoutExtension(path), path => path, StringComparer.Ordinal);
        }
        catch (IOException) { return new Dictionary<string, string>(StringComparer.Ordinal); }
        catch (UnauthorizedAccessException) { return new Dictionary<string, string>(StringComparer.Ordinal); }
    }
    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }
}

/// <summary>Creates a bounded typed error projection from DistroNexus's JSON NLog records.</summary>
public sealed class StructuredFileErrorProvider : IStructuredErrorProvider
{
    private readonly ApplicationDiagnosticLogProvider _logs;
    public StructuredFileErrorProvider(ApplicationDiagnosticLogProvider logs) => _logs = logs;

    public async Task<IReadOnlyList<StructuredErrorRecord>> GetRecentAsync(int maximumEntries, CancellationToken cancellationToken = default)
    {
        if (maximumEntries is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        var records = new List<StructuredErrorRecord>();
        foreach (var id in _logs.AllowedLogIds)
        {
            var content = await _logs.ReadAsync(id, 64 * 1024, cancellationToken).ConfigureAwait(false);
            foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParse(line, out var record)) records.Add(record);
            }
        }
        return records.OrderByDescending(x => x.OccurredAt).Take(maximumEntries).ToArray();
    }

    private static bool TryParse(string line, out StructuredErrorRecord record)
    {
        record = default!;
        try
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            var level = Text(root, "level");
            var code = Text(root, "errorCode");
            var exception = Text(root, "exception");
            if (!string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(exception)) return false;
            if (string.IsNullOrWhiteSpace(code)) code = "DN-9999";
            if (!code.StartsWith("DN-", StringComparison.OrdinalIgnoreCase)) code = "DN-" + code;
            var time = DateTimeOffset.TryParse(Text(root, "time"), out var parsed) ? parsed : DateTimeOffset.MinValue;
            var message = Text(root, "message");
            if (!string.IsNullOrWhiteSpace(exception)) message = string.IsNullOrWhiteSpace(message) ? exception : message + " | " + exception;
            record = new StructuredErrorRecord(time, code, Text(root, "logger"), SensitiveDataRedactor.Redact(message));
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static string Text(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
