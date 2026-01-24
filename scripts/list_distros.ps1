# PowerShell script to list WSL instances as JSON
# Used by GUI to populate Uninstall list

param (
    [switch]$ForceUpdate
)

$ErrorActionPreference = "Stop"
# Ensure UTF-8 output for JSON
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "list.log"
Log-Message "Starting list generation" -FileOnly

# --- Configuration ---
$ConfigDir = Join-Path $PSScriptRoot "..\\config"
if (-not (Test-Path $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null }
$CacheFile = Join-Path $ConfigDir "instances.json" 

# --- Functions ---

function Get-WslDistros {
    $LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
    if (-not (Test-Path $LxssPath)) { return @() }
    
    # 1. Get Running State and Version from wsl --list --verbose
    # This is fast and always needed for current state
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

    # 2. Load Cache if exists
    $Cache = @{}
    if (Test-Path $CacheFile) {
        try {
            $CacheData = Get-Content $CacheFile -Raw -Encoding UTF8 | ConvertFrom-Json
            # Handle Single Object vs Array
            if ($CacheData -is [PSCustomObject]) { $CacheData = @($CacheData) }
            
            foreach ($c in $CacheData) {
                if ($c.Name) { $Cache[$c.Name] = $c }
            }
        } catch {
             Log-Message "Failed to load cache: $_" -Level WARN -FileOnly
        }
    }

    $CurrentDistros = @()
    $Keys = Get-ChildItem -Path $LxssPath

    $UpdatedCache = @{}
    $CacheChanged = $false

    foreach ($Key in $Keys) {
        $Props = Get-ItemProperty -Path $Key.PSPath
        
        $Name = $Props.DistributionName
        if (-not $Name) { continue }
        
        $BasePath = $Props.BasePath
        
        # Determine Status
        $State = "Stopped"
        $WslVer = "?"
        if ($WslStatus.Contains($Name)) {
            $State = $WslStatus[$Name].State
            $WslVer = $WslStatus[$Name].Version
        }
        
        # --- Cache Logic ---
        $CachedItem = $Cache[$Name]
        $IsNew = ($null -eq $CachedItem)
        
        $Release = ""
        $User = ""
        $InstallTime = ""
        
        if (-not $IsNew) {
            # Use cached values
            $Release = $CachedItem.Release
            $User = $CachedItem.User
            $InstallTime = $CachedItem.InstallTime
        }

        # Initialize InstallTime if missing
        if ([string]::IsNullOrEmpty($InstallTime)) {
            if (Test-Path $BasePath) {
                 try {
                    $InstallTime = (Get-Item $BasePath).CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                 } catch {
                    $InstallTime = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
                 }
            } else {
                 $InstallTime = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
            }
            $CacheChanged = $true
        }

        # Force Update / Discovery Logic
        # We try to fetch info if:
        # 1. ForceUpdate is requested
        # 2. It's a new instance (first discovery)
        # 3. Missing critical info in cache (Release empty)
        $ShouldFetch = $ForceUpdate -or $IsNew -or ([string]::IsNullOrEmpty($Release))

        # User Request: Avoid auto-starting instances to fetch info. 
        # Information like OS Release or User is static mostly.
        # We only fetch if the instance is CURRENTLY RUNNING.
        if ($ShouldFetch -and ($State -eq "Running")) {
            
            # Get Release
            try {
                $osRel = wsl -d $Name cat /etc/os-release 2>$null
                $newRel = ""
                if ($osRel) {
                    foreach ($line in $osRel) {
                       if ($line -match '^PRETTY_NAME="?([^"]+)"?') {
                           $newRel = $matches[1]
                           break
                       }
                    }
                }
                if ($newRel -and $newRel -ne $Release) {
                    $Release = $newRel
                    $CacheChanged = $true
                }
            } catch {}

            # Get User
            if ([string]::IsNullOrEmpty($User) -or $ForceUpdate) {
                 $Uid = $Props.DefaultUid
                 if ($null -eq $Uid) { $Uid = 0 }
                 try {
                    $newUser = wsl -d $Name id -nu $Uid 2>$null
                    if ($newUser -and $newUser -ne $User) { 
                        $User = $newUser 
                        $CacheChanged = $true
                    }
                } catch {
                     # If lookup fails, maybe just "root"
                     if (-not $User) { $User = "root" }
                }
            }
        }
        
        # Get Disk Size
        $DiskSize = "Unknown"
        if ($BasePath -and (Test-Path $BasePath)) {
            # Manual join to avoid 'drive is null' error with \\?\ paths in Join-Path
            $Vhdx = "$BasePath\ext4.vhdx"
            if (Test-Path $Vhdx) {
                $Bytes = (Get-Item $Vhdx).Length
                if ($Bytes -gt 1GB) {
                    $DiskSize = "{0:N2} GB" -f ($Bytes / 1GB)
                } elseif ($Bytes -gt 1MB) {
                    $DiskSize = "{0:N2} MB" -f ($Bytes / 1MB)
                } else {
                    $DiskSize = "{0:N0} KB" -f ($Bytes / 1KB)
                }
            }
        }

        $DistroObj = [ordered]@{
            Name        = $Name
            BasePath    = $BasePath
            State       = $State
            WslVer      = $WslVer
            Release     = $Release
            User        = $User
            InstallTime = $InstallTime
            DiskSize    = $DiskSize
        }
        
        $CurrentDistros += $DistroObj
        $UpdatedCache[$Name] = $DistroObj
    }

    # Detect Deletions (Items in Cache but not in Current Registry)
    if ($Cache.Count -ne $CurrentDistros.Count) {
         # If counts differ, something changed (ignoring strict content check, registry is truth)
         # We rewrite cache based on CurrentDistros (which is registry-derived)
         $CacheChanged = $true
    }
    
    # Save Cache if needed
    if ($CacheChanged) {
        $JsonData = $CurrentDistros | ConvertTo-Json -Depth 2
        Set-Content -Path $CacheFile -Value $JsonData -Encoding UTF8
    }

    return $CurrentDistros
}

$Available = Get-WslDistros
$Available | ConvertTo-Json -Depth 2