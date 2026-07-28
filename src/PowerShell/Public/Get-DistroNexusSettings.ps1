function Get-DistroNexusSettings {
    <#
    .SYNOPSIS
        Retrieves the typed global DistroNexus settings.
    #>
    [CmdletBinding()]
    param()

    Invoke-DistroNexusWorkspaceBridge -Operation 'settings.get.v1'
}
