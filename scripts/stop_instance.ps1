# PowerShell script to terminate a WSL instance
# Usage: ./stop_instance.ps1 -DistroName "Ubuntu-24.04"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName
)

$ErrorActionPreference = "Stop"

if (-not (wsl --list --quiet | Select-String -Pattern "^$DistroName$")) {
    Write-Error "WSL instance '$DistroName' not found."
    exit 1
}

Write-Host "Stopping WSL instance '$DistroName'..." -ForegroundColor Cyan
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

    Write-Host "Instance '$DistroName' stopped successfully." -ForegroundColor Green
} catch {
    Write-Error "Failed to stop instance: $_"
    exit 1
}
