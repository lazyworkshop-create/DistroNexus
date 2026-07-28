function Move-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]{1,1024}$')][string]$Destination)
    if (-not $PSCmdlet.ShouldProcess($Name, 'Move reviewed WSL instance')) { return [pscustomobject]@{ Succeeded=$false; Operation='Move'; InstanceName=$Name; OutcomeCode='WhatIf' } }
    $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.move.preview.v1' -Payload @{ Name=$Name; Destination=$Destination }
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.move.execute.v1' -Payload @{ PreviewToken=$preview.PreviewToken }
}
