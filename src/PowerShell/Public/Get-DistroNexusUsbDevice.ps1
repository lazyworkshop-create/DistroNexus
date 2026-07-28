function Get-DistroNexusUsbDevice {
    <# .SYNOPSIS Lists sanitized USB device snapshots through the fixed WorkspaceBridge contract. #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    Invoke-DistroNexusWorkspaceBridge -Operation 'usb.list.v1'
}
