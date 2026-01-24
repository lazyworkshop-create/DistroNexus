# PowerShell script to rename a WSL instance
# Usage: ./rename_instance.ps1 -OldName "Ubuntu" -NewName "Ubuntu-Dev" [-NewPath "D:\..."]

param(
    [Parameter(Mandatory=$true)]
    [string]$OldName,

    [Parameter(Mandatory=$true)]
    [string]$NewName,

    [string]$NewPath
)

$ErrorActionPreference = "Stop"

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "rename.log"

# Get list of distros, trim whitespace, and filter empty lines
$rawOutput = wsl --list --quiet
$distros = $rawOutput | ForEach-Object { $_.Trim() -replace "`0", "" } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($distros -notcontains $OldName) {
    $msg = "Source instance '$OldName' not found. Available: $($distros -join ', ')"
    Log-Message $msg "ERROR"
    Write-Error $msg
    exit 1
}

# Check if NewName exists
if ($distros -contains $NewName) {
    $msg = "Target name '$NewName' already exists."
    Log-Message $msg "ERROR"
    Write-Error $msg
    exit 1
}

if ($NewPath) {
    $TargetDir = [System.IO.Path]::GetFullPath($NewPath)
} else {
    # If no path specified, try to stay in current parent dir but rename folder?
    
    # 1. Try to find BasePath from instances.json first (preferred)
    $ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
    $OldPath = $null
    
    if (Test-Path $ConfigPath) {
        try {
            $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
            if ($Json -isnot [System.Array]) { $Json = @($Json) }
            $Found = $Json | Where-Object { $_.Name -eq $OldName }
            if ($Found) { $OldPath = $Found.BasePath }
        } catch {
             Log-Message "Failed to read instances.json for path lookup." "WARN"
             Write-Warning "Failed to read instances.json for path lookup."
        }
    }

    # 2. Fallback to Registry if not found
    if (-not $OldPath) {
        Log-Message "Searching registry for instance location..."
        $LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
        if (Test-Path $LxssPath) {
            $Keys = Get-ChildItem -Path $LxssPath
            foreach ($Key in $Keys) {
                $Props = Get-ItemProperty -Path $Key.PSPath
                if ($Props.DistributionName -eq $OldName) {
                    $OldPath = $Props.BasePath
                    break
                }
            }
        }
    }
    
    if (-not $OldPath) { throw "Could not determine installation path for '$OldName'. Please specify -NewPath manually." }
    
    # sibling folder
    $Parent = Split-Path $OldPath -Parent
    $TargetDir = Join-Path $Parent $NewName
}

if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
}

Log-Message "Renaming '$OldName' -> '$NewName'..."
Log-Message "Location: $TargetDir"

# Prepare Temp File (outside target to avoid conflicts if same dir?)
$TempExport = Join-Path $env:TEMP "${OldName}_export.tar"

try {
    # 1. Export
    Log-Message "Exporting..."
    wsl --terminate $OldName
    wsl --export $OldName $TempExport

    # 2. Get Metadata
    $ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
    $User = "root"
    $Release = "Custom"
    if (Test-Path $ConfigPath) {
        $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $Instance = $Json | Where-Object { $_.Name -eq $OldName }
        if ($Instance) { 
             if ($Instance.User) { $User = $Instance.User }
             if ($Instance.Release) { $Release = $Instance.Release }
        }
    }

    # 3. Unregister Old
    Log-Message "Unregistering old..."
    wsl --unregister $OldName
    
    # Optional: If old path is empty and different, remove it? 
    # Not doing it automatically to be safe.

    # 4. Import New
    Log-Message "Importing as '$NewName'..."
    wsl --import $NewName $TargetDir $TempExport --version 2

    # 5. Restore Metadata logic
    if ($User -ne "root") {
         # Use printf for reliable newline handling in /etc/wsl.conf
         wsl -d $NewName -u root -- sh -c "printf '[user]\ndefault=$User\n' > /etc/wsl.conf"
    }

    # 6. Update Json Manually (instead of scan, to preserve Release name immediately)
    # Actually scan is safer, but scan loses "Release" if not cached.
    # We should update config to map OldName -> NewName
    
    if (Test-Path $ConfigPath) {
        $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        if ($Json -isnot [System.Array]) { $Json = @($Json) }
        
        # Remove Old
        $Json = $Json | Where-Object { $_.Name -ne $OldName }
        
        # Add New (We construct it manually to ensure Release/User is kept)
        $NewObj = @{
            Name = $NewName
            BasePath = $TargetDir
            State = "Stopped"
            WslVer = "2"
            Release = $Release
            User = $User
            InstallTime = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        }
        $Json += $NewObj
        
        $Json | ConvertTo-Json -Depth 4 | Set-Content $ConfigPath -Force
    }
    
    # Cleanup
    Remove-Item $TempExport -Force
    
    Log-Message "Rename complete."

} catch {
    $err = "Rename failed: $_"
    Log-Message $err "ERROR"
    Write-Error $err
    if (Test-Path $TempExport) { Remove-Item $TempExport -ErrorAction SilentlyContinue }
    exit 1
}
