function Get-DistroNexusInstance {
    <#
    .SYNOPSIS
        Gets information about installed WSL instances.

    .DESCRIPTION
        Retrieves detailed information about WSL distributions registered on the system,
        including status, version, base path, and disk usage.

    .PARAMETER Name
        Filter by instance name. Supports wildcards.

    .EXAMPLE
        Get-DistroNexusInstance
        # Gets all WSL instances

    .EXAMPLE
        Get-DistroNexusInstance -Name "Ubuntu*"
        # Gets all Ubuntu instances

    .OUTPUTS
        PSCustomObject representing each WSL instance
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name
    )
    
    begin {
        Initialize-DistroNexusLogger
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
                
                $instances += $instance
            }
            catch {
                Write-DistroNexusLog "Failed to process registry key $($key.PSPath): $_" -Level WARN
            }
        }
        
        Write-DistroNexusLog "Found $($instances.Count) WSL instance(s)" -FileOnly
        return $instances
    }
}
