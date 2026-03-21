function Compress-DistroNexusInstance {
    <#
    .SYNOPSIS
        Compacts the VHDX disk of a WSL distribution to reclaim unused space.

    .DESCRIPTION
        Runs fstrim inside the instance to zero deleted blocks, then compacts the ext4.vhdx
        file using Optimize-VHD (Hyper-V module) or diskpart as a fallback.
        Automatically stops the instance before compaction and restarts it if it was running.

    .PARAMETER Name
        The name(s) of the WSL distribution(s) to compact. Accepts an array of names or input from the pipeline.

    .PARAMETER WhatIf
        Reports the estimated reclaimable space without performing compaction.

    .PARAMETER Force
        Skips the confirmation prompt before compacting.

    .OUTPUTS
        PSCustomObject with Name, SizeBefore, SizeAfter, SpaceSaved properties.

    .EXAMPLE
        Compress-DistroNexusInstance -Name "Ubuntu-22.04"

    .EXAMPLE
        Compress-DistroNexusInstance -Name "Ubuntu-22.04" -WhatIf

    .EXAMPLE
        Compress-DistroNexusInstance -Name "Ubuntu-22.04", "Debian"

    .EXAMPLE
        "Ubuntu-22.04", "Debian" | Compress-DistroNexusInstance
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [ValidateNotNullOrEmpty()]
        [string[]]$Name,

        [Parameter(Mandatory = $false)]
        [switch]$WhatIf,

        [Parameter(Mandatory = $false)]
        [switch]$Force
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        foreach ($instanceName in $Name) {
            Write-DistroNexusLog "Compress-DistroNexusInstance: starting for '$instanceName'"

            # Validate instance exists
            $instance = Get-DistroNexusInstance -Name $instanceName | Where-Object { $_.Name -eq $instanceName }
            if (-not $instance) {
                Write-Error "Instance '$instanceName' not found."
                Write-DistroNexusLog "Instance not found: $instanceName" -Level ERROR
                continue
            }

            # Resolve VHDX path from registry
            $vhdxPath = Get-InstanceVhdxPath -Name $instanceName
            if (-not $vhdxPath) {
                Write-Error "Could not resolve VHDX path for instance '$instanceName'."
                Write-DistroNexusLog "VHDX path not found for: $instanceName" -Level ERROR
                continue
            }

            # Measure current size
            $sizeBefore = 0
            if (Test-Path $vhdxPath) {
                $sizeBefore = (Get-Item $vhdxPath).Length
            }

            if ($WhatIf) {
                Write-DistroNexusLog "WhatIf: current VHDX size for '$instanceName' is $sizeBefore bytes"
                [PSCustomObject]@{
                    Name       = $instanceName
                    SizeBefore = $sizeBefore
                    SizeAfter  = $null
                    SpaceSaved = $null
                    WhatIf     = $true
                }
                continue
            }

            # Confirm if not forced
            if (-not $Force) {
                $sizeGB = [math]::Round($sizeBefore / 1GB, 2)
                $confirm = Read-Host "Compact VHDX for '$instanceName' (current: ${sizeGB} GB)? [y/N]"
                if ($confirm -notmatch '^[yY]$') {
                    Write-DistroNexusLog "Compaction cancelled by user for: $instanceName"
                    continue
                }
            }

            # Auto-stop if running
            $wasRunning = $instance.State -eq "Running"
            if ($wasRunning) {
                Write-DistroNexusLog "Stopping instance '$instanceName' before compaction"
                Write-Progress -Activity "Compacting $instanceName" -Status "Stopping instance..." -PercentComplete 10
                Stop-DistroNexusInstance -Name $instanceName | Out-Null
            }

            try {
                # fstrim inside instance to zero freed blocks
                Write-DistroNexusLog "Running fstrim inside '$instanceName'"
                Write-Progress -Activity "Compacting $instanceName" -Status "Running fstrim..." -PercentComplete 30
                try {
                    wsl -d $instanceName -e fstrim -av 2>&1 | Out-Null
                }
                catch {
                    Write-DistroNexusLog "fstrim failed (non-fatal): $_" -Level WARN
                }

                # Choose compaction method
                Write-Progress -Activity "Compacting $instanceName" -Status "Compacting VHDX..." -PercentComplete 50
                $hyperVAvailable = Get-Module -ListAvailable -Name "Hyper-V" -ErrorAction SilentlyContinue

                if ($hyperVAvailable) {
                    Write-DistroNexusLog "Using Optimize-VHD for compaction"
                    Optimize-VHD -Path $vhdxPath -Mode Full
                }
                else {
                    Write-DistroNexusLog "Hyper-V not available — using diskpart fallback"
                    if (-not (Test-AdminPrivilege)) {
                        Write-Error "diskpart requires administrator privileges. Re-run as administrator."
                        Write-DistroNexusLog "Compaction aborted: not running as administrator" -Level ERROR
                        continue
                    }
                    $diskpartScript = @"
select vdisk file="$vhdxPath"
compact vdisk
exit
"@
                    $scriptFile = Join-Path $env:TEMP "dn-compact-$instanceName.txt"
                    $diskpartScript | Set-Content $scriptFile -Encoding ASCII
                    Invoke-Expression "diskpart /s `"$scriptFile`""
                    Remove-Item $scriptFile -Force -ErrorAction SilentlyContinue
                }

                Write-Progress -Activity "Compacting $instanceName" -Status "Measuring result..." -PercentComplete 90

                $sizeAfter  = if (Test-Path $vhdxPath) { (Get-Item $vhdxPath).Length } else { 0 }
                $spaceSaved = $sizeBefore - $sizeAfter

                Write-DistroNexusLog "Compaction complete for '$instanceName': saved $([math]::Round($spaceSaved / 1MB, 1)) MB"
                Write-Progress -Activity "Compacting $instanceName" -Status "Done" -PercentComplete 100 -Completed

                [PSCustomObject]@{
                    Name       = $instanceName
                    SizeBefore = $sizeBefore
                    SizeAfter  = $sizeAfter
                    SpaceSaved = $spaceSaved
                }
            }
            catch {
                Write-DistroNexusLog "Compaction failed for '$instanceName': $_" -Level ERROR
                Write-Error "Compaction failed: $_"
            }
            finally {
                # Restart instance if it was running
                if ($wasRunning) {
                    Write-DistroNexusLog "Restarting instance '$instanceName' after compaction"
                    Start-DistroNexusInstance -Name $instanceName | Out-Null
                }
            }
        }
    }
}

function Get-InstanceVhdxPath {
    <#
    .SYNOPSIS
        Resolves the ext4.vhdx path for a WSL instance from the registry.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $lxssRoot = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
    $keys = Get-ChildItem $lxssRoot -ErrorAction SilentlyContinue
    foreach ($key in $keys) {
        $distName = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).DistributionName
        if ($distName -eq $Name) {
            $basePath = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).BasePath
            if ($basePath) {
                return Join-Path $basePath "ext4.vhdx"
            }
        }
    }

    $fallback = Get-ItemProperty -Path $lxssRoot -ErrorAction SilentlyContinue
    if ($fallback -and $fallback.BasePath) {
        return Join-Path $fallback.BasePath "ext4.vhdx"
    }

    return $null
}

function Test-AdminPrivilege {
    <#
    .SYNOPSIS
        Returns $true if the current process is running as administrator.
    #>
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
