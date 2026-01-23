# PowerShell script to interactively list and uninstall WSL2 instances
# Author: GitHub Copilot

$ErrorActionPreference = "Stop"

# Use UTF-8 for output
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Get-WslDistros {
    $LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
    if (-not (Test-Path $LxssPath)) { return @() }

    $Distros = @()
    $Keys = Get-ChildItem -Path $LxssPath

    foreach ($Key in $Keys) {
        $Props = Get-ItemProperty -Path $Key.PSPath
        
        $Name = $Props.DistributionName
        if (-not $Name) { continue }
        
        $BasePath = $Props.BasePath
        
        # Attempt to get real filesystem info
        $InstallTime = "Unknown"
        $LastUsedTime = "Unknown"
        
        if ($BasePath -and (Test-Path $BasePath)) {
             try {
                 $DirInfo = Get-Item $BasePath
                 $InstallTime = $DirInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                 
                 $VhdxPath = Join-Path $BasePath "ext4.vhdx"
                 if (Test-Path $VhdxPath) {
                     $FileInfo = Get-Item $VhdxPath
                     $LastUsedTime = $FileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                 }
             } catch {
                 # Ignore permission errors etc
             }
        }

        # Attempt to identify "Distro" type (heuristic based on Name usually, as Registry doesn't strictly store 'Ubuntu' vs 'Debian' metadata cleanly aside from sometimes PackageFamilyName for Store apps)
        # For our custom distros, we don't store "Distro" type in registry, so we just return the name. 
        
        $Distros += [PSCustomObject]@{
            Name        = $Name
            BasePath    = $BasePath
            InstallTime = $InstallTime
            LastUsed    = $LastUsedTime
        }
    }
    return $Distros
}

Write-Host "Scanning for WSL distributions..." -ForegroundColor Gray
$Available = Get-WslDistros

if ($Available.Count -eq 0) {
    Write-Host "No WSL distributions found." -ForegroundColor Yellow
    exit 0
}

# --- 1. List Instances ---
Write-Host "`n=== Installed WSL Distributions ===" -ForegroundColor Cyan
for ($i = 0; $i -lt $Available.Count; $i++) {
    $d = $Available[$i]
    Write-Host "[$($i+1)] $($d.Name)" -ForegroundColor Green
    Write-Host "    Location:  $($d.BasePath)"
    Write-Host "    Installed: $($d.InstallTime)"
    Write-Host "    Last Used: $($d.LastUsed)"
    Write-Host ""
}

# --- 2. Select Instance ---
do {
    $Selection = Read-Host "Select a number to uninstall (1-$($Available.Count)) [q to quit]"
    if ($Selection -eq 'q') { exit 0 }
} until ($Selection -match "^\d+$" -and [int]$Selection -ge 1 -and [int]$Selection -le $Available.Count)

$Target = $Available[[int]$Selection - 1]

# --- 3. Confirm Uninstall (Unregister) ---
Write-Host "`n[WARNING] You are about to UNREGISTER the following distribution:" -ForegroundColor Yellow
Write-Host "  Name: $($Target.Name)"
Write-Host "  Path: $($Target.BasePath)"
Write-Host "This will remove the registration from WSL."
$Confirm = Read-Host "Are you sure? (Type 'yes' to confirm)"

if ($Confirm -ne 'yes') {
    Write-Host "Operation cancelled." -ForegroundColor Grill
    exit 0
}

Write-Host "Unregistering $($Target.Name)..." -ForegroundColor Cyan
try {
    wsl --unregister $Target.Name
    Write-Host "Unregistered successfully." -ForegroundColor Green
} catch {
    Write-Error "Failed to unregister: $_"
    # Proceed to file deletion? Maybe ask user.
}

# --- 4. Confirm Delete Files ---
if ($Target.BasePath -and (Test-Path $Target.BasePath)) {
    # Check if files remain
    $RemainingItems = Get-ChildItem -Path $Target.BasePath -Force -ErrorAction SilentlyContinue
    if ($RemainingItems) {
         Write-Host "`nThe directory '$($Target.BasePath)' still exists and is not empty." -ForegroundColor Yellow
         Write-Host "It may contain user data or the disk image if unregistering failed to clean it up."
         
         $ConfirmDelete = Read-Host "Do you want to permanently DELETE this folder and all contents? (Type 'delete' to confirm)"
         if ($ConfirmDelete -eq 'delete') {
             Write-Host "Deleting files..." -ForegroundColor Cyan
             try {
                Remove-Item -Path $Target.BasePath -Recurse -Force -ErrorAction Stop
                Write-Host "Folder deleted." -ForegroundColor Green
             } catch {
                Write-Error "Failed to delete folder: $_"
             }
         } else {
             Write-Host "Folder preserved at $($Target.BasePath)" -ForegroundColor Gray
         }
    } else {
        # Directory is empty, maybe just remove it
        Write-Host "Removing empty directory $($Target.BasePath)..." -ForegroundColor Gray
        Remove-Item -Path $Target.BasePath -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "Install location is already gone." -ForegroundColor Gray
}

Write-Host "`nUninstall process complete."
