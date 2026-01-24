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

if (-not (wsl --list --quiet | Select-String -Pattern "^$OldName$")) {
    Write-Error "Source instance '$OldName' not found."
    exit 1
}

# Check if NewName exists
if (wsl --list --quiet | Select-String -Pattern "^$NewName$") {
    Write-Error "Target name '$NewName' already exists."
    exit 1
}

if ($NewPath) {
    $TargetDir = [System.IO.Path]::GetFullPath($NewPath)
} else {
    # If no path specified, try to stay in current parent dir but rename folder?
    # Or just use current location?
    # Safer: Require NewPath or imply same location? 
    # If we import inplace, files clash.
    # Let's assume we want to create a new folder for the new name if not specified.
    
    # Get Old Path
    $LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
    $Keys = Get-ChildItem -Path $LxssPath
    $OldPath = $null
    foreach ($Key in $Keys) {
        $Props = Get-ItemProperty -Path $Key.PSPath
        if ($Props.DistributionName -eq $OldName) {
            $OldPath = $Props.BasePath
            break
        }
    }
    
    if (-not $OldPath) { throw "Could not determine path for $OldName" }
    
    # sibling folder
    $Parent = Split-Path $OldPath -Parent
    $TargetDir = Join-Path $Parent $NewName
}

if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
}

Write-Host "Renaming '$OldName' -> '$NewName'..." -ForegroundColor Cyan
Write-Host "Location: $TargetDir" -ForegroundColor Gray

# Prepare Temp File (outside target to avoid conflicts if same dir?)
$TempExport = Join-Path $env:TEMP "${OldName}_export.tar"

try {
    # 1. Export
    Write-Host "Exporting..." -ForegroundColor Gray
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
    Write-Host "Unregistering old..." -ForegroundColor Gray
    wsl --unregister $OldName
    
    # Optional: If old path is empty and different, remove it? 
    # Not doing it automatically to be safe.

    # 4. Import New
    Write-Host "Importing as '$NewName'..." -ForegroundColor Gray
    wsl --import $NewName $TargetDir $TempExport --version 2

    # 5. Restore Metadata logic
    if ($User -ne "root") {
         wsl -d $NewName -u root -- sh -c "echo '[user]\ndefault=$User' > /etc/wsl.conf"
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
    
    Write-Host "Rename complete." -ForegroundColor Green

} catch {
    Write-Error "Rename failed: $_"
    if (Test-Path $TempExport) { Remove-Item $TempExport -ErrorAction SilentlyContinue }
    exit 1
}
