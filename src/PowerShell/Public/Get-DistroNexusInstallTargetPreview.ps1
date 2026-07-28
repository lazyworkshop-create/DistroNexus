function Get-DistroNexusInstallTargetPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$InstallRoot)
    Invoke-DistroNexusWorkspaceBridge -Operation 'install.target.preview.v1' -Payload @{ InstallRoot = $InstallRoot }
}
