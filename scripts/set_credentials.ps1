# PowerShell script to set user credentials for a WSL instance
# Usage: ./set_credentials.ps1 -DistroName "Ubuntu" -UserName "dev" -Password "secret"

param(
    [Parameter(Mandatory=$true)]
    [string]$DistroName,
    
    [Parameter(Mandatory=$true)]
    [string]$UserName,
    
    [string]$Password
)

$ErrorActionPreference = "Stop"

if (-not (wsl --list --quiet | Select-String -Pattern "^$DistroName$")) {
    Write-Error "WSL instance '$DistroName' not found."
    exit 1
}

Write-Host "Configuring credentials for '$DistroName'..." -ForegroundColor Cyan

try {
    # Check if user exists
    $CheckUser = wsl -d $DistroName -u root -- id -u $UserName 2>$null
    if (-not $CheckUser) {
        Write-Host "Creating user '$UserName'..." -ForegroundColor Gray
        wsl -d $DistroName -u root -- useradd -m -s /bin/bash $UserName
    } else {
        Write-Host "User '$UserName' already exists. Updating..." -ForegroundColor Gray
    }

    # Set password if provided
    if ($Password) {
        Write-Host "Setting password..." -ForegroundColor Gray
        wsl -d $DistroName -u root -- sh -c "echo '${UserName}:${Password}' | chpasswd"
    }

    # Add to sudo/wheel
    wsl -d $DistroName -u root -- sh -c "usermod -aG sudo $UserName 2>/dev/null || true"
    wsl -d $DistroName -u root -- sh -c "usermod -aG wheel $UserName 2>/dev/null || true"

    # Set as default user in /etc/wsl.conf
    # We construct the content to overwrite or append. Safer to overwrite [user] section or entire file if simple.
    # For this script, we'll try to replace [user] default or append it.
    # Simplest reliable way for this tool: Overwrite /etc/wsl.conf with standard config if we control it, 
    # OR utilize a wsl.conf setup helper. 
    # Implem: Just write [user] default=$UserName for now, preserving boot settings is harder without parsing.
    # Assuming minimal config for these custom instances.
    
    Write-Host "Setting default user in /etc/wsl.conf..." -ForegroundColor Gray
    
    # Read existing conf
    $CurrentConf = wsl -d $DistroName -u root -- cat /etc/wsl.conf 2>$null
    if (-not $CurrentConf) { $CurrentConf = "" }
    
    # Simple logic: If [user] exists, replace the default=... line. If not, append.
    # Using specific logic to just overwrite for robustness in this context
    $Cmd = "printf '[user]\ndefault=$UserName\n' > /etc/wsl.conf"
    if ($CurrentConf -match "\[boot\]") {
        # If there are other settings, attempt to preserve (simple append if [user] missing, else tedious)
        # For V1, let's just Append if not present, or warn. 
        # Actually, let's just force the user section.
        $Cmd = "printf '\n[user]\ndefault=$UserName\n' >> /etc/wsl.conf" 
    }
    
    wsl -d $DistroName -u root -- sh -c "$Cmd"

    # Terminate to apply changes
    wsl --terminate $DistroName

    # Update instances.json
    $ConfigPath = Join-Path $PSScriptRoot "..\config\instances.json"
    if (Test-Path $ConfigPath) {
        $Json = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $Instance = $Json | Where-Object { $_.Name -eq $DistroName }
        if ($Instance) {
            $Instance.User = $UserName
            $Json | ConvertTo-Json -Depth 4 | Set-Content $ConfigPath -Force
        }
    }

    Write-Host "Credentials updated successfully. Instance terminated to apply settings." -ForegroundColor Green

} catch {
    Write-Error "Failed to set credentials: $_"
    exit 1
}
