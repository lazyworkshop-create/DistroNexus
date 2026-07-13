namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Structured error codes for all DistroNexus operations.
/// Assign stable numeric prefixes by category:
///   1xxx = instance lifecycle
///   2xxx = disk / VHDX
///   3xxx = Docker integration
///   4xxx = backup / export / import
///   5xxx = configuration
///   6xxx = templates
///   9xxx = system / unknown
/// </summary>
public enum DistroNexusErrorCode
{
    // ── Instance lifecycle ────────────────────────────────────────────────
    InstanceNotFound       = 1001,
    InstanceAlreadyRunning = 1002,
    InstanceAlreadyStopped = 1003,
    InstanceAlreadyExists  = 1004,
    TooManyTags            = 1005,
    StartFailed            = 1006,
    StopFailed             = 1007,
    RemoveFailed           = 1008,
    RenameFailed           = 1009,

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
    ScheduleNotFound       = 4005,
    BackupFailed           = 4006,
    InvalidFrequency       = 4007,
    InstallFailed          = 4008,
    RecoveryPointInvalid   = 4009,
    RecoveryTargetReserved = 4010,
    RecoveryOperationFailed = 4011,
    RecoveryManualRecoveryRequired = 4012,

    // ── Configuration ─────────────────────────────────────────────────────
    WslConfigReadFailed   = 5001,
    WslConfigWriteFailed  = 5002,
    RegistryAccessDenied  = 5003,

    // ── Templates ─────────────────────────────────────────────────────────
    TemplateNotFound       = 6001,
    TemplateScriptFailed   = 6002,

    // ── Capability / Health / Monitoring ─────────────────────────────────
    HealthCheckUnavailable = 7001,
    HealthRepairPreviewInvalid = 7002,
    HealthRepairConfirmationRequired = 7003,
    HealthRepairElevationRequired = 7004,
    HealthRepairFailed = 7005,
    HealthRepairPostconditionFailed = 7006,
    DiagnosticExportInvalid = 7007,

    // ── systemd / networking / firewall ─────────────────────────────────
    SystemdUnavailable = 8001,
    LinuxPrivilegeRequired = 8002,
    FirewallElevationRequired = 8003,
    NetworkProbeFailed = 8004,
    FirewallOwnershipDenied = 8005,

    // ── System / Unknown ──────────────────────────────────────────────────
    WslNotInstalled             = 9001,
    WslVersionTooLow            = 9002,
    OperationTimeout            = 9003,
    PowerShellModuleUnavailable = 9004,
    ProcessStartFailed           = 9101,
    ProcessOutputLimitExceeded   = 9102,
    StoreRevisionConflict        = 9201,
    StoreSchemaUnsupported       = 9202,
    StoreDocumentInvalid         = 9203,
    StoreWriteFailed             = 9204,
    ValidationFailed             = 9301,
    UnknownError                = 9999,
}
