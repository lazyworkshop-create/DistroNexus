function Reset-DistroNexusSettings {
    <#
    .SYNOPSIS
        Resets global DistroNexus settings to their modeled defaults.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param()

    if (-not $PSCmdlet.ShouldProcess('global DistroNexus settings', 'Reset')) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'settings.reset.v1'
}
