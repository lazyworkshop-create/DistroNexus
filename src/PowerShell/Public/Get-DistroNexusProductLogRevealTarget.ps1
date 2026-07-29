function Get-DistroNexusProductLogRevealTarget {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (-not $PSCmdlet.ShouldProcess('DistroNexus product log directory', 'Create and reveal')) {
        return [pscustomobject]@{ RevealUri = $null; OutcomeCode = 'ProductLog.Declined' }
    }
    Invoke-DistroNexusWorkspaceBridge -Operation 'product.log.reveal-target.v1' -Payload @{}
}
