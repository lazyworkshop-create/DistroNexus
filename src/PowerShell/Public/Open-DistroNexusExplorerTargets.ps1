function Open-DistroNexusWslConfigFile {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (-not $PSCmdlet.ShouldProcess('.wslconfig', 'Open fixed WSL configuration file')) { return [pscustomobject]@{ Succeeded = $false; OutcomeCode = 'WhatIf' } }
    Invoke-DistroNexusWorkspaceBridge -Operation 'explorer.wslconfig.v1'
}

function Open-DistroNexusRecoveryPointFolder {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][guid]$Id)
    if (-not $PSCmdlet.ShouldProcess($Id, 'Open fixed recovery point folder')) { return [pscustomobject]@{ Succeeded = $false; OutcomeCode = 'WhatIf' } }
    Invoke-DistroNexusWorkspaceBridge -Operation 'explorer.recovery-point.v1' -Id $Id
}
