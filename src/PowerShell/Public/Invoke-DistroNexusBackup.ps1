function Invoke-DistroNexusBackup {
    <#
    .SYNOPSIS
        Performs an on-demand backup of a WSL instance.

    .DESCRIPTION
        Stops the instance (if running), exports it to a timestamped TAR file, restarts it,
        then enforces the retention policy by deleting the oldest files beyond RetentionCount.

    .PARAMETER Name
        The WSL instance name to back up.

    .PARAMETER Destination
        The directory where the backup TAR file will be written.

    .PARAMETER RetentionCount
        Maximum number of backup files to retain in the destination directory (1-30).

    .EXAMPLE
        Invoke-DistroNexusBackup -Name "Ubuntu-22.04" -Destination "D:\Backups" -RetentionCount 7
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
        [ValidateRange(1, 30)]
        [int]$RetentionCount
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        Write-DistroNexusLog "Invoke-DistroNexusBackup: starting backup for '$Name'"

        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found." -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        # Ensure destination exists
        if (-not (Test-Path $Destination)) {
            New-Item -Path $Destination -ItemType Directory -Force | Out-Null
        }

        # Lifecycle: stop if running
        $wasRunning = $instance.State -eq "Running"
        if ($wasRunning) {
            Write-DistroNexusLog "Stopping instance '$Name' before backup"
            Stop-DistroNexusInstance -Name $Name | Out-Null
        }

        try {
            # Build timestamped filename
            $timestamp  = (Get-Date).ToString("yyyyMMdd-HHmmss")
            $backupFile = Join-Path $Destination "$Name-backup-$timestamp.tar"

            Write-DistroNexusLog "Exporting '$Name' to '$backupFile'"
            Export-DistroNexusInstance -Name $Name -Destination $backupFile -Force

            Write-DistroNexusLog "Backup complete: '$backupFile'"

            # Enforce retention policy
            $existing = Get-ChildItem -Path $Destination -Filter "$Name-backup-*.tar" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending

            if ($existing.Count -gt $RetentionCount) {
                $toDelete = $existing | Select-Object -Skip $RetentionCount
                foreach ($f in $toDelete) {
                    Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue
                    Write-DistroNexusLog "Retention: deleted old backup '$($f.Name)'"
                }
            }

            return [PSCustomObject]@{
                PSTypeName  = 'DistroNexus.BackupResult'
                Name        = $Name
                BackupFile  = $backupFile
                Timestamp   = $timestamp
                Success     = $true
            }
        }
        catch {
            Write-DistroNexusLog "Backup failed for '$Name': $_" -Level ERROR
            Write-Error "Backup failed for '$Name': $_" -ErrorId "DistroNexus.BackupFailed"
        }
        finally {
            # Restart instance if it was running
            if ($wasRunning) {
                Write-DistroNexusLog "Restarting instance '$Name' after backup"
                Start-DistroNexusInstance -Name $Name | Out-Null
            }
        }
    }
}
