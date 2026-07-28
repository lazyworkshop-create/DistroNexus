function Get-DistroNexusPackageCacheLocation {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'package-cache.location.v1'
}
