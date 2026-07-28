function Rename-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$NewName)
    if (-not $PSCmdlet.ShouldProcess($Name, 'Rename reviewed WSL instance')) { return [pscustomobject]@{ Succeeded=$false; Operation='Rename'; InstanceName=$Name; OutcomeCode='WhatIf' } }
    $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.rename.preview.v1' -Payload @{ Name=$Name; NewName=$NewName }
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.rename.execute.v1' -Payload @{ PreviewToken=$preview.PreviewToken }
}
