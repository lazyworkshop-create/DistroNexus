function Export-DistroNexusInstance {
    <#
    .SYNOPSIS
        Exports a WSL instance to a TAR file.

    .DESCRIPTION
        Checks the instance exists and (by default) is not running before exporting.
        Runs the export as a background job and reports progress via Write-Progress.
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
        $stoppedForExport = $false

        try {
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
                    if (-not (Stop-DistroNexusInstance -Name $Name -Force)) {
                        Write-Error -Message "Failed to stop instance '$Name' before export." `
                            -ErrorId "DistroNexus.StopFailed" `
                            -Category OperationStopped `
                            -TargetObject $Name `
                            -ErrorAction Stop
                    }
                    $stoppedForExport = $true
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
                & wsl --export $inst $dest 2>&1 | Out-Null
                [PSCustomObject]@{
                    ExitCode = $LASTEXITCODE
                }
            } -ArgumentList $Name, $Destination

            # Poll output file size while job runs
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            while ($true) {
                $job = Get-Job -Id $job.Id
                if ($job.State -ne "Running") {
                    break
                }

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

            $jobResult = Receive-Job -Id $job.Id -Wait -AutoRemoveJob
            $exitCodeResult = $jobResult | Where-Object {
                $_ -and $_.PSObject.Properties.Match('ExitCode').Count -gt 0
            } | Select-Object -Last 1
            $exitCode = if ($exitCodeResult) { $exitCodeResult.ExitCode } else { $null }

            if ($null -ne $exitCode -and $exitCode -ne 0) {
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
        finally {
            # Clean up job if it still exists
            if ($null -ne $job) {
                Remove-Job -Id $job.Id -Force -ErrorAction SilentlyContinue
            }

            if ($stoppedForExport) {
                Write-DistroNexusLog "Restarting instance '$Name' after export" -FileOnly
                if (-not (Start-DistroNexusInstance -Name $Name)) {
                    Write-DistroNexusLog "Failed to restart instance '$Name' after export" -Level WARN
                    Write-Warning "Failed to restart instance '$Name' after export."
                }
            }
        }
    }
}
