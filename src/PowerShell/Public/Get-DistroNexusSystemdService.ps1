function Get-DistroNexusSystemdService {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name,
        [ValidateSet('User', 'System')][string]$Scope = 'User',
        [switch]$AsJson
    )
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'systemd.list.v1' -Payload @{ InstanceName = $Name; Scope = $Scope }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 12) }
    $value
}

function Get-DistroNexusSystemdServiceDetail {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    Invoke-DistroNexusWorkspaceBridge -Operation 'systemd.details.v1' -Payload @{ InstanceName = $Name; Unit = $Unit; Scope = $Scope }
}

function Get-DistroNexusSystemdServiceJournal {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User', [ValidatePattern('^[^\r\n\0]*$')][string]$Search, [ValidateRange(1, 5000)][int]$LineLimit = 200)
    Invoke-DistroNexusWorkspaceBridge -Operation 'systemd.journal.v1' -Payload @{ InstanceName = $Name; Unit = $Unit; Scope = $Scope; Search = $Search; LineLimit = $LineLimit }
}
