# PowerShell script to interactively list and uninstall WSL2 instances
# Author: GitHub Copilot

param (
    [string]$DistroName,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "uninstall.log"

# Use UTF-8 for output
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Get-WslDistros {
    $LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
    if (-not (Test-Path $LxssPath)) { return @() }
    
    # 1. Get Running State and Version from wsl -l -v
    # Note: Output encoding of wsl.exe can be tricky. We treat it as string array.
    $WslStatus = @{}
    $cliOutput = wsl --list --verbose
    if ($cliOutput) {
        foreach ($line in $cliOutput) {
            # Trim nulls/spaces
            $line = $line -replace "`0", "" 
            if ($line -match "NAME") { continue }
            
            # parts: [*] Name State Version
            # Remove leading * if default
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
                 $InstallTime = $DirInfo.CreationTime.ToString("yyyy-MM-dd HH:mm")
             } catch {}
        }

        # Status Info
        $State = "Stopped"
        $WslVer = "?"
        if ($WslStatus.Contains($Name)) {
            $State = $WslStatus[$Name].State
            $WslVer = $WslStatus[$Name].Version
        }

        # Attempt to get OS Release info (PRETTY_NAME)
        # Warning: This starts the distro if stopped.
        $OsName = "Unknown Distro"
        try {
            # Use 'cat' directly to avoid shell compatibility issues (e.g., 'source' not found in dash/Ubuntu)
            # and parse the output in PowerShell.
            $osReleaseContent = wsl -d $Name -u root -e cat /etc/os-release 2>$null
            
            if ($osReleaseContent) {
                # Look for PRETTY_NAME="..."
                foreach ($line in $osReleaseContent) {
                    if ($line -match '^PRETTY_NAME=') {
                        # Remove key and quotes
                        $OsName = $line -replace '^PRETTY_NAME=', '' -replace '"', ''
                        break
                    }
                }
            }
        } catch {
            $OsName = "Read Error"
        }
        
        $Distros += [PSCustomObject]@{
            Name        = $Name
            BasePath    = $BasePath
            InstallTime = $InstallTime
            State       = $State
            WslVer      = $WslVer
            OsName      = $OsName
        }
    }
    return $Distros
}

Log-Message "Scanning for WSL distributions..."
$Available = Get-WslDistros

if ($Available.Count -eq 0) {
    Log-Message "No WSL distributions found." "WARN"
    exit 0
}

$Target = $null

if ($DistroName) {
    $Target = $Available | Where-Object { $_.Name -eq $DistroName } | Select-Object -First 1
    if (-not $Target) {
        $msg = "Distribution '$DistroName' not found."
        Log-Message $msg "ERROR"
        Write-Error $msg
        exit 1
    }
} else {
    # --- 1. List Instances ---
    Write-Host "`n=== Installed WSL Distributions ===" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Available.Count; $i++) {
        $d = $Available[$i]
        
        # Line 1: [Index] Name (OsName) | State | WSLvX
        $StateColor = if ($d.State -eq 'Running') { "Green" } else { "Gray" }
        
        Write-Host "[$($i+1)] " -NoNewline -ForegroundColor Cyan
        Write-Host "$($d.Name) " -NoNewline -ForegroundColor White
        Write-Host "($($d.OsName)) " -NoNewline -ForegroundColor Yellow
        Write-Host "| $($d.State) | WSL$($d.WslVer)" -ForegroundColor $StateColor
        
        # Line 2: Path, Installed
        Write-Host "      Path: $($d.BasePath)" -ForegroundColor DarkGray
    }

    # --- 2. Select Instance ---
    do {
        $Selection = Read-Host "Select a number to uninstall (1-$($Available.Count)) [q to quit]"
        if ($Selection -eq 'q') { exit 0 }
    } until ($Selection -match "^\d+$" -and [int]$Selection -ge 1 -and [int]$Selection -le $Available.Count)

    $Target = $Available[[int]$Selection - 1]
}

# --- 3. Confirm Uninstall (Unregister) ---
Log-Message "`n[WARNING] You are about to UNREGISTER the following distribution:"
Log-Message "  Name: $($Target.Name)"
Log-Message "  Path: $($Target.BasePath)"
Log-Message "This will remove the registration from WSL."

if (-not $Force) {
    $Confirm = Read-Host "Are you sure? (Type 'yes' to confirm)"
    if ($Confirm -ne 'yes') {
        Log-Message "Operation cancelled." "WARN"
        exit 0
    }
}

Log-Message "Unregistering $($Target.Name)..."
try {
    wsl --unregister $Target.Name
    Log-Message "Unregistered successfully."
} catch {
    Log-Message "Failed to unregister: $_" "ERROR"
    Write-Error "Failed to unregister: $_"
    # Proceed to file deletion? Maybe ask user.
}

# --- 4. Confirm Delete Files ---
if ($Target.BasePath -and (Test-Path $Target.BasePath)) {
    # Check if files remain
    $RemainingItems = Get-ChildItem -Path $Target.BasePath -Force -ErrorAction SilentlyContinue
    if ($RemainingItems) {
         Log-Message "`nThe directory '$($Target.BasePath)' still exists and is not empty." "WARN"
         Log-Message "It may contain user data or the disk image if unregistering failed to clean it up."
         
         if ($Force) {
             $ConfirmDelete = 'delete'
         } else {
             $ConfirmDelete = Read-Host "Do you want to permanently DELETE this folder and all contents? (Type 'delete' to confirm)"
         }

         if ($ConfirmDelete -eq 'delete') {
             Log-Message "Deleting files..."
             try {
                Remove-Item -Path $Target.BasePath -Recurse -Force -ErrorAction Stop
                Log-Message "Folder deleted."
             } catch {
                Log-Message "Failed to delete folder: $_" "ERROR"
                Write-Error "Failed to delete folder: $_"
             }
         } else {
             Log-Message "Folder preserved at $($Target.BasePath)"
         }
    } else {
        # Directory is empty, maybe just remove it
        Log-Message "Removing empty directory $($Target.BasePath)..."
        Remove-Item -Path $Target.BasePath -Force -ErrorAction SilentlyContinue
    }
} else {
    Log-Message "Install location is already gone."
}

# --- 5. Update Instances Configuration ---
$InstancesConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
if (Test-Path $InstancesConfigPath) {
    try {
        $CurrentInstances = Get-Content $InstancesConfigPath -Raw | ConvertFrom-Json
        # Handle single object vs array
        if ($CurrentInstances -isnot [System.Array]) { 
             if ($CurrentInstances) { $CurrentInstances = @($CurrentInstances) }
             else { $CurrentInstances = @() }
        }

        # Filter out the removed instance
        $UpdatedInstances = $CurrentInstances | Where-Object { $_.Name -ne $Target.Name }
        
        $UpdatedInstances | ConvertTo-Json -Depth 4 | Set-Content $InstancesConfigPath -Force
        Log-Message "Updated instances registry (removed '$($Target.Name)')."
    } catch {
        Log-Message "Failed to update instances.json: $_" "WARN"
        Write-Warning "Failed to update instances.json: $_"
    }
}

Log-Message "`nUninstall process complete."
