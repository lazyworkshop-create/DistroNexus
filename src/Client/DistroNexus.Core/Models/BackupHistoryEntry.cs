namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a single backup file entry in the backup history list.
/// </summary>
public class BackupHistoryEntry
{
    public string Kind { get; set; } = "ScheduledBackup";
    /// <summary>File creation timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Full path to the backup file.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Error message for failed backup entries.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Whether the backup is considered successful. Always true for entries found on disk.</summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>Human-readable file name (without directory).</summary>
    public string FileName => System.IO.Path.GetFileName(FilePath);

    /// <summary>Text displayed in the backup history list.</summary>
    public string DisplayName => IsSuccess ? FileName : ErrorMessage;
}
