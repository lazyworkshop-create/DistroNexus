# PowerShell Script to Update DistroNexus Configuration from Microsoft Official Source
# Usage: .\update_distros.ps1

param(
    [string]$SourceUrl
)

$ErrorActionPreference = "Stop"

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "update.log"

# --- Configuration ---
$DefaultUrl = "https://raw.githubusercontent.com/microsoft/WSL/master/distributions/DistributionInfo.json"
if (-not $SourceUrl) { $SourceUrl = $DefaultUrl }

$ScriptDir = $PSScriptRoot
# Navigate up from tools/ to config/
$ConfigDir = Join-Path $ScriptDir "..\config"
$OutputPath = Join-Path $ConfigDir "distros.json"

Log-Message "=== DistroNexus Update Tool (PowerShell) ==="
Log-Message "Fetching latest distribution info from: $SourceUrl"

# 0. Load Existing Config for Preservation
$ExistingConfig = $null
if (Test-Path $OutputPath) {
    try {
        $ExistingConfig = Get-Content -Raw -Path $OutputPath | ConvertFrom-Json
    } catch {
        Log-Message "Could not load existing config for preservation." "WARN"
        Write-Warning "Could not load existing config for preservation."
    }
}

# 1. Fetch JSON
try {
    $JsonContent = Invoke-RestMethod -Uri $SourceUrl -Method Get
} catch {
    $err = "Failed to fetch distribution info: $_"
    Log-Message $err "ERROR"
    Write-Error $err
    exit 1
}

Log-Message "Found $($JsonContent.ModernDistributions.PSObject.Properties.Count) distribution families."

# 2. Convert to DistroNexus Format
$NexusData = [ordered]@{}
$FamilyCounter = 1

foreach ($FamilyName in $JsonContent.ModernDistributions.PSObject.Properties.Name) {
    $Versions = $JsonContent.ModernDistributions.$FamilyName
    
    # Nexus Naming Normalization
    $NexusFamilyName = $FamilyName
    if ($FamilyName -eq "kali") { $NexusFamilyName = "Kali" }
    elseif ($FamilyName -eq "DistroName") { continue } # Skip template/example

    $NexusVersions = [ordered]@{}
    $Counter = 1

    foreach ($Ver in $Versions) {
        # Check AMD64 URL
        $AmdUrl = $Ver.Amd64Url.Url
        if ([string]::IsNullOrEmpty($AmdUrl)) { continue }

        # Extract Filename
        $Filename = $AmdUrl.Split('/')[-1].Split('?')[0]

        # Generate Default Name (clean non-alphanumeric)
        $DefaultName = $Ver.Name -replace '[^a-zA-Z0-9\-\.]', ''

        # Try to find existing entry to preserve LocalPath
        $LocalPath = $null
        if ($ExistingConfig) {
             # Naive search: Match by Filename AND Url
             # Since structure changed to ID-based, we iterate deeply
             foreach ($eFamKey in $ExistingConfig.PSObject.Properties.Name) {
                 $eFam = $ExistingConfig.$eFamKey
                 # Iterate versions
                 foreach ($eVerKey in $eFam.Versions.PSObject.Properties.Name) {
                     $eVer = $eFam.Versions.$eVerKey
                     if ($eVer.Url -eq $AmdUrl -or $eVer.Filename -eq $Filename) {
                         if ($eVer.LocalPath) {
                             $LocalPath = $eVer.LocalPath
                         }
                     }
                 }
             }
        }

        $NexusVer = [ordered]@{
            Name        = $Ver.FriendlyName
            Url         = $AmdUrl
            DefaultName = $DefaultName
            Filename    = $Filename
            Source      = "Official"
        }
        
        if ($LocalPath) {
            $NexusVer["LocalPath"] = $LocalPath
        }

        $NexusVersions["$Counter"] = $NexusVer
        $Counter++
    }

    if ($NexusVersions.Count -gt 0) {
        $NexusData["$FamilyCounter"] = [ordered]@{
            Name     = $NexusFamilyName
            Versions = $NexusVersions
        }
        $FamilyCounter++
    }
}

# 3. Save Output
if (-not (Test-Path $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null }

# Backup
if (Test-Path $OutputPath) {
    $Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $BackupPath = "$OutputPath.$Timestamp.bak"
    Copy-Item -Path $OutputPath -Destination $BackupPath -Force
    Log-Message "Backed up existing config to $BackupPath"
}

$OutputJson = $NexusData | ConvertTo-Json -Depth 4
Set-Content -Path $OutputPath -Value $OutputJson -Encoding UTF8

Log-Message "Successfully updated $OutputPath with $($NexusData.Count) families."

Write-Host "Successfully updated $OutputPath with $($NexusData.Count) families."
