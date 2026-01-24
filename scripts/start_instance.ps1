# PowerShell script to start a WSL instance
# Usage: ./start_instance.ps1 -DistroName "Ubuntu"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName
)

$ErrorActionPreference = "Stop"

if (-not (wsl --list --quiet | Select-String -Pattern "^$DistroName$")) {
    Write-Error "WSL instance '$DistroName' not found."
    exit 1
}

# Update State in instances.json to 'Running' (Optimistic)
$ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
if (Test-Path $ConfigPath) {
    try {
        $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $Instance = $Json | Where-Object { $_.Name -eq $DistroName }
        if ($Instance) {
            $Instance.State = "Running"
            $Instance.LastUsed = (Get-Date).ToString("yyyy-MM-dd HH:mm")
            $Json | ConvertTo-Json -Depth 4 | Set-Content $ConfigPath -Force
        }
    } catch {
        Write-Warning "Failed to update registry status: $_"
    }
}

Write-Host "Starting '$DistroName'..." -ForegroundColor Green

# Launch the distro
# This will block until the session is closed if running in the same console.
# If called from GUI, GUI should handle execution (e.g. launch in new terminal).
# If this script is the entry point, we simply hand over execution.
wsl -d $DistroName
