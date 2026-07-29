function Get-DistroNexusUsbStatus {
    <# .SYNOPSIS Gets the sanitized USB discovery status through the fixed WorkspaceBridge contract. #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    Invoke-DistroNexusWorkspaceBridge -Operation 'usb.status.v1'
}
