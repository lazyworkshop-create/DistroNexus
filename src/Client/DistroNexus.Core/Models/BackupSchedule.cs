namespace DistroNexus.Core.Models;

/// <summary>
/// Represents a persisted backup schedule for a WSL instance.
/// </summary>
public class BackupSchedule
{
    /// <summary>The WSL instance name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Destination directory for backup TAR files.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// Backup frequency string: "Daily" | "Weekly:Monday" | "Monthly:1"
    /// </summary>
    public string Frequency { get; set; } = "Daily";

    /// <summary>Number of backup files to retain (1-30).</summary>
    public int RetentionCount { get; set; } = 7;

    /// <summary>Time of day to run the backup.</summary>
    public TimeSpan Time { get; set; } = new TimeSpan(2, 0, 0);

    /// <summary>The Task Scheduler task name.</summary>
    public string TaskName => $"DistroNexus_Backup_{Name}";

    /// <summary>When the schedule was created.</summary>
    public DateTimeOffset? CreatedAt { get; set; }
}
