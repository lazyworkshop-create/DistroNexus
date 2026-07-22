function Disconnect-DistroNexusUsbDevice {
    <# .SYNOPSIS Detaches a USB device from the WSL VM. It cannot bind or unbind a host device. #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{1,3}-[0-9A-Fa-f]{1,3}$')][string]$BusId)

    $preflight = Get-DistroNexusUsbActionPreflight -Action Detach -BusId $BusId
    if (-not $preflight.Succeeded) { Write-DistroNexusUsbContractError -ErrorId $preflight.ErrorId -Category $preflight.Category -Message $preflight.Message; return }
    $preflight.Preview
    if (-not $PSCmdlet.ShouldProcess("USB device $($preflight.Preview.BusId)", 'Detach from WSL')) { return }
    # Re-read the read-only contract after confirmation so a device/service/version change cannot reuse a stale preview.
    $current = Get-DistroNexusUsbActionPreflight -Action Detach -BusId $BusId -PreviewToken $preflight.Preview.Token
    if (-not $current.Succeeded) { Write-DistroNexusUsbContractError -ErrorId $current.ErrorId -Category $current.Category -Message $current.Message; return }
    $status = Get-DistroNexusUsbIpdStatus
    $result = Invoke-DistroNexusUsbNative -FilePath $status.Command.Source -ArgumentList @('detach', '--busid', $current.Preview.BusId)
    if ($result.ExitCode -ne 0) { Write-DistroNexusUsbContractError -ErrorId 'DistroNexus.Usb.DetachFailed' -Category OperationStopped -Message 'DN-8011: usbipd rejected the detach request.' }
}
