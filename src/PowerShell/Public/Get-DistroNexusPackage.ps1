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
            
            # Assume flat array format
            $distroList = @()
            if ($config.Distros -is [Array]) {
                $distroList = $config.Distros
            }
            else {
                # Single object or unexpected format, wrap in array if possible
                if ($config.Distros) {
                    $distroList = @($config.Distros)
                }
            }

            foreach ($distro in $distroList) {
                # Apply family filter
                if ($Family -and $distro.Category -ne $Family) {
                    continue
                }
                
                # Update status for the flat object
                $isCached = $false
                $localPath = $null
                
                # Initialize fileSize from existing property if available, otherwise 0
                $fileSize = 0
                if ($distro.FileSize) { $fileSize = $distro.FileSize }

                # Check cache based on LocalPath or reconstruct path from DownloadUrl
                if ($distro.LocalPath -and (Test-Path $distro.LocalPath)) {
                    $fileInfo = Get-Item $distro.LocalPath
                    if ($fileInfo.Length -gt 0) {
                        $isCached = $true
                        $localPath = $distro.LocalPath
                        $fileSize = $fileInfo.Length
                    }
                }
                elseif ($cachePath -and $distro.DownloadUrl) {
                    $filename = Split-Path $distro.DownloadUrl -Leaf
                    if ($filename) {
                        $cachedFile = Join-Path $cachePath $filename
                        if (Test-Path $cachedFile) {
                            $fileInfo = Get-Item $cachedFile
                            if ($fileInfo.Length -gt 0) {
                                $isCached = $true
                                $localPath = $cachedFile
                                $fileSize = $fileInfo.Length
                            }
                        }
                    }
                }
                
                # Clone the object deeply using JSON serialization/deserialization
                $package = $distro | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                
                # Add PSTypeName manually
                $package.PSObject.TypeNames.Insert(0, 'DistroNexus.Package')
                
                # Add extra properties if missing
                if (-not $package.PSObject.Properties['IsCached']) {
                    $package | Add-Member -MemberType NoteProperty -Name 'IsCached' -Value $isCached -Force
                } else {
                    $package.IsCached = $isCached
                }
                
                if (-not $package.PSObject.Properties['LocalPath']) {
                    $package | Add-Member -MemberType NoteProperty -Name 'LocalPath' -Value $(if ($localPath) { $localPath } else { '' }) -Force
                } else {
                    $package.LocalPath = if ($localPath) { $localPath } else { '' }
                }
                
                if (-not $package.PSObject.Properties['FileSize']) {
                    $package | Add-Member -MemberType NoteProperty -Name 'FileSize' -Value $fileSize -Force
                } else {
                     $package.FileSize = $fileSize
                }
                
                $packages += $package
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
