function Remove-DistroNexusPackage {
    <#
    .SYNOPSIS
        Removes a cached WSL distribution package.

    .DESCRIPTION
        Deletes a downloaded distribution package from the local cache directory.
        This does not affect installed WSL instances.

    .PARAMETER DefaultName
        The default name of the distribution package to remove (e.g., "Ubuntu-22.04").

    .PARAMETER LocalPath
        The full path to the package file to remove.

    .PARAMETER Force
        Skip confirmation prompt.

    .EXAMPLE
        Remove-DistroNexusPackage -DefaultName "Ubuntu-22.04"

    .EXAMPLE
        Remove-DistroNexusPackage -LocalPath "D:\wsl\distro\ubuntu-24.04.3-wsl-amd64.wsl" -Force

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(DefaultParameterSetName = 'ByDefaultName')]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'ByDefaultName')]
        [string]$DefaultName,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByPath')]
        [string]$LocalPath,

        [Parameter(Mandatory = $false)]
        [switch]$Force
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        try {
            $filePath = $null

            if ($PSCmdlet.ParameterSetName -eq 'ByDefaultName') {
                Write-DistroNexusLog "Looking for package: $DefaultName" -FileOnly
                
                # Load catalog to find the package
                $config = Get-DistroNexusConfig
                $cachePath = $config.Settings.PackageCachePath
                
                if (-not $config.Distros) {
                    Write-DistroNexusLog "Distro catalog not found" -Level ERROR
                    return $false
                }

                # Find the package
                $found = $false
                
                # Handle both nested (legacy) and flat (new) config formats
                if ($config.Distros -is [System.Collections.IDictionary] -or $config.Distros -is [System.Management.Automation.PSCustomObject]) {
                    # Nested structure
                    foreach ($familyProp in $config.Distros.PSObject.Properties) {
                        foreach ($versionProp in $familyProp.Value.Versions.PSObject.Properties) {
                            $version = $versionProp.Value
                            if ($version.DefaultName -eq $DefaultName) {
                                $found = $true
                                
                                # Check LocalPath first
                                if ($version.LocalPath -and (Test-Path $version.LocalPath)) {
                                    $filePath = $version.LocalPath
                                }
                                elseif ($cachePath -and $version.Filename) {
                                    $cachedFile = Join-Path $cachePath $version.Filename
                                    if (Test-Path $cachedFile) {
                                        $filePath = $cachedFile
                                    }
                                }
                                break
                            }
                        }
                        if ($found) { break }
                    }
                }
                else {
                    # Flat list (array)
                    $distroList = @($config.Distros)
                    foreach ($distro in $distroList) {
                        if ($distro.DefaultName -eq $DefaultName) {
                            $found = $true
                            
                            # Check LocalPath first
                            if ($distro.LocalPath -and (Test-Path $distro.LocalPath)) {
                                $filePath = $distro.LocalPath
                            }
                            elseif ($cachePath -and $distro.Filename) {
                                $cachedFile = Join-Path $cachePath $distro.Filename
                                if (Test-Path $cachedFile) {
                                    $filePath = $cachedFile
                                }
                            }
                            break
                        }
                    }
                }

                if (-not $found) {
                    Write-DistroNexusLog "Package not found in catalog: $DefaultName" -Level ERROR -FileOnly
                    return $false
                }

                if (-not $filePath) {
                    Write-DistroNexusLog "Package is not cached: $DefaultName" -Level WARN -FileOnly
                    return $false
                }
            }
            else {
                # ByPath parameter set
                $filePath = $LocalPath
                
                if (-not (Test-Path $filePath)) {
                    Write-DistroNexusLog "File not found: $filePath" -Level ERROR -FileOnly
                    return $false
                }
            }

            # Get file info
            $fileInfo = Get-Item $filePath
            $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)

            # Confirm deletion
            if (-not $Force) {
                $fileName = $fileInfo.Name
                # Write-Host avoided to keep pipeline clean
                $confirmation = Read-Host "About to delete: $fileName ($fileSizeMB MB). Are you sure? (Y/N)"
                if ($confirmation -ne 'Y' -and $confirmation -ne 'y') {
                    Write-DistroNexusLog "Deletion cancelled by user" -Level INFO -FileOnly
                    return $false
                }
            }

            # Delete the file
            Write-DistroNexusLog "Deleting package: $filePath ($fileSizeMB MB)" -FileOnly
            Remove-Item -Path $filePath -Force -ErrorAction Stop
            
            Write-DistroNexusLog "Package deleted successfully" -Level INFO -FileOnly
            return $true
        }
        catch {
            Write-DistroNexusLog "Failed to remove package: $_" -Level ERROR -FileOnly
            return $false
        }
    }
}
