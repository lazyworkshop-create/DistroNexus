function New-DistroNexusBackupSchedule {
    <#
    .SYNOPSIS
        Creates a Windows Task Scheduler task for automated instance backups.

    .DESCRIPTION
        Registers a recurring backup schedule for a WSL instance via Windows Task Scheduler.
        The task invokes Invoke-DistroNexusBackup on the configured frequency.
        Schedule metadata is persisted to %APPDATA%\DistroNexus\backup-schedules.json.

    .PARAMETER Name
        The WSL instance name to schedule backups for.

    .PARAMETER Destination
        Destination directory for backup TAR files.

    .PARAMETER Frequency
        Backup frequency: Daily | Weekly:<DayOfWeek> | Monthly:<DayNumber>
        Examples: "Daily", "Weekly:Monday", "Monthly:1"

    .PARAMETER RetentionCount
        Number of backup files to keep (1-30). Older files are deleted after each backup.

    .PARAMETER Time
        Time of day to run the backup task (as a TimeSpan, e.g. [TimeSpan]::new(2,0,0) for 02:00).

    .EXAMPLE
        New-DistroNexusBackupSchedule -Name "Ubuntu-22.04" -Destination "D:\Backups" `
            -Frequency "Daily" -RetentionCount 7 -Time ([TimeSpan]::new(2,0,0))

    .EXAMPLE
        New-DistroNexusBackupSchedule -Name "Debian" -Destination "D:\Backups" `
            -Frequency "Weekly:Monday" -RetentionCount 4 -Time ([TimeSpan]::new(3,0,0))
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Destination,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Frequency,

        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 30)]
        [int]$RetentionCount,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Time
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        Write-DistroNexusLog "New-DistroNexusBackupSchedule: configuring schedule for '$Name'"

        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found." -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        # Ensure destination directory exists
        if (-not (Test-Path $Destination)) {
            New-Item -Path $Destination -ItemType Directory -Force | Out-Null
            Write-DistroNexusLog "Created backup destination directory: $Destination"
        }

        # Build Task Scheduler trigger
        $taskName = "DistroNexus_Backup_$Name"
        $runAt = [DateTime]::Today.Add($Time)

        $trigger = switch -Regex ($Frequency) {
            '^Daily$' {
                New-ScheduledTaskTrigger -Daily -At $runAt
            }
            '^Weekly:(.+)$' {
                $dayOfWeek = $Matches[1]
                New-ScheduledTaskTrigger -Weekly -DaysOfWeek $dayOfWeek -At $runAt
            }
            '^Monthly:(\d+)$' {
                $dayOfMonth = [int]$Matches[1]
                New-ScheduledTaskTrigger -Monthly -DaysOfMonth $dayOfMonth -At $runAt
            }
            default {
                Write-Error "Invalid Frequency format '$Frequency'. Use: Daily | Weekly:<DayOfWeek> | Monthly:<DayNumber>" `
                    -ErrorId "DistroNexus.InvalidFrequency"
                return
            }
        }

        # Build task action
        $modulePath   = $MyInvocation.MyCommand.Module.ModuleBase
        $runnerScript = Join-Path $modulePath "backup-runner.ps1"

        # Discover PowerShell executable — prefer pwsh (PS 7+), fall back to powershell (Windows PS 5.1)
        $psExe = if (Get-Command pwsh.exe -ErrorAction SilentlyContinue) { 'pwsh.exe' } else { 'powershell.exe' }

        $action = New-ScheduledTaskAction `
            -Execute $psExe `
            -Argument "-NonInteractive -File `"$runnerScript`" -InstanceName `"$Name`" -Destination `"$Destination`" -RetentionCount $RetentionCount"

        $settings = New-ScheduledTaskSettingsSet `
            -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
            -StartWhenAvailable

        # Register (overwrite if already exists)
        $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if ($existingTask) {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
        }

        Register-ScheduledTask `
            -TaskName    $taskName `
            -Action      $action `
            -Trigger     $trigger `
            -Settings    $settings `
            -Description "DistroNexus automated backup for WSL instance '$Name'" `
            -ErrorAction Stop | Out-Null

        Write-DistroNexusLog "Registered Task Scheduler task: $taskName"

        # Persist schedule metadata
        $scheduleData = [PSCustomObject]@{
            PSTypeName     = 'DistroNexus.BackupSchedule'
            Name           = $Name
            Destination    = $Destination
            Frequency      = $Frequency
            RetentionCount = $RetentionCount
            Time           = $Time.ToString()
            TaskName       = $taskName
            CreatedAt      = (Get-Date -Format 'o')
        }

        Save-BackupScheduleEntry -Schedule $scheduleData

        Write-DistroNexusLog "Backup schedule saved for '$Name' (frequency=$Frequency, retention=$RetentionCount)"

        return $scheduleData
    }
}

# ── Module-private helpers (backup-schedules.json I/O) ────────────────────────

function Get-BackupScheduleFilePath {
    return Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
}

function Get-BackupScheduleEntries {
    $filePath = Get-BackupScheduleFilePath
    if (-not (Test-Path $filePath)) { return @() }
    try {
        $content = Get-Content $filePath -Raw -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace($content)) { return @() }
        $raw = $content | ConvertFrom-Json
        return @($raw)
    }
    catch {
        return @()
    }
}

function Save-BackupScheduleEntry {
    param([Parameter(Mandatory=$true)] [PSCustomObject]$Schedule)
    $existing = @(Get-BackupScheduleEntries | Where-Object { $_.Name -ne $Schedule.Name })
    Save-AllBackupScheduleEntries -Schedules (@($existing) + @($Schedule))
}

function Save-AllBackupScheduleEntries {
    param([Parameter(Mandatory=$false)] $Schedules = @())
    $filePath = Get-BackupScheduleFilePath
    $dir = Split-Path $filePath -Parent
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
    @($Schedules) | ConvertTo-Json -Depth 5 | Set-Content $filePath -Encoding UTF8
}
