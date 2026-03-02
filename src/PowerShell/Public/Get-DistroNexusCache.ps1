function Get-DistroNexusCache {
    <#
    .SYNOPSIS
        Returns diagnostic information about the current instance cache state.

    .DESCRIPTION
        Shows the cache age (time since last update), entry count, staleness flag,
        and the last invalidation reason. Useful for diagnosing stale-data issues.

    .OUTPUTS
        PSCustomObject with CacheAge, EntryCount, IsStale, LastReason, InvalidatedAt properties.

    .EXAMPLE
        Get-DistroNexusCache
    #>
    [CmdletBinding()]
    param()

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $cacheFile   = Join-Path $env:APPDATA "DistroNexus\instances.json"
        $entryCount  = 0
        $cacheAge    = $null

        if (Test-Path $cacheFile) {
            try {
                $content = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $entryCount = if ($content.Instances) { @($content.Instances).Count } else { 0 }
                if ($content.LastUpdated) {
                    $lastUpdated = [datetime]$content.LastUpdated
                    $cacheAge    = (Get-Date) - $lastUpdated
                }
            }
            catch {
                Write-DistroNexusLog "Could not read cache file for diagnostics: $_" -Level WARN
            }
        }

        $invalidationState = Get-CacheInvalidationState

        return [PSCustomObject]@{
            PSTypeName    = 'DistroNexus.CacheDiagnostics'
            CacheAge      = $cacheAge
            EntryCount    = $entryCount
            IsStale       = $invalidationState.IsStale
            LastReason    = $invalidationState.LastReason
            InvalidatedAt = $invalidationState.InvalidatedAt
            CacheFile     = $cacheFile
        }
    }
}
