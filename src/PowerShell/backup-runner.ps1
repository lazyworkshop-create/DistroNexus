# backup-runner.ps1
# Invoked by Windows Task Scheduler to perform a scheduled backup.
# This script is called by tasks created with New-DistroNexusBackupSchedule.
param(
    [Parameter(Mandatory = $true)]
    [string]$InstanceName,

    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [Parameter(Mandatory = $true)]
    [int]$RetentionCount
)

$modulePath = Join-Path $PSScriptRoot "DistroNexus.psd1"
Import-Module $modulePath -Force -ErrorAction Stop

try {
    Invoke-DistroNexusBackup -Name $InstanceName -Destination $Destination -RetentionCount $RetentionCount
}
catch {
    # Persist failure notification for next app launch (E-04-1)
    $notifPath = Join-Path $env:APPDATA "DistroNexus\pending-notifications.json"
    $notifDir  = Split-Path $notifPath -Parent
    if (-not (Test-Path $notifDir)) {
        New-Item -Path $notifDir -ItemType Directory -Force | Out-Null
    }
    $notifs = if (Test-Path $notifPath) {
        Get-Content $notifPath -Raw | ConvertFrom-Json -AsHashtable
    } else { @{ notifications = @() } }
    $notifs.notifications += @{
        type     = "BackupFailure"
        instance = $InstanceName
        message  = $_.Exception.Message
        time     = (Get-Date -Format "o")
    }
    $notifs | ConvertTo-Json -Depth 5 | Set-Content $notifPath -Encoding UTF8
    throw
}
