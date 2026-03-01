function Remove-DistroNexusBackupSchedule {
    <#
    .SYNOPSIS
        Removes the Windows Task Scheduler task and schedule entry for an instance backup.

    .PARAMETER Name
        The WSL instance name whose backup schedule to remove.

    .EXAMPLE
        Remove-DistroNexusBackupSchedule -Name "Ubuntu-22.04"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        Write-DistroNexusLog "Remove-DistroNexusBackupSchedule: removing schedule for '$Name'"

        $schedules = Get-BackupScheduleEntries
        $existing  = $schedules | Where-Object { $_.Name -eq $Name }

        if (-not $existing) {
            Write-Error "No backup schedule found for instance '$Name'." -ErrorId "DistroNexus.ScheduleNotFound"
            return
        }

        $taskName = "DistroNexus_Backup_$Name"
        $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if ($task) {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
            Write-DistroNexusLog "Unregistered Task Scheduler task: $taskName"
        }

        # Remove entry from JSON
        $updated = $schedules | Where-Object { $_.Name -ne $Name }
        Save-AllBackupScheduleEntries -Schedules $updated

        Write-DistroNexusLog "Backup schedule removed for '$Name'"
    }
}
