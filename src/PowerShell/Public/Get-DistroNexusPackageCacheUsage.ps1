function Get-DistroNexusPackageCacheUsage {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'package-cache.usage.v1'
}
