function Get-DistroNexusMonitoringSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation monitorSnapshot -Payload @{ InstanceName=$Name }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
