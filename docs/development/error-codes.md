# DistroNexus Error Code Reference

All structured error codes used by DistroNexus are defined in `DistroNexusErrorCode` (C#).
PowerShell bridges structured failures using `DistroNexus.<CodeName>` error IDs where an operation
maps to a PowerShell error record; not every Core code is necessarily emitted by every host.

## Code Ranges

| Range  | Category               |
|--------|------------------------|
| 1xxx   | Instance lifecycle     |
| 2xxx   | Disk / VHDX            |
| 3xxx   | Docker integration     |
| 4xxx   | Backup / Export / Import |
| 5xxx   | Configuration          |
| 6xxx   | Templates              |
| 7xxx   | Health / diagnostics    |
| 8xxx   | systemd / networking    |
| 9xxx   | System / Unknown       |

---

## 1xxx — Instance Lifecycle

| Code | Name                    | When thrown |
|------|-------------------------|-------------|
| 1001 | `InstanceNotFound`      | A requested WSL instance does not exist |
| 1002 | `InstanceAlreadyRunning`| Start attempted on a running instance |
| 1003 | `InstanceAlreadyStopped`| Stop attempted on an already-stopped instance |
| 1004 | `InstanceAlreadyExists` | Import or create attempted with a name that already exists |
| 1005 | `TooManyTags`           | Tag limit exceeded; an instance cannot have more than the allowed number of tags |
| 1006 | `StartFailed`           | Starting a WSL instance failed |
| 1007 | `StopFailed`            | Stopping a WSL instance failed |
| 1008 | `RemoveFailed`          | Removing/unregistering a WSL instance failed |
| 1009 | `RenameFailed`          | Renaming a WSL instance failed |

---

## 2xxx — Disk / VHDX

| Code | Name               | When thrown |
|------|--------------------|-------------|
| 2001 | `VhdxNotFound`     | VHDX file path resolved from registry but file is missing |
| 2002 | `VhdxAccessDenied` | Access denied reading or writing the VHDX |
| 2003 | `CompactionFailed` | `Optimize-VHD` and diskpart fallback both failed |

---

## 3xxx — Docker Integration

| Code | Name                      | When thrown |
|------|---------------------------|-------------|
| 3001 | `DockerDesktopNotFound`   | Docker Desktop is not installed |
| 3002 | `DockerConfigWriteConflict` | Docker settings JSON could not be written (locked / schema mismatch) |

---

## 4xxx — Backup / Export / Import

| Code | Name                    | When thrown |
|------|-------------------------|-------------|
| 4001 | `ExportFailed`          | `wsl --export` returned a non-zero exit code |
| 4002 | `ImportFailed`          | `wsl --import` returned a non-zero exit code |
| 4003 | `BackupDestinationFull` | Destination drive has insufficient free space |
| 4004 | `ScheduleCreateFailed`  | `Register-ScheduledTask` failed to create the backup task |
| 4005 | `ScheduleNotFound`      | Referenced backup schedule task does not exist in Task Scheduler |
| 4006 | `BackupFailed`          | Backup invocation failed after validation, including export or retention failures |
| 4007 | `InvalidFrequency`      | Backup schedule frequency format is invalid |
| 4008 | `InstallFailed`         | WSL instance installation failed |
| 4009 | `RecoveryPointInvalid` | A recovery point is malformed, unsafe, or cannot be verified |
| 4010 | `RecoveryTargetReserved` | A recovery restore target is already reserved or exists |
| 4011 | `RecoveryOperationFailed` | A recovery create, restore, or remove operation failed |
| 4012 | `RecoveryManualRecoveryRequired` | Recovery detected a partial operation that requires documented manual recovery |

---

## 5xxx — Configuration

| Code | Name                  | When thrown |
|------|-----------------------|-------------|
| 5001 | `WslConfigReadFailed` | `~\.wslconfig` could not be read or parsed |
| 5002 | `WslConfigWriteFailed`| `~\.wslconfig` could not be written |
| 5003 | `RegistryAccessDenied`| Registry key for WSL/instance metadata could not be opened |

---

## 6xxx — Templates

| Code | Name                  | When thrown |
|------|-----------------------|-------------|
| 6001 | `TemplateNotFound`    | Requested template ID does not exist in the template catalog |
| 6002 | `TemplateScriptFailed`| The template's post-install script exited with a non-zero code or threw an exception |
| 6003 | `TemplateManifestInvalid` | A remote template manifest is invalid or unsupported |
| 6004 | `TemplateArtifactIntegrityFailed` | Downloaded artifact hash verification failed |
| 6005 | `TemplateTrustRequired` | A remote template has not completed explicit review/trust |
| 6006 | `TemplateArtifactUnsafe` | Archive, path, size, link, or executable-file validation rejected an artifact |

---

## 7xxx — Health and Diagnostics

| Code | Name | When thrown |
|------|------|-------------|
| 7001 | `HealthCheckUnavailable` | A check prerequisite is unavailable |
| 7002 | `HealthRepairPreviewInvalid` | A repair preview is stale or invalid |
| 7003 | `HealthRepairConfirmationRequired` | A repair requires explicit confirmation |
| 7004 | `HealthRepairElevationRequired` | A repair requires a reviewed elevation flow |
| 7005 | `HealthRepairFailed` | A repair did not complete |
| 7006 | `HealthRepairPostconditionFailed` | A repair completed but did not meet its verification condition |
| 7007 | `DiagnosticExportInvalid` | A diagnostic export request failed validation |

---

## 8xxx — systemd and Networking

| Code | Name | When thrown |
|------|------|-------------|
| 8001 | `SystemdUnavailable` | systemd is not available for the selected instance |
| 8002 | `LinuxPrivilegeRequired` | A Linux-side operation needs privilege not available to the request |
| 8003 | `FirewallElevationRequired` | A Windows firewall operation needs the bounded elevation path |
| 8004 | `NetworkProbeFailed` | A read-only network probe failed |
| 8005 | `FirewallOwnershipDenied` | The requested firewall rule is not owned by DistroNexus |

---

## 9xxx — System / Unknown

| Code | Name              | When thrown |
|------|-------------------|-------------|
| 9001 | `WslNotInstalled` | `wsl.exe` is not found on `PATH` |
| 9002 | `WslVersionTooLow`| Installed WSL version is below the minimum required |
| 9003 | `OperationTimeout`            | A WSL operation exceeded its timeout; thrown by `WslOperationTimeoutException` |
| 9004 | `PowerShellModuleUnavailable` | PowerShell execution failed, or the DistroNexus PowerShell module could not be loaded/resolved |
| 9101 | `ProcessStartFailed` | A reviewed local process could not be started |
| 9102 | `ProcessOutputLimitExceeded` | Process output exceeded the bounded capture limit |
| 9201 | `StoreRevisionConflict` | A persisted document changed after the caller's expected revision |
| 9202 | `StoreSchemaUnsupported` | A persisted document uses a newer unsupported schema |
| 9203 | `StoreDocumentInvalid` | A persisted document could not be parsed or validated |
| 9204 | `StoreWriteFailed` | A persisted document could not be written atomically |
| 9301 | `ValidationFailed` | Input or persisted data failed a required validation rule |
| 9999 | `UnknownError`                | Catch-all for unexpected exceptions; check inner exception |

---

## Usage in C#

```csharp
throw new WslException("Instance not found", DistroNexusErrorCode.InstanceNotFound);
// or via typed subclass:
throw new WslInstanceNotFoundException("Ubuntu-22.04");
// ex.Code == DistroNexusErrorCode.InstanceNotFound (1001)
```

## Usage in PowerShell

```powershell
Write-Error -Message "Instance not found" `
            -ErrorId "DistroNexus.InstanceNotFound" `
            -Category ObjectNotFound
```

## Fully Qualified Error ID Format

```
DistroNexus.<CodeName>
```

Example: `DistroNexus.InstanceNotFound`
