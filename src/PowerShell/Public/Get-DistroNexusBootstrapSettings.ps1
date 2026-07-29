function Get-DistroNexusBootstrapSettings {
    [CmdletBinding()]
    param()
    $settings = Invoke-DistroNexusWorkspaceBridge -Operation 'settings.get.v1'
    if ($settings.PSObject.Properties['PowerShellModulePath']) { $settings.PowerShellModulePath = $null }
    [pscustomobject]@{ Settings = $settings; ModuleState = 'Ready' }
}
