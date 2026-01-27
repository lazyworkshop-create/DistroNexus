function Update-DistroNexusCatalog {
    <#
    .SYNOPSIS
        Updates the distribution catalog from the online source.

    .DESCRIPTION
        Fetches the latest distros.json from the configured GitHub repository
        and updates the local catalog. Falls back to existing local copy on failure.

    .PARAMETER SourceUrl
        Override the default catalog URL.

    .EXAMPLE
        Update-DistroNexusCatalog

    .EXAMPLE
        Update-DistroNexusCatalog -SourceUrl "https://raw.githubusercontent.com/user/repo/main/config/distros.json"

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$SourceUrl
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Updating distro catalog..."
        
        try {
            # Get source URL
            if (-not $SourceUrl) {
                $config = Get-DistroNexusConfig
                $SourceUrl = $config.Settings.DistroSourceUrl
                
                if (-not $SourceUrl) {
                    $SourceUrl = "https://raw.githubusercontent.com/LazyWorkshop-Create/DistroNexus/main/config/distros.json"
                }
            }
            
            Write-DistroNexusLog "Fetching from: $SourceUrl"
            
            # Download catalog
            $tempFile = Join-Path $env:TEMP "distros_temp.json"
            $progressPreference = 'SilentlyContinue'
            
            try {
                Invoke-WebRequest -Uri $SourceUrl -OutFile $tempFile -UseBasicParsing -TimeoutSec 10
                
                # Validate JSON
                $testJson = Get-Content -Raw -Path $tempFile | ConvertFrom-Json
                
                # Replace local catalog
                $configRoot = Join-Path $script:ModuleRoot "..\config"
                $catalogPath = Join-Path $configRoot "distros.json"
                
                Copy-Item -Path $tempFile -Destination $catalogPath -Force
                
                Write-DistroNexusLog "Catalog updated successfully"
                return $true
            }
            catch {
                Write-DistroNexusLog "Failed to download catalog: $_" -Level WARN
                Write-DistroNexusLog "Using local catalog (offline mode)" -Level WARN
                return $false
            }
            finally {
                $progressPreference = 'Continue'
                if (Test-Path $tempFile) {
                    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
                }
            }
        }
        catch {
            Write-DistroNexusLog "Failed to update catalog: $_" -Level ERROR
            return $false
        }
    }
}
