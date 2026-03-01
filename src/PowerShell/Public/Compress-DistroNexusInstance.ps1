function Compress-DistroNexusInstance {
    <#
    .SYNOPSIS
        Compacts the VHDX disk of a WSL distribution to reclaim unused space.

    .DESCRIPTION
        Runs fstrim inside the instance to zero deleted blocks, then compacts the ext4.vhdx
        file using Optimize-VHD (Hyper-V module) or diskpart as a fallback.
        Automatically stops the instance before compaction and restarts it if it was running.

    .PARAMETER Name
        The name of the WSL distribution to compact.

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
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $false)]
        [switch]$WhatIf,

        [Parameter(Mandatory = $false)]
        [switch]$Force
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        Write-DistroNexusLog "Compress-DistroNexusInstance: starting for '$Name'"

        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name | Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found."
            Write-DistroNexusLog "Instance not found: $Name" -Level ERROR
            return
        }

        # Resolve VHDX path from registry
        $vhdxPath = Get-InstanceVhdxPath -Name $Name
        if (-not $vhdxPath) {
            Write-Error "Could not resolve VHDX path for instance '$Name'."
            Write-DistroNexusLog "VHDX path not found for: $Name" -Level ERROR
            return
        }

        # Measure current size
        $sizeBefore = 0
        if (Test-Path $vhdxPath) {
            $sizeBefore = (Get-Item $vhdxPath).Length
        }

        if ($WhatIf) {
            Write-DistroNexusLog "WhatIf: current VHDX size for '$Name' is $sizeBefore bytes"
            return [PSCustomObject]@{
                Name       = $Name
                SizeBefore = $sizeBefore
                SizeAfter  = $null
                SpaceSaved = $null
                WhatIf     = $true
            }
        }

        # Confirm if not forced
        if (-not $Force) {
            $sizeGB = [math]::Round($sizeBefore / 1GB, 2)
            $confirm = Read-Host "Compact VHDX for '$Name' (current: ${sizeGB} GB)? [y/N]"
            if ($confirm -notmatch '^[yY]$') {
                Write-DistroNexusLog "Compaction cancelled by user for: $Name"
                return
            }
        }

        # Auto-stop if running
        $wasRunning = $instance.State -eq "Running"
        if ($wasRunning) {
            Write-DistroNexusLog "Stopping instance '$Name' before compaction"
            Write-Progress -Activity "Compacting $Name" -Status "Stopping instance..." -PercentComplete 10
            Stop-DistroNexusInstance -Name $Name | Out-Null
        }

        try {
            # fstrim inside instance to zero freed blocks
            Write-DistroNexusLog "Running fstrim inside '$Name'"
            Write-Progress -Activity "Compacting $Name" -Status "Running fstrim..." -PercentComplete 30
            try {
                wsl -d $Name -e fstrim -av 2>&1 | Out-Null
            }
            catch {
                Write-DistroNexusLog "fstrim failed (non-fatal): $_" -Level WARN
            }

            # Choose compaction method
            Write-Progress -Activity "Compacting $Name" -Status "Compacting VHDX..." -PercentComplete 50
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
                    return
                }
                $diskpartScript = @"
select vdisk file="$vhdxPath"
compact vdisk
exit
"@
                $scriptFile = Join-Path $env:TEMP "dn-compact-$Name.txt"
                $diskpartScript | Set-Content $scriptFile -Encoding ASCII
                Invoke-Expression "diskpart /s `"$scriptFile`""
                Remove-Item $scriptFile -Force -ErrorAction SilentlyContinue
            }

            Write-Progress -Activity "Compacting $Name" -Status "Measuring result..." -PercentComplete 90

            $sizeAfter  = if (Test-Path $vhdxPath) { (Get-Item $vhdxPath).Length } else { 0 }
            $spaceSaved = $sizeBefore - $sizeAfter

            Write-DistroNexusLog "Compaction complete for '$Name': saved $([math]::Round($spaceSaved / 1MB, 1)) MB"
            Write-Progress -Activity "Compacting $Name" -Status "Done" -PercentComplete 100 -Completed

            return [PSCustomObject]@{
                Name       = $Name
                SizeBefore = $sizeBefore
                SizeAfter  = $sizeAfter
                SpaceSaved = $spaceSaved
            }
        }
        catch {
            Write-DistroNexusLog "Compaction failed for '$Name': $_" -Level ERROR
            Write-Error "Compaction failed: $_"
        }
        finally {
            # Restart instance if it was running
            if ($wasRunning) {
                Write-DistroNexusLog "Restarting instance '$Name' after compaction"
                Start-DistroNexusInstance -Name $Name | Out-Null
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
