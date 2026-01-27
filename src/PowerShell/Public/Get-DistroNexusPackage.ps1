function Get-DistroNexusPackage {
    <#
    .SYNOPSIS
        Lists available WSL distribution packages.

    .DESCRIPTION
        Retrieves the catalog of available distributions with their download status
        (cached locally or available online).

    .PARAMETER Family
        Filter by distribution family (e.g., "Ubuntu", "Debian").

    .EXAMPLE
        Get-DistroNexusPackage

    .EXAMPLE
        Get-DistroNexusPackage -Family "Ubuntu"

    .OUTPUTS
        PSCustomObject representing each available package
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$Family
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Loading distro catalog..." -FileOnly
        
        try {
            $config = Get-DistroNexusConfig
            if (-not $config.Distros) {
                Write-DistroNexusLog "Distro catalog not found" -Level ERROR
                return
            }
            
            $packages = @()
            $cachePath = $config.Settings.PackageCachePath
            
            foreach ($familyProp in $config.Distros.PSObject.Properties) {
                $familyName = $familyProp.Value.Name
                
                # Apply family filter
                if ($Family -and $familyName -ne $Family) {
                    continue
                }
                
                foreach ($versionProp in $familyProp.Value.Versions.PSObject.Properties) {
                    $version = $versionProp.Value
                    
                    # Check if cached
                    $isCached = $false
                    $localPath = $null
                    
                    if ($version.LocalPath -and (Test-Path $version.LocalPath)) {
                        $isCached = $true
                        $localPath = $version.LocalPath
                    }
                    elseif ($cachePath -and $version.Filename) {
                        $cachedFile = Join-Path $cachePath $version.Filename
                        if (Test-Path $cachedFile) {
                            $isCached = $true
                            $localPath = $cachedFile
                        }
                    }
                    
                    $package = [PSCustomObject]@{
                        PSTypeName = 'DistroNexus.Package'
                        Family = $familyName
                        Name = $version.Name
                        DefaultName = $version.DefaultName
                        Url = $version.Url
                        Filename = $version.Filename
                        Source = $version.Source
                        IsCached = $isCached
                        LocalPath = $localPath
                    }
                    
                    $packages += $package
                }
            }
            
            Write-DistroNexusLog "Found $($packages.Count) package(s)" -FileOnly
            return $packages
        }
        catch {
            Write-DistroNexusLog "Failed to load catalog: $_" -Level ERROR
            return
        }
    }
}
