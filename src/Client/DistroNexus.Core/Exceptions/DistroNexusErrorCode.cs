namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Structured error codes for all DistroNexus operations.
/// Assign stable numeric prefixes by category:
///   1xxx = instance lifecycle
///   2xxx = disk / VHDX
///   3xxx = Docker integration
///   4xxx = backup / export / import
///   5xxx = configuration
///   9xxx = system / unknown
/// </summary>
public enum DistroNexusErrorCode
{
    // ── Instance lifecycle ────────────────────────────────────────────────
    InstanceNotFound      = 1001,
    InstanceAlreadyRunning = 1002,
    InstanceAlreadyStopped = 1003,
    InstanceAlreadyExists  = 1004,

    // ── Disk / VHDX ───────────────────────────────────────────────────────
    VhdxNotFound      = 2001,
    VhdxAccessDenied  = 2002,
    CompactionFailed  = 2003,

    // ── Docker integration ────────────────────────────────────────────────
    DockerDesktopNotFound      = 3001,
    DockerConfigWriteConflict  = 3002,

    // ── Backup / Export / Import ──────────────────────────────────────────
    ExportFailed           = 4001,
    ImportFailed           = 4002,
    BackupDestinationFull  = 4003,
    ScheduleCreateFailed   = 4004,

    // ── Configuration ─────────────────────────────────────────────────────
    WslConfigReadFailed   = 5001,
    WslConfigWriteFailed  = 5002,
    RegistryAccessDenied  = 5003,

    // ── System / Unknown ──────────────────────────────────────────────────
    WslNotInstalled  = 9001,
    WslVersionTooLow = 9002,
    OperationTimeout = 9003,
    UnknownError     = 9999,
}
