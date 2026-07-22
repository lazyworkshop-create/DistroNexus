function Connect-DistroNexusUsbDevice {
    <# .SYNOPSIS Attaches an already-shared USB device to one WSL distribution. #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{1,3}-[0-9A-Fa-f]{1,3}$')][string]$BusId,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 128)][ValidatePattern('^[^\r\n\x00]+$')][string]$Distribution
    )

    $preflight = Get-DistroNexusUsbActionPreflight -Action Attach -BusId $BusId -Distribution $Distribution
    if (-not $preflight.Succeeded) { Write-DistroNexusUsbContractError -ErrorId $preflight.ErrorId -Category $preflight.Category -Message $preflight.Message; return }
    $preflight.Preview
    if (-not $PSCmdlet.ShouldProcess("USB device $($preflight.Preview.BusId)", "Attach to WSL distribution $Distribution")) { return }
    # Re-read the read-only contract after confirmation so a device/service/version change cannot reuse a stale preview.
    $current = Get-DistroNexusUsbActionPreflight -Action Attach -BusId $BusId -Distribution $Distribution -PreviewToken $preflight.Preview.Token
    if (-not $current.Succeeded) { Write-DistroNexusUsbContractError -ErrorId $current.ErrorId -Category $current.Category -Message $current.Message; return }
    $status = Get-DistroNexusUsbIpdStatus
    $result = Invoke-DistroNexusUsbNative -FilePath $status.Command.Source -ArgumentList @('attach', '--wsl', '--busid', $current.Preview.BusId, '--distribution', $Distribution)
    if ($result.ExitCode -ne 0) { Write-DistroNexusUsbContractError -ErrorId 'DistroNexus.Usb.AttachFailed' -Category OperationStopped -Message 'DN-8011: usbipd rejected the attach request.' }
}
