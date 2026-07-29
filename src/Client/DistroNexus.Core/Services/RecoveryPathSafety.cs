using System.Text.Json;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Last-moment ownership checks for recovery-point destructive operations.</summary>
public static class RecoveryPathSafety
{
    public static bool IsNoReparsePointInExistingPath(string path)
    {
        try
        {
            for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
                if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool IsOwnedPointDirectory(string directory, RecoveryPointManifest manifest)
    {
        try
        {
            var full = Path.GetFullPath(directory);
            var expected = "DistroNexusRecovery-" + manifest.Id.ToString("N");
            var marker = new FileInfo(Path.Combine(full, "manifest.json"));
            return Directory.Exists(full) && IsNoReparsePointInExistingPath(full)
                && StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(full), expected)
                && Path.GetFileName(manifest.PayloadFile) == manifest.PayloadFile
                && !manifest.PayloadFile.Contains("..", StringComparison.Ordinal)
                && marker.Exists && !marker.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static void DeleteOwnedPoint(RecoveryPointSummary point)
    {
        // Re-check immediately before Directory.Delete: a path validated during listing can be
        // replaced by a junction before a user confirms deletion or retention runs.
        if (!IsOwnedPointDirectory(point.DirectoryPath, point.Manifest))
            throw new InvalidOperationException("Recovery point ownership or path safety could not be revalidated.");
        Directory.Delete(Path.GetFullPath(point.DirectoryPath), true);
    }

    internal static void DeleteOwnedCreateDirectory(string directory, Guid id)
    {
        var full = Path.GetFullPath(directory);
        if (!Directory.Exists(full) || !IsNoReparsePointInExistingPath(full)
            || !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(full), "DistroNexusRecovery-" + id.ToString("N"))) return;
        Directory.Delete(full, true);
    }

    internal static void DeleteOperationEvidence(string operationId, string instanceName, string targetDirectory, string journal)
    {
        try
        {
            var target = Path.GetFullPath(targetDirectory);
            var parent = Path.GetDirectoryName(target);
            var expectedMarker = Path.Combine(parent ?? throw new IOException("Recovery target has no parent."), $".distronexus-recovery-{operationId}.json");
            var fullJournal = Path.GetFullPath(journal);
            var marker = new FileInfo(expectedMarker); var journalInfo = new FileInfo(fullJournal);
            if (!marker.Exists || marker.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || !journalInfo.Exists || journalInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || !IsNoReparsePointInExistingPath(Path.GetDirectoryName(fullJournal)!)
                || !MatchesOperationEvidence(expectedMarker, operationId, instanceName, target)
                || !MatchesOperationEvidence(fullJournal, operationId, instanceName, target)) return;
            File.Delete(expectedMarker);
            File.Delete(fullJournal);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool MatchesOperationEvidence(string path, string operationId, string instanceName, string targetDirectory)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return Get(root, "operationId") == operationId
                && StringComparer.OrdinalIgnoreCase.Equals(Get(root, "targetInstance"), instanceName)
                && StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(Get(root, "targetDirectory")!), targetDirectory);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }

        static string? Get(JsonElement root, string name) => root.EnumerateObject()
            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Value.GetString();
    }

    internal static bool IsSafeOperationId(string value) => value.Length == 32 && value.All(Uri.IsHexDigit);
}
