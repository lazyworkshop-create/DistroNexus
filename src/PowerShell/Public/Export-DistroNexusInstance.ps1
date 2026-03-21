function Export-DistroNexusInstance {
    <#
    .SYNOPSIS
        Exports a WSL instance to a TAR file.

    .DESCRIPTION
        Checks the instance exists and (by default) is not running before exporting.
        Uses `wsl --export` to create the TAR archive. When Destination is a directory,
        the filename defaults to <Name>-<yyyyMMdd>.tar.

    .PARAMETER Name
        The name of the WSL instance to export.

    .PARAMETER Destination
        Path to the output TAR file, or a directory in which to create the default filename.

    .PARAMETER Force
        If the instance is running, stop it automatically before exporting.

    .EXAMPLE
        Export-DistroNexusInstance -Name "Ubuntu-22.04" -Destination "D:\Backups"

    .EXAMPLE
        Export-DistroNexusInstance -Name "Ubuntu-22.04" -Destination "D:\Backups\ubuntu.tar" -Force
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string]$Destination,

        [Parameter(Mandatory = $false)]
        [switch]$Force
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        # ErrorId = "DistroNexus.ExportFailed"

        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found." -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        # Handle running instance
        if ($instance.State -eq "Running") {
            if ($Force) {
                Write-Verbose "Stopping running instance '$Name' before export..."
                wsl --terminate $Name 2>&1 | Out-Null
                Write-DistroNexusLog "Stopped instance '$Name' for export" -FileOnly
            }
            else {
                Write-Error "Instance '$Name' is currently running. Use -Force to stop it before exporting, or stop it manually with Stop-DistroNexusInstance." `
                    -ErrorId "DistroNexus.InstanceAlreadyRunning"
                return
            }
        }

        # Resolve destination path
        if (Test-Path $Destination -PathType Container) {
            $dateStr = (Get-Date).ToString("yyyyMMdd")
            $Destination = Join-Path $Destination "$Name-$dateStr.tar"
        }

        # Ensure destination directory exists
        $destDir = Split-Path $Destination -Parent
        if ($destDir -and -not (Test-Path $destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        }

        Write-DistroNexusLog "Exporting '$Name' to '$Destination'..."
        Write-Verbose "Exporting WSL instance '$Name' to '$Destination'..."

        # Start export as background job
        $job = Start-Job -ScriptBlock {
            param($inst, $dest)
            wsl --export $inst $dest
        } -ArgumentList $Name, $Destination

        # Poll output file size while job runs
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($job.State -eq "Running") {
            Start-Sleep -Milliseconds 500
            $sizeMB = if (Test-Path $Destination) {
                [math]::Round((Get-Item $Destination).Length / 1MB, 1)
            }
            else { 0 }
            $elapsed = [math]::Round($sw.Elapsed.TotalSeconds, 0)
            Write-Progress -Activity "Exporting $Name" `
                -Status "${sizeMB} MB written, ${elapsed}s elapsed" `
                -PercentComplete -1
        }
        $sw.Stop()
        Write-Progress -Activity "Exporting $Name" -Completed

        $jobId = $job.Id
        Receive-Job -Id $jobId -Wait -AutoRemoveJob | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Error -Message "Export failed for $Name" `
                -ErrorId "DistroNexus.ExportFailed" `
                -Category OperationStopped `
                -TargetObject $Name `
                -ErrorAction Stop
        }

        Write-DistroNexusLog "Export complete: '$Name' -> '$Destination'" -FileOnly
        Write-Verbose "Export complete."

        return [PSCustomObject]@{
            PSTypeName  = 'DistroNexus.ExportResult'
            Name        = $Name
            Destination = $Destination
            Success     = $true
        }
    }
}
