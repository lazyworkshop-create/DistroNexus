function Get-DistroNexusInstance {
    <#
    .SYNOPSIS
        Gets information about installed WSL instances.

    .DESCRIPTION
        Retrieves detailed information about WSL distributions registered on the system,
        including status, version, base path, and disk usage.
        
        By default, uses cached data (valid for 10 minutes) to improve performance.
        Use -ForceUpdate to bypass cache and scan the system directly.

    .PARAMETER Name
        Filter by instance name. Supports wildcards.

    .PARAMETER ForceUpdate
        Forces a fresh scan of the system, bypassing the cache.
        Use this to ensure you have the most current information.

    .PARAMETER IncludeRelease
        Includes Linux distribution release information (e.g., Ubuntu 22.04).
        WARNING: This requires starting stopped instances, which may be slow.

    .PARAMETER IncludeUser
        Includes current default user information.
        WARNING: This requires starting stopped instances, which may be slow.

    .EXAMPLE
        Get-DistroNexusInstance
        # Gets all WSL instances (uses cache if available)

    .EXAMPLE
        Get-DistroNexusInstance -Name "Ubuntu*"
        # Gets all Ubuntu instances

    .EXAMPLE
        Get-DistroNexusInstance -ForceUpdate
        # Forces a fresh scan, ignoring cache

    .EXAMPLE
        Get-DistroNexusInstance -IncludeRelease -IncludeUser
        # Gets instances with detailed Linux information (slow)

    .OUTPUTS
        PSCustomObject representing each WSL instance
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name,
        
        [Parameter(Mandatory = $false)]
        [switch]$ForceUpdate,
        
        [Parameter(Mandatory = $false)]
        [switch]$IncludeRelease,
        
        [Parameter(Mandatory = $false)]
        [switch]$IncludeUser
    )
    
    begin {
        Initialize-DistroNexusLogger
        
        # Try to use cache if ForceUpdate is not specified
        if (-not $ForceUpdate -and -not $IncludeRelease -and -not $IncludeUser) {
            $cachedInstances = Get-InstanceCache
            if ($cachedInstances) {
                Write-DistroNexusLog "Using cached instance data" -FileOnly
                
                # Apply name filter if specified
                if ($Name) {
                    $cachedInstances = $cachedInstances | Where-Object { $_.Name -like $Name }
                }
                
                return $cachedInstances
            }
        }
        
        Write-DistroNexusLog "Scanning WSL instances..." -FileOnly
    }
    
    process {
        # Get running state from wsl --list --verbose
        $wslStatus = @{}
        try {
            $cliOutput = wsl --list --verbose 2>&1
            if ($LASTEXITCODE -eq 0 -and $cliOutput) {
                foreach ($line in $cliOutput) {
                    $line = $line -replace "`0", ""
                    if ($line -match "NAME|---") { continue }
                    $cleanLine = $line.Replace("*", " ").Trim() -split "\s+"
                    if ($cleanLine.Count -ge 3) {
                        $distroName = $cleanLine[0]
                        $wslStatus[$distroName] = @{
                            State = $cleanLine[1]
                            Version = $cleanLine[2]
                        }
                    }
                }
            }
        }
        catch {
            Write-DistroNexusLog "Failed to query WSL status: $_" -Level WARN
        }
        
        # Scan registry for installed instances
        $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
        if (-not (Test-Path $lxssPath)) {
            Write-Warning "No WSL distributions found in registry."
            return
        }
        
        $instances = @()
        $keys = Get-ChildItem -Path $lxssPath
        
        foreach ($key in $keys) {
            try {
                $props = Get-ItemProperty -Path $key.PSPath
                $distroName = $props.DistributionName
                
                if (-not $distroName) { continue }
                
                # Apply name filter if specified
                if ($Name -and $distroName -notlike $Name) {
                    continue
                }
                
                $basePath = $props.BasePath
                
                # Get filesystem info
                $installTime = $null
                $diskSize = 0
                if ($basePath -and (Test-Path $basePath)) {
                    try {
                        $dirInfo = Get-Item $basePath
                        $installTime = $dirInfo.CreationTime
                        
                        # Calculate disk usage (VHDX file size)
                        $vhdxPath = Join-Path $basePath "ext4.vhdx"
                        if (Test-Path $vhdxPath) {
                            $vhdxInfo = Get-Item $vhdxPath
                            $diskSize = $vhdxInfo.Length
                        }
                    }
                    catch {
                        Write-Verbose "Failed to get filesystem info for $distroName : $_"
                    }
                }
                
                # Get status from wsl
                $state = "Stopped"
                $version = "?"
                if ($wslStatus.ContainsKey($distroName)) {
                    $state = $wslStatus[$distroName].State
                    $version = $wslStatus[$distroName].Version
                }
                
                # Create instance object
                $instance = [PSCustomObject]@{
                    PSTypeName = 'DistroNexus.WslInstance'
                    Name = $distroName
                    State = $state
                    Version = $version
                    BasePath = $basePath
                    DiskSize = $diskSize
                    InstallTime = $installTime
                    Guid = $key.PSChildName
                }
                
                # Add release information if requested (requires starting instance)
                if ($IncludeRelease -and $state -eq "Stopped") {
                    Write-Verbose "Querying release info for $distroName (starting instance)..."
                    try {
                        $releaseOutput = wsl --distribution $distroName -- bash -c "lsb_release -d 2>/dev/null | cut -f2" 2>$null
                        if ($releaseOutput -and $LASTEXITCODE -eq 0) {
                            $instance | Add-Member -NotePropertyName "Release" -NotePropertyValue $releaseOutput.Trim()
                        }
                    }
                    catch {
                        Write-Verbose "Failed to query release for $distroName : $_"
                    }
                }
                elseif ($IncludeRelease) {
                    try {
                        $releaseOutput = wsl --distribution $distroName -- bash -c "lsb_release -d 2>/dev/null | cut -f2" 2>$null
                        if ($releaseOutput -and $LASTEXITCODE -eq 0) {
                            $instance | Add-Member -NotePropertyName "Release" -NotePropertyValue $releaseOutput.Trim()
                        }
                    }
                    catch {
                        Write-Verbose "Failed to query release for $distroName : $_"
                    }
                }
                
                # Add user information if requested (requires starting instance)
                if ($IncludeUser) {
                    Write-Verbose "Querying user info for $distroName..."
                    try {
                        $userOutput = wsl --distribution $distroName -- bash -c "whoami" 2>$null
                        if ($userOutput -and $LASTEXITCODE -eq 0) {
                            $instance | Add-Member -NotePropertyName "CurrentUser" -NotePropertyValue $userOutput.Trim()
                        }
                    }
                    catch {
                        Write-Verbose "Failed to query user for $distroName : $_"
                    }
                }
                
                $instances += $instance
            }
            catch {
                Write-DistroNexusLog "Failed to process registry key $($key.PSPath): $_" -Level WARN
            }
        }
        
        Write-DistroNexusLog "Found $($instances.Count) WSL instance(s)" -FileOnly
        
        # Update cache if not using IncludeRelease/IncludeUser (those require instance startup)
        if (-not $IncludeRelease -and -not $IncludeUser -and $instances.Count -gt 0) {
            Set-InstanceCache -Instances $instances
        }
        
        return $instances
    }
}
