# PowerShell script to download all available WSL2 distro packages
# Author: GitHub Copilot

param (
    [string]$SelectFamily,
    [string]$SelectVersion
)

# --- Distro Definitions ---
$ConfigPath = Join-Path $PSScriptRoot "..\config\distros.json"
if (-not (Test-Path $ConfigPath)) { throw "Config file not found at: $ConfigPath" }
$ConfigRaw = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
$ConfigChanged = $false

$SettingsPath = Join-Path $PSScriptRoot "..\config\settings.json"
$GlobalSettings = $null
if (Test-Path $SettingsPath) {
    try {
        $GlobalSettings = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json
    } catch {
        Write-Warning "Failed to load settings.json"
    }
}

$DistroCatalog = [ordered]@{}
# Reconstruct ordered dictionary
$Keys = $ConfigRaw.PSObject.Properties.Name | Sort-Object { [int]$_ }
foreach ($Key in $Keys) {
    $FamObj = $ConfigRaw.$Key
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
    if ($SelectFamily -and $FamilyKey -ne $SelectFamily) { continue }

    $Family = $DistroCatalog[$FamilyKey]
    
    foreach ($VerKey in $Family.Versions.Keys) {
        if ($SelectVersion -and $VerKey -ne $SelectVersion) { continue }

        $Version = $Family.Versions[$VerKey]
        
        # Organization: distro/FamilyName/VersionName/File
        $TargetDir = Join-Path $BaseDir $Family.Name
        $TargetDir = Join-Path $TargetDir $Version.Name
        
        if (-not (Test-Path $TargetDir)) {
            New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        }

        $OutFile = Join-Path $TargetDir $Version.Filename
        
        Write-Host "[$($Family.Name)] $($Version.Name)" -NoNewline

        $FileExists = Test-Path $OutFile
        if ($FileExists) {
            Write-Host " -> Skipped (Already exists)" -ForegroundColor Yellow
        } else {
            Write-Host " -> Downloading..." -ForegroundColor Green
            try {
                Invoke-WebRequest -Uri $Version.Url -OutFile $OutFile -UseBasicParsing -Verbose
                $FileExists = $true
            } catch {
                Write-Host " -> Failed: $_" -ForegroundColor Red
            }
        }

        # Update Config if path changed or is new
        if ($FileExists) {
            $CurrentSavedPath = $ConfigRaw.$FamilyKey.Versions.$VerKey.LocalPath
            if ($CurrentSavedPath -ne $OutFile) {
                # We need to assign to the PSObject
                $ConfigRaw.$FamilyKey.Versions.$VerKey | Add-Member -MemberType NoteProperty -Name "LocalPath" -Value $OutFile -Force
                $ConfigChanged = $true
            }
        }
    }
}

if ($ConfigChanged) {
    Write-Host "Updating configuration file with local paths..." -ForegroundColor DarkCyan
    $JsonOutput = $ConfigRaw | ConvertTo-Json -Depth 6
    if ($JsonOutput) {
        Set-Content -Path $ConfigPath -Value $JsonOutput -Encoding UTF8
    }
}
