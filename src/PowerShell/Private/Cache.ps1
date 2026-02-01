function Get-InstanceCache {
    <#
    .SYNOPSIS
        Retrieves WSL instance information from persistent configuration file.

    .DESCRIPTION
        Internal helper function to load instance configuration from instances.json.
        This is a persistent configuration file, not a time-based cache.
        Automatically migrates old cache format and backs up corrupted files.

    .PARAMETER CachePath
        Path to config directory. If not specified, uses default config path.

    .EXAMPLE
        $instances = Get-InstanceCache
        if ($instances) {
            # Use stored configuration data
        }

    .OUTPUTS
        Array of PSCustomObject representing stored instances, or $null if missing.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$CachePath
    )
    
    if (-not $CachePath) {
        # Standard configuration path in AppData
        $CachePath = Join-Path $env:APPDATA "DistroNexus"
        
        if (-not (Test-Path $CachePath)) {
            New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
        }
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    if (-not (Test-Path $cacheFile)) {
        Write-Verbose "Instance configuration file not found at $cacheFile"
        return $null
    }
    
    try {
        $cacheContent = Get-Content -Raw -Path $cacheFile | ConvertFrom-Json
        
        # Auto-migrate old cache format (CachedAt -> LastUpdated)
        if ($cacheContent.PSObject.Properties.Name -contains 'CachedAt') {
            Write-Verbose "Migrating old cache format to persistent configuration"
            $cacheContent | Add-Member -NotePropertyName 'LastUpdated' -NotePropertyValue $cacheContent.CachedAt -Force
            $cacheContent.PSObject.Properties.Remove('CachedAt')
            
            # Save migrated format
            try {
                $cacheContent | ConvertTo-Json -Depth 5 | Set-Content -Path $cacheFile -Force -Encoding UTF8
                Write-DistroNexusLog "Instance configuration migrated to new format" -FileOnly
            }
            catch {
                Write-DistroNexusLog "Failed to save migrated configuration: $_" -Level WARN
            }
        }
        
        Write-Verbose "Loaded $($cacheContent.Instances.Count) instance(s) from configuration"
        return $cacheContent.Instances
    }
    catch {
        Write-DistroNexusLog "Failed to load instance configuration: $_" -Level WARN
        
        # Backup corrupted file
        try {
            $backupPath = $cacheFile + ".corrupted." + (Get-Date).ToString("yyyyMMddHHmmss")
            Copy-Item -Path $cacheFile -Destination $backupPath -Force
            Write-DistroNexusLog "Corrupted instance configuration backed up to: $backupPath" -Level WARN
        }
        catch {
            Write-DistroNexusLog "Failed to backup corrupted configuration: $_" -Level WARN
        }
        
        return $null
    }
}

function Set-InstanceCache {
    <#
    .SYNOPSIS
        Saves WSL instance information to persistent configuration file.

    .DESCRIPTION
        Internal helper function to persist instance information to instances.json.
        This is a persistent configuration file that stores instance metadata.

    .PARAMETER Instances
        Array of instance objects to save.

    .PARAMETER CachePath
        Path to config directory. If not specified, uses default config path.

    .EXAMPLE
        $instances = Get-DistroNexusInstance
        Set-InstanceCache -Instances $instances
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject[]]$Instances,
        
        [Parameter(Mandatory = $false)]
        [string]$CachePath
    )
    
    if (-not $CachePath) {
        # Standard configuration path in AppData
        $CachePath = Join-Path $env:APPDATA "DistroNexus"
    }
    
    # Ensure config directory exists
    if (-not (Test-Path $CachePath)) {
        New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    $cacheObject = @{
        LastUpdated = (Get-Date).ToString("o")  # ISO 8601 format
        InstanceCount = $Instances.Count
        Instances = $Instances
    }
    
    try {
        $cacheObject | ConvertTo-Json -Depth 5 | Set-Content -Path $cacheFile -Force -Encoding UTF8
        Write-DistroNexusLog "Instance configuration updated: $($Instances.Count) instance(s)" -FileOnly
        Write-Verbose "Instance configuration saved to $cacheFile"
    }
    catch {
        Write-DistroNexusLog "Failed to save instance configuration: $_" -Level WARN
        # Non-fatal error, continue without saving
    }
}

function Clear-InstanceCache {
    <#
    .SYNOPSIS
        Clears the instance configuration file.

    .DESCRIPTION
        Internal helper function to remove the instance configuration file.
        This will force a complete rescan on next Get-DistroNexusInstance call.

    .PARAMETER CachePath
        Path to config directory. If not specified, uses default config path.

    .EXAMPLE
        Clear-InstanceCache
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$CachePath
    )
    
    if (-not $CachePath) {
        # Standard configuration path in AppData
        $CachePath = Join-Path $env:APPDATA "DistroNexus"
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    if (Test-Path $cacheFile) {
        try {
            Remove-Item -Path $cacheFile -Force
            Write-DistroNexusLog "Instance configuration cleared" -FileOnly
            Write-Verbose "Instance configuration file removed: $cacheFile"
        }
        catch {
            Write-DistroNexusLog "Failed to clear instance configuration: $_" -Level ERROR
            throw
        }
    }
    else {
        Write-Verbose "Instance configuration file does not exist: $cacheFile"
    }
}
