# PowerShell script to terminate a WSL instance
# Usage: ./stop_instance.ps1 -DistroName "Ubuntu-24.04"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName
)

$ErrorActionPreference = "Stop"

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "stop.log"

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

Log-Message "Stopping WSL instance '$DistroName'..."
try {
    wsl --terminate $DistroName
    
    # Update State in instances.json
    $ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
    if (Test-Path $ConfigPath) {
        $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $Instance = $Json | Where-Object { $_.Name -eq $DistroName }
        if ($Instance) {
            $Instance.State = "Stopped"
            $Json | ConvertTo-Json -Depth 4 | Set-Content $ConfigPath -Force
        }
    }

    Log-Message "Instance '$DistroName' stopped successfully."
} catch {
    $err = "Failed to stop instance: $_"
    Log-Message $err "ERROR"
    Write-Error $err
    exit 1
}
