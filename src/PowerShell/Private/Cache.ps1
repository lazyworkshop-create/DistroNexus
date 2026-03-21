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

        # Populate in-memory TTL timestamp from file so Test-DistroNexusCacheStale
        # uses the fast path on subsequent calls within the same session (E07-2).
        $tsField = if ($cacheContent.LastUpdated) { $cacheContent.LastUpdated } elseif ($cacheContent.CachedAt) { $cacheContent.CachedAt } else { $null }
        if ($tsField -and -not $script:__CacheState.CacheTimestamp) {
            try { $script:__CacheState.CacheTimestamp = [datetime]$tsField } catch {}
        }

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
        # Stamp TTL timestamp so Test-DistroNexusCacheStale can track freshness (E07-2)
        Set-DistroNexusCache -Timestamp (Get-Date)
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

# ── Cache invalidation state (E-07) ──────────────────────────────────────────
# Module-scoped state used by Invalidate-InstanceCache and Get-DistroNexusCache.

$script:__CacheState = [PSCustomObject]@{
    IsStale        = $false
    LastReason     = $null
    InvalidatedAt  = $null
    CacheTimestamp = $null
}

function Invalidate-InstanceCache {
    <#
    .SYNOPSIS
        Marks the instance cache as stale so the next read triggers a full refresh.
        Called by the C# WslEventWatcher host via ExecuteModuleCmdletAsync, or internally.
    #>
    param(
        [Parameter(Mandatory = $false)]
        [string]$Reason = "ExternalEvent"
    )

    $script:__CacheState.IsStale       = $true
    $script:__CacheState.LastReason    = $Reason
    $script:__CacheState.InvalidatedAt = Get-Date

    Write-DistroNexusLog "Cache invalidated. Reason: $Reason" -FileOnly
}

function Reset-CacheInvalidationState {
    <#
    .SYNOPSIS
        Clears the stale flag after a successful cache refresh.
    #>
    $script:__CacheState.IsStale = $false
}

function Get-CacheInvalidationState {
    <#
    .SYNOPSIS
        Returns the current cache invalidation state object.
    #>
    return $script:__CacheState
}

function Set-DistroNexusCache {
    <#
    .SYNOPSIS
        Records a cache timestamp for TTL tracking (E07-2).
    #>
    param(
        [Parameter(Mandatory = $false)]
        [object]$Data,
        [Parameter(Mandatory = $false)]
        [datetime]$Timestamp = (Get-Date)
    )
    $script:__CacheState.CacheTimestamp = $Timestamp
}

function Test-DistroNexusCacheStale {
    <#
    .SYNOPSIS
        Returns $true when the TTL timestamp is absent or older than 10 minutes.

    .DESCRIPTION
        Checks the in-memory timestamp first. If not available (e.g. new session),
        falls back to the LastUpdated field in the on-disk cache file so that a
        freshly-written cache is not immediately treated as stale.
    #>
    # Fast path: in-memory timestamp is available
    if ($script:__CacheState.CacheTimestamp) {
        return ((Get-Date) - $script:__CacheState.CacheTimestamp).TotalMinutes -ge 10
    }

    # Slow path: new session — read LastUpdated from disk
    $cachePath = Join-Path $env:APPDATA "DistroNexus"
    $cacheFile = Join-Path $cachePath "instances.json"
    if (Test-Path $cacheFile) {
        try {
            $data = Get-Content $cacheFile -Raw | ConvertFrom-Json -ErrorAction Stop
            $tsField = if ($data.LastUpdated) { $data.LastUpdated } elseif ($data.CachedAt) { $data.CachedAt } else { $null }
            if ($tsField) {
                $fileTime = [datetime]$tsField
                return ((Get-Date) - $fileTime).TotalMinutes -ge 10
            }
        }
        catch {
            # Unreadable file — treat as stale
        }
    }

    return $true  # No timestamp found anywhere
}

