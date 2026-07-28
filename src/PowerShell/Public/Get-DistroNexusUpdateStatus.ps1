function Get-DistroNexusUpdateStatus {
    [CmdletBinding()]
    param([switch]$IncludePrerelease)
    Invoke-DistroNexusWorkspaceBridge -Operation 'update-status.get.v1' -Payload @{ IncludePrerelease = [bool]$IncludePrerelease }
}
