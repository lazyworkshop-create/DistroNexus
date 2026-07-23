function Get-DistroNexusSystemdService {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name,
        [ValidateSet('User', 'System')][string]$Scope = 'User',
        [switch]$AsJson
    )
    $value = Invoke-DistroNexusWorkspaceBridge -Operation systemdList -Payload @{ InstanceName = $Name; Scope = $Scope }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 12) }
    $value
}
