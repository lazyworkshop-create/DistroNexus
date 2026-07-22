function Get-DistroNexusUsbDevice {
    <# .SYNOPSIS Lists usbipd-win devices without changing host or WSL state. #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    $status = Get-DistroNexusUsbIpdStatus
    if (-not $status.Installed) {
        Write-DistroNexusUsbContractError -ErrorId 'DistroNexus.Usb.Unavailable' -Category ObjectNotFound -Message 'DN-8006: usbipd-win is unavailable. No USB operation was performed.'
        return
    }
    try { Get-DistroNexusUsbDeviceRows -Status $status }
    catch { Write-DistroNexusUsbContractError -ErrorId 'DistroNexus.Usb.ListMalformed' -Category InvalidData -Message 'usbipd returned malformed or unsupported device data.' }
}
