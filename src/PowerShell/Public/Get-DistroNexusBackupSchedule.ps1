function Get-DistroNexusBackupSchedule {
    [CmdletBinding()]
    param([string]$Name,[switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'backup.schedule.list.v1'
    if ($Name) { $value = @($value | Where-Object { $_.InstanceName -eq $Name }) }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 8 -Compress) }; $value
}
