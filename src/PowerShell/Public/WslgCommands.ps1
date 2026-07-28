function Get-DistroNexusWslgStatus {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'wslg.status.v1' -Payload @{ InstanceName = $Name }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Get-DistroNexusWslgApplication {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'wslg.discover.v1' -Payload @{ InstanceName = $Name }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Start-DistroNexusWslgApplication {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$DiscoveryToken, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$ApplicationId)
    process {
        if (-not $PSCmdlet.ShouldProcess($ApplicationId, 'Start WSLg application')) { return [pscustomobject]@{ Succeeded=$false; Detail='WhatIf' } }
        Invoke-DistroNexusWorkspaceBridge -Operation 'wslg.launch.v1' -Payload @{ DiscoveryToken = $DiscoveryToken; ApplicationId = $ApplicationId }
    }
}
function Show-DistroNexusWslgApplicationEntry {
    [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$DiscoveryToken, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$ApplicationId)
    if ($PSCmdlet.ShouldProcess($ApplicationId, 'Reveal WSLg desktop entry')) { Invoke-DistroNexusWorkspaceBridge -Operation 'wslg.reveal.v1' -Payload @{ DiscoveryToken=$DiscoveryToken; ApplicationId=$ApplicationId } }
}
function Set-DistroNexusWslgApplicationPin {
    [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$DiscoveryToken, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$ApplicationId, [Parameter(Mandatory)][bool]$Pinned)
    if ($PSCmdlet.ShouldProcess($ApplicationId, 'Set WSLg application pin')) { Invoke-DistroNexusWorkspaceBridge -Operation 'wslg.pin.v1' -Payload @{ DiscoveryToken=$DiscoveryToken; ApplicationId=$ApplicationId; Pinned=$Pinned } }
}
