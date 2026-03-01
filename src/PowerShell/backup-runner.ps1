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

Invoke-DistroNexusBackup -Name $InstanceName -Destination $Destination -RetentionCount $RetentionCount
