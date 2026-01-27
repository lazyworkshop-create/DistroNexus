function Save-DistroNexusPackage {
    <#
    .SYNOPSIS
        Downloads a WSL distribution package to the local cache.

    .DESCRIPTION
        Downloads the specified distribution package from the configured source URL
        and saves it to the package cache directory.

    .PARAMETER DefaultName
        The default name of the distribution to download (e.g., "Ubuntu-22.04").

    .PARAMETER Destination
        Override the default cache directory.

    .EXAMPLE
        Save-DistroNexusPackage -DefaultName "Ubuntu-22.04"

    .EXAMPLE
        Save-DistroNexusPackage -DefaultName "Debian" -Destination "D:\Downloads"

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DefaultName,
        
        [Parameter(Mandatory = $false)]
        [string]$Destination
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Downloading package: $DefaultName"
        
        try {
            # Find package in catalog
            $package = Get-DistroNexusPackage | Where-Object { $_.DefaultName -eq $DefaultName }
            
            if (-not $package) {
                Write-DistroNexusLog "Package not found in catalog: $DefaultName" -Level ERROR
                return $false
            }
            
            if (-not $package.Url) {
                Write-DistroNexusLog "No download URL available for: $DefaultName" -Level ERROR
                return $false
            }
            
            # Determine destination
            if (-not $Destination) {
                $config = Get-DistroNexusConfig
                $Destination = $config.Settings.PackageCachePath
                if (-not $Destination) {
                    $Destination = Join-Path $env:LOCALAPPDATA "DistroNexus\packages"
                }
            }
            
            if (-not (Test-Path $Destination)) {
                New-Item -ItemType Directory -Path $Destination -Force | Out-Null
            }
            
            $outputFile = Join-Path $Destination $package.Filename
            
            if (Test-Path $outputFile) {
                Write-DistroNexusLog "Package already exists: $outputFile" -Level WARN
                return $true
            }
            
            if (-not $PSCmdlet.ShouldProcess($DefaultName, "Download package")) {
                return $false
            }
            
            # Download with progress
            Write-DistroNexusLog "Downloading from: $($package.Url)"
            Write-DistroNexusLog "Saving to: $outputFile"
            
            $progressPreference = 'SilentlyContinue'
            try {
                Invoke-WebRequest -Uri $package.Url -OutFile $outputFile -UseBasicParsing
                
                if (Test-Path $outputFile) {
                    Write-DistroNexusLog "Successfully downloaded: $outputFile"
                    return $true
                }
                else {
                    throw "Download completed but file not found"
                }
            }
            catch {
                throw "Download failed: $_"
            }
            finally {
                $progressPreference = 'Continue'
            }
        }
        catch {
            Write-DistroNexusLog "Failed to download package: $_" -Level ERROR
            return $false
        }
    }
}
