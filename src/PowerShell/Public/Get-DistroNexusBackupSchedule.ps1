function Get-DistroNexusBackupSchedule {
    <#
    .SYNOPSIS
        Returns backup schedule(s) from %APPDATA%\DistroNexus\backup-schedules.json.

    .PARAMETER Name
        Optional. When specified, returns only the schedule for that instance name.

    .EXAMPLE
        Get-DistroNexusBackupSchedule

    .EXAMPLE
        Get-DistroNexusBackupSchedule -Name "Ubuntu-22.04"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $schedules = Get-BackupScheduleEntries

        if ($PSBoundParameters.ContainsKey('Name') -and $Name) {
            $schedules = $schedules | Where-Object { $_.Name -eq $Name }
        }

        return $schedules
    }
}
