# PowerShell script to download all available WSL2 distro packages
# Author: GitHub Copilot

# --- Distro Definitions ---
$ConfigPath = Join-Path $PSScriptRoot "..\config\distros.json"
if (-not (Test-Path $ConfigPath)) { throw "Config file not found at: $ConfigPath" }

$SettingsPath = Join-Path $PSScriptRoot "..\config\settings.json"
$GlobalSettings = $null
if (Test-Path $SettingsPath) {
    try {
        $GlobalSettings = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json
    } catch {
        Write-Warning "Failed to load settings.json"
    }
}

try {
    $JsonRaw = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
} catch {
    throw "Failed to parse distros.json. Please ensure it is valid JSON."
}

$DistroCatalog = [ordered]@{}
# Reconstruct ordered dictionary
$Keys = $JsonRaw.PSObject.Properties.Name | Sort-Object { [int]$_ }
foreach ($Key in $Keys) {
    $FamObj = $JsonRaw.$Key
    $VersionsDict = [ordered]@{}
    $VerKeys = $FamObj.Versions.PSObject.Properties.Name | Sort-Object { [int]$_ }
    foreach ($vKey in $VerKeys) {
        $VersionsDict[$vKey] = $FamObj.Versions.$vKey
    }
    
    $DistroCatalog[$Key] = @{
        Name = $FamObj.Name
        Versions = $VersionsDict
    }
}

$BaseDir = Join-Path $PSScriptRoot "..\..\distro"
if ($GlobalSettings.DistroCachePath) {
    if ([System.IO.Path]::IsPathRooted($GlobalSettings.DistroCachePath)) {
        $BaseDir = $GlobalSettings.DistroCachePath
    } else {
        $BaseDir = Join-Path $PSScriptRoot $GlobalSettings.DistroCachePath
    }
}
# Normalize path
$BaseDir = [System.IO.Path]::GetFullPath($BaseDir)

Write-Host "=== WSL Distro Downloader ===" -ForegroundColor Cyan
Write-Host "Base Directory: $BaseDir"
Write-Host "============================`n"

foreach ($FamilyKey in $DistroCatalog.Keys) {
    $Family = $DistroCatalog[$FamilyKey]
    
    foreach ($VerKey in $Family.Versions.Keys) {
        $Version = $Family.Versions[$VerKey]
        
        # Organization: distro/FamilyName/VersionName/File
        $TargetDir = Join-Path $BaseDir $Family.Name
        $TargetDir = Join-Path $TargetDir $Version.Name
        
        if (-not (Test-Path $TargetDir)) {
            New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        }

        $OutFile = Join-Path $TargetDir $Version.Filename
        
        Write-Host "[$($Family.Name)] $($Version.Name)" -NoNewline

        if (Test-Path $OutFile) {
            Write-Host " -> Skipped (Already exists)" -ForegroundColor Yellow
        } else {
            Write-Host " -> Downloading..." -ForegroundColor Green
            try {
                Invoke-WebRequest -Uri $Version.Url -OutFile $OutFile -UseBasicParsing -Verbose
            } catch {
                Write-Host " -> Failed: $_" -ForegroundColor Red
            }
        }
    }
}

Write-Host "`nAll downloads completed." -ForegroundColor Cyan
if (Test-Path $BaseDir) { Invoke-Item $BaseDir }
