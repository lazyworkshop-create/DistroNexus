function Set-DistroNexusInstanceSparseMode {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$PreviewToken)
    if (-not $PSCmdlet.ShouldProcess('reviewed sparse-mode preview', 'Set WSL sparse mode')) {
        return [PSCustomObject]@{ Succeeded = $false; OutcomeCode = 'WhatIf' }
    }
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.sparse.execute.v1' -Payload @{ PreviewToken = $PreviewToken }
}
