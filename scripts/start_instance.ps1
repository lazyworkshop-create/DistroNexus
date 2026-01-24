# PowerShell script to start a WSL instance
# Usage: ./start_instance.ps1 -DistroName "Ubuntu"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName,
    [switch]$OpenTerminal,
    [string]$StartPath
)

$ErrorActionPreference = "Stop"

# --- Logging Setup ---
. "$PSScriptRoot\pwsh_utils.ps1"
Setup-Logger -LogFileName "start.log"
Log-Message "Starting '$DistroName'..." -FileOnly

# Get list of distros, trim whitespace, and filter empty lines
# This handles potential encoding issues with wsl output
$rawOutput = wsl --list --quiet
$distros = $rawOutput | ForEach-Object { $_.Trim() -replace "`0", "" } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($distros -notcontains $DistroName) {
    Write-Error "WSL instance '$DistroName' not found. Available: $($distros -join ', ')"
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
            $Json | ConvertTo-Json -Depth 4 | Set-Content $ConfigPath -Force
        }
    } catch {
        Write-Warning "Failed to update registry status: $_"
    }
}

# Launch the distro
if ($OpenTerminal) {
    Log-Message "Starting in terminal..."
    if ($StartPath) {
        Log-Message "Working Dir: $StartPath"
        wsl -d $DistroName --cd "$StartPath"
    } else {
        # Default to user home if not specified, instead of current working directory
        wsl -d $DistroName --cd "~"
    }
} else {
    Log-Message "Starting in background..."
    # Start the instance without opening a shell (run a no-op command)
    # This ensures the WSL VM for this distro is booted.
    wsl -d $DistroName -e true
}
