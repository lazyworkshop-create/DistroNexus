# PowerShell script to scan active WSL instances and update the registry
# Usage: ./scan_wsl_instances.ps1

$ErrorActionPreference = "Continue" # Don't stop on single read error
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "Scanning WSL instances..." -ForegroundColor Cyan

$LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
$ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"

# 1. Get Running State
$WslStatus = @{}
$cliOutput = wsl --list --verbose
if ($cliOutput) {
    foreach ($line in $cliOutput) {
        $line = $line -replace "`0", ""
        if ($line -match "NAME") { continue }
        $cleanArgs = $line.Replace("*", " ").Trim() -split "\s+"
        if ($cleanArgs.Count -ge 3) {
                $n = $cleanArgs[0]
                $WslStatus[$n] = @{
                    State = $cleanArgs[1]
                    Version = $cleanArgs[2]
                }
        }
    }
}

$Distros = @()
if (Test-Path $LxssPath) {
    $Keys = Get-ChildItem -Path $LxssPath
    foreach ($Key in $Keys) {
        $Props = Get-ItemProperty -Path $Key.PSPath
        $Name = $Props.DistributionName
        if (-not $Name) { continue }
        
        $BasePath = $Props.BasePath
        
        # Filesystem Info
        $InstallTime = "Unknown"
        if ($BasePath -and (Test-Path $BasePath)) {
            try {
                $DirInfo = Get-Item $BasePath
                $InstallTime = $DirInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
            } catch {}
        }

        # Status
        $State = "Stopped"
        $WslVer = "?"
        if ($WslStatus.Contains($Name)) {
            $State = $WslStatus[$Name].State
            $WslVer = $WslStatus[$Name].Version
        }

        # Try to infer OS Release
        $Release = "Unknown"
        # Reading from existing JSON to preserve "Release" field if possible (avoid waking up distros)
        # OR assume we want a fresh scan. 
        # A full scan implies we might accept "Unknown" rather than waking everything up.
        # Let's try to read previous JSON to cache the "Release" name.
        
        $Distros += @{
            Name = $Name
            BasePath = $BasePath
            State = $State
            WslVer = $WslVer
            Release = $Release # Placeholder, rectified below
            User = "root"      # Placeholder, rectified below
            InstallTime = $InstallTime
        }
    }
}

# 2. Merge with existing config to preserve manual metadata (Release, User)
$ExistingData = @()
if (Test-Path $ConfigPath) {
    try {
        $ExistingData = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        if ($ExistingData -isnot [System.Array]) { $ExistingData = @($ExistingData) }
    } catch {}
}

$FinalList = @()
foreach ($d in $Distros) {
    $Match = $ExistingData | Where-Object { $_.Name -eq $d.Name } | Select-Object -First 1
    
    if ($Match) {
        if ($Match.Release) { $d.Release = $Match.Release }
        if ($Match.User) { $d.User = $Match.User }
    }
    
    # If still unknown, maybe categorize by name or leave as Unknown
    if ($d.Release -eq "Unknown") {
        if ($d.Name -match "Ubuntu") { $d.Release = "Ubuntu" }
        elseif ($d.Name -match "Debian") { $d.Release = "Debian" }
    }

    $FinalList += $d
}

$FinalList | ConvertTo-Json -Depth 4 | Set-Content $ConfigPath -Force
Write-Host "Registry updated with $($FinalList.Count) instances." -ForegroundColor Green
