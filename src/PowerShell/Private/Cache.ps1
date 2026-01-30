function Get-InstanceCache {
    <#
    .SYNOPSIS
        Retrieves WSL instance information from cache file.

    .DESCRIPTION
        Internal helper function to load cached instance information from instances.json.
        Cache is considered valid if it was created within the last 10 minutes.

    .PARAMETER CachePath
        Path to cache directory. If not specified, uses default config path.

    .EXAMPLE
        $instances = Get-InstanceCache
        if ($instances) {
            # Use cached data
        }

    .OUTPUTS
        Array of PSCustomObject representing cached instances, or $null if cache is invalid/missing.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$CachePath
    )
    
    if (-not $CachePath) {
        $CachePath = Join-Path $script:ProjectRoot "config"
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    if (-not (Test-Path $cacheFile)) {
        Write-Verbose "Instance cache file not found at $cacheFile"
        return $null
    }
    
    try {
        $cacheContent = Get-Content -Raw -Path $cacheFile | ConvertFrom-Json
        
        # Check cache validity (10 minutes)
        $cacheAge = (Get-Date) - [DateTime]::Parse($cacheContent.CachedAt)
        if ($cacheAge.TotalMinutes -gt 10) {
            Write-Verbose "Instance cache expired (age: $($cacheAge.TotalMinutes.ToString('F1')) minutes)"
            return $null
        }
        
        Write-Verbose "Loaded $($cacheContent.Instances.Count) instance(s) from cache (age: $($cacheAge.TotalSeconds.ToString('F0'))s)"
        return $cacheContent.Instances
    }
    catch {
        Write-DistroNexusLog "Failed to load instance cache: $_" -Level WARN
        return $null
    }
}

function Set-InstanceCache {
    <#
    .SYNOPSIS
        Saves WSL instance information to cache file.

    .DESCRIPTION
        Internal helper function to persist instance information to instances.json.
        Includes timestamp for cache validation.

    .PARAMETER Instances
        Array of instance objects to cache.

    .PARAMETER CachePath
        Path to cache directory. If not specified, uses default config path.

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
        $CachePath = Join-Path $script:ProjectRoot "config"
    }
    
    # Ensure cache directory exists
    if (-not (Test-Path $CachePath)) {
        New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    $cacheObject = @{
        CachedAt = (Get-Date).ToString("o")  # ISO 8601 format
        InstanceCount = $Instances.Count
        Instances = $Instances
    }
    
    try {
        $cacheObject | ConvertTo-Json -Depth 5 | Set-Content -Path $cacheFile -Force -Encoding UTF8
        Write-DistroNexusLog "Instance cache updated: $($Instances.Count) instance(s)" -FileOnly
        Write-Verbose "Instance cache saved to $cacheFile"
    }
    catch {
        Write-DistroNexusLog "Failed to save instance cache: $_" -Level WARN
        # Non-fatal error, continue without caching
    }
}

function Update-InstanceCache {
    <#
    .SYNOPSIS
        Forces a refresh of the instance cache.

    .DESCRIPTION
        Internal helper function to update the cache by calling Get-DistroNexusInstance
        with -ForceUpdate parameter.

    .PARAMETER CachePath
        Path to cache directory. If not specified, uses default config path.

    .EXAMPLE
        Update-InstanceCache
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$CachePath
    )
    
    Write-Verbose "Forcing instance cache refresh..."
    
    # This will be called by Get-DistroNexusInstance with -ForceUpdate
    # So we just clear the cache file to force a rescan
    if (-not $CachePath) {
        $CachePath = Join-Path $script:ProjectRoot "config"
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    if (Test-Path $cacheFile) {
        try {
            Remove-Item -Path $cacheFile -Force
            Write-DistroNexusLog "Instance cache cleared" -FileOnly
        }
        catch {
            Write-DistroNexusLog "Failed to clear cache: $_" -Level WARN
        }
    }
}

function Clear-InstanceCache {
    <#
    .SYNOPSIS
        Clears the instance cache file.

    .DESCRIPTION
        Internal helper function to remove the instance cache file.
        Useful when cache becomes stale or corrupted.

    .PARAMETER CachePath
        Path to cache directory. If not specified, uses default config path.

    .EXAMPLE
        Clear-InstanceCache
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$CachePath
    )
    
    if (-not $CachePath) {
        $CachePath = Join-Path $script:ProjectRoot "config"
    }
    
    $cacheFile = Join-Path $CachePath "instances.json"
    
    if (Test-Path $cacheFile) {
        try {
            Remove-Item -Path $cacheFile -Force
            Write-DistroNexusLog "Instance cache cleared" -FileOnly
            Write-Verbose "Instance cache file removed: $cacheFile"
        }
        catch {
            Write-DistroNexusLog "Failed to clear instance cache: $_" -Level ERROR
            throw
        }
    }
    else {
        Write-Verbose "Instance cache file does not exist: $cacheFile"
    }
}
