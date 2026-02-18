<#
.SYNOPSIS
    Updates the local catalog.json from the official Microsoft WSL distribution list.
.DESCRIPTION
    Fetches the latest distribution list from https://raw.githubusercontent.com/microsoft/WSL/master/distributions/DistributionInfo.json
    and converts it to the DistroNexus catalog format.
.PARAMETER OutputPath
    Path to save the generated catalog.json. Defaults to config/catalog.json in the repo root.
.EXAMPLE
    .\tools\update_catalog_from_ms.ps1
#>
param(
    [string]$OutputPath = "$PSScriptRoot\..\config\catalog.json"
)

$ErrorActionPreference = 'Stop'
$MsDistroUrl = "https://raw.githubusercontent.com/microsoft/WSL/master/distributions/DistributionInfo.json"

Write-Host "🌐 Fetching distribution list from Microsoft..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri $MsDistroUrl -Method Get
}
catch {
    Write-Error "Failed to fetch distribution list: $_"
    exit 1
}

$catalog = @()

# Process ModernDistributions
if ($response.ModernDistributions) {
    foreach ($family in $response.ModernDistributions.PSObject.Properties) {
        $familyName = $family.Name
        $versions = $family.Value
        
        foreach ($version in $versions) {
            # Skip if no URL for x64
            if (-not $version.Amd64Url.Url) { continue }
            
            $package = [ordered]@{
                Id          = $version.Name
                Name        = $version.FriendlyName
                Version     = $version.Name -replace "^.*-([\d.]+)$",'$1' # Attempt to extract version
                DefaultName = $version.Name
                Description = "$($version.FriendlyName) ($familyName)"
                Category    = $familyName
                DownloadUrl = $version.Amd64Url.Url
                Sha256      = $version.Amd64Url.Sha256
                FileSize    = 0 # Unknown without HEAD request
                Metadata    = @{
                    "Source" = "Microsoft Checkpoint"
                }
            }
            
            # Add ARM64 URL to Metadata
            if ($version.Arm64Url.Url) {
                $package['Metadata']['DownloadUrl_ARM64'] = $version.Arm64Url.Url
                $package['Metadata']['Sha256_ARM64'] = $version.Arm64Url.Sha256
            }
            
            $catalog += $package
        }
    }
}

# Generate JSON
$jsonOptions = @{
    Depth = 10
    Compress = $false
}

$jsonContent = $catalog | ConvertTo-Json @jsonOptions
$jsonContent = [System.Text.RegularExpressions.Regex]::Unescape($jsonContent) # Fix escaped slashes if needed

Write-Host "💾 Saving catalog to $OutputPath..." -ForegroundColor Cyan
Set-Content -Path $OutputPath -Value $jsonContent -Encoding UTF8

Write-Host "✅ Catalog updated successfully with $($catalog.Count) distributions." -ForegroundColor Green
