function Get-DistroNexusDockerDesktopInstallUri {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'external.docker-desktop-install-uri.v1' -Payload @{}
}
