# PowerShell script to move a WSL instance to a new location
# Usage: ./move_instance.ps1 -DistroName "Ubuntu" -NewPath "D:\WSL\Ubuntu"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName,

    [Parameter(Mandatory=$true)]
    [string]$NewPath
)

$ErrorActionPreference = "Stop"

# Use absolute path
$NewPath = [System.IO.Path]::GetFullPath($NewPath)

if (-not (wsl --list --quiet | Select-String -Pattern "^$DistroName$")) {
    Write-Error "WSL instance '$DistroName' not found."
    exit 1
}

if (-not (Test-Path $NewPath)) {
    New-Item -ItemType Directory -Force -Path $NewPath | Out-Null
} else {
    # Check if empty
    if ((Get-ChildItem $NewPath).Count -gt 0) {
        Write-Warning "Target directory '$NewPath' is not empty."
        $Confirm = Read-Host "Continue? (y/n)"
        if ($Confirm -ne 'y') { exit }
    }
}

Write-Host "Moving '$DistroName' to '$NewPath'..." -ForegroundColor Cyan

# Prepare Temp File
$TempExport = Join-Path $NewPath "export_temp.tar"

try {
    # 1. Export
    Write-Host "Exporting instance (this may take time)..." -ForegroundColor Gray
    wsl --terminate $DistroName
    wsl --export $DistroName $TempExport

    if (-not (Test-Path $TempExport)) {
        throw "Export Failed."
    }

    # 2. Get User Info before destroying (if possible from config)
    $ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
    $User = "root"
    if (Test-Path $ConfigPath) {
        $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $Instance = $Json | Where-Object { $_.Name -eq $DistroName }
        if ($Instance -and $Instance.User) { $User = $Instance.User }
    }

    # 3. Unregister
    Write-Host "Unregistering old instance..." -ForegroundColor Gray
    wsl --unregister $DistroName

    # 4. Import to new location
    Write-Host "Importing to new location..." -ForegroundColor Gray
    wsl --import $DistroName $NewPath $TempExport --version 2

    # 5. Restore Config (User)
    # Import usually resets default user to root in registry, need to check inside distro?
    # Actually, --import doesn't preserve /etc/wsl.conf if it's inside the tar? Yes it does.
    # But wsl executable metadata for "default user" is lost.
    if ($User -ne "root") {
        Write-Host "Restoring default user to '$User'..." -ForegroundColor Gray
        # We assume /etc/wsl.conf is inside the tar.
        # But we might need to nudge registry?
        # Usually handled by `scan_wsl_instances` or manual usage.
        # Let's try to ensure it via config injection just in case.
         wsl -d $DistroName -u root -- sh -c "echo '[user]\ndefault=$User' > /etc/wsl.conf"
    }

    # 6. Cleanup
    Remove-Item $TempExport -Force
    
    # 7. Update Registry
    & "$PSScriptRoot\scan_wsl_instances.ps1"

    Write-Host "Move complete." -ForegroundColor Green

} catch {
    Write-Error "Move failed: $_"
    if (Test-Path $TempExport) {
        Write-Warning "A temporary export exists at: $TempExport"
    }
    exit 1
}
