# PowerShell script to move a WSL instance to a new location
# Usage: ./move_instance.ps1 -DistroName "Ubuntu" -NewPath "D:\WSL\Ubuntu"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName,

    [Parameter(Mandatory=$true)]
    [string]$NewPath
)

$ErrorActionPreference = "Stop"

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "move.log"

# Use absolute path
$NewPath = [System.IO.Path]::GetFullPath($NewPath)

# Get list of distros, trim whitespace, and filter empty lines
# This handles potential encoding issues with wsl output
$rawOutput = wsl --list --quiet
$distros = $rawOutput | ForEach-Object { $_.Trim() -replace "`0", "" } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($distros -notcontains $DistroName) {
    $msg = "WSL instance '$DistroName' not found. Available: $($distros -join ', ')"
    Log-Message $msg "ERROR"
    Write-Error $msg
    exit 1
}

if (-not (Test-Path $NewPath)) {
    New-Item -ItemType Directory -Force -Path $NewPath | Out-Null
} else {
    # Check if empty
    if ((Get-ChildItem $NewPath).Count -gt 0) {
        $warnMsg = "Target directory '$NewPath' is not empty."
        Log-Message $warnMsg "WARN"
        Write-Warning $warnMsg
        $Confirm = Read-Host "Continue? (y/n)"
        if ($Confirm -ne 'y') { exit }
    }
}

Log-Message "Moving '$DistroName' to '$NewPath'..."

# Prepare Temp File
$TempExport = Join-Path $NewPath "export_temp.tar"

try {
    # 1. Export
    Log-Message "Exporting instance (this may take time)..."
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
    Log-Message "Unregistering old instance..."
    wsl --unregister $DistroName

    # 4. Import to new location
    Log-Message "Importing to new location..."
    wsl --import $DistroName $NewPath $TempExport --version 2

    # 5. Restore Config (User)
    # Import usually resets default user to root in registry, need to check inside distro?
    # Actually, --import doesn't preserve /etc/wsl.conf if it's inside the tar? Yes it does.
    # But wsl executable metadata for "default user" is lost.
    if ($User -ne "root") {
        Log-Message "Restoring default user to '$User'..."
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

    Log-Message "Move complete."

} catch {
    $err = "Move failed: $_"
    Log-Message $err "ERROR"
    Write-Error $err
    if (Test-Path $TempExport) {
        Log-Message "A temporary export exists at: $TempExport" "WARN"
        Write-Warning "A temporary export exists at: $TempExport"
    }
    exit 1
}
