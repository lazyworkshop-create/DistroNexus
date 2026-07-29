function Get-DistroNexusSettings {
    <#
    .SYNOPSIS
        Retrieves the typed global DistroNexus settings.
    #>
    [CmdletBinding()]
    param()

    $settings = Invoke-DistroNexusWorkspaceBridge -Operation 'settings.get.v1'
    if ($settings.PSObject.Properties['PowerShellModulePath']) { $settings.PowerShellModulePath = $null }
    $settings
}
