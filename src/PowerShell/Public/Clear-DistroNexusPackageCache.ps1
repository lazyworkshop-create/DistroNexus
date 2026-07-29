function Clear-DistroNexusPackageCache {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param()
    if ($PSCmdlet.ShouldProcess('DistroNexus package cache', 'Clear')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'package-cache.clear.v1'
    }
}
