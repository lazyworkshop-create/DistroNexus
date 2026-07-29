if ($null -eq $global:DistroNexusUsbPreviewGrants) { $global:DistroNexusUsbPreviewGrants = @{} }

function Invoke-DistroNexusUsbNative {
    <# .SYNOPSIS Invokes a native usbipd/sc command with a fresh, explicit exit-code contract. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter()][string[]]$ArgumentList = @(),
        [ValidateRange(1, 4096)][int]$MaximumLines = 256,
        [ValidateRange(1, 16384)][int]$MaximumLineLength = 4096,
        [ValidateRange(1, 65536)][int]$MaximumOutputCharacters = 4096
    )

    # A Pester script fixture need not set LASTEXITCODE.  Clear the native-process
    # value before every call so a prior failing native command cannot poison it.
    $global:LASTEXITCODE = $null
    $remaining = $MaximumOutputCharacters
    $lines = @(& $FilePath @ArgumentList 2>$null | Select-Object -First $MaximumLines | ForEach-Object {
        if ($remaining -le 0) { return }
        $text = [string]$_
        $limit = [Math]::Min($MaximumLineLength, $remaining)
        if ($text.Length -gt $limit) { $text = $text.Substring(0, $limit) }
        $remaining -= $text.Length
        $text
    })
    $exitCode = if ($null -eq $global:LASTEXITCODE) { 0 } else { [int]$global:LASTEXITCODE }
    [PSCustomObject]@{ Output = $lines; ExitCode = $exitCode }
}

function Get-DistroNexusUsbIpdStatus {
    <# .SYNOPSIS Reads the bounded usbipd capability contract without changing host state. #>
    [CmdletBinding()]
    param()

    $command = Get-Command usbipd.exe -ErrorAction SilentlyContinue
    if (-not $command) {
        return [PSCustomObject]@{ Installed = $false; ServiceRunning = $false; Version = $null; Major = $null; SupportsMutation = $false; Command = $null }
    }

    $versionResult = Invoke-DistroNexusUsbNative -FilePath $command.Source -ArgumentList '--version'
    $versionOutput = $versionResult.Output
    $match = if ($versionResult.ExitCode -eq 0) { [regex]::Match(($versionOutput -join "`n"), '\b(\d+)\.(\d+)(?:\.(\d+))?') } else { [regex]::Match('', '^$') }
    $major = if ($match.Success) { [int]$match.Groups[1].Value } else { $null }
    $version = if ($match.Success) { $match.Value } else { $null }
    $serviceResult = Invoke-DistroNexusUsbNative -FilePath 'sc.exe' -ArgumentList @('query', 'usbipd')
    $serviceRunning = $serviceResult.ExitCode -eq 0 -and (($serviceResult.Output -join "`n") -match 'RUNNING')
    return [PSCustomObject]@{
        Installed = $true
        ServiceRunning = $serviceRunning
        Version = $version
        Major = $major
        SupportsMutation = $major -in 4, 5
        Command = $command
    }
}

function Write-DistroNexusUsbContractError {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ErrorId,
        [Parameter(Mandatory)][System.Management.Automation.ErrorCategory]$Category,
        [Parameter(Mandatory)][string]$Message
    )

    Write-Error -ErrorId $ErrorId -Category $Category -Message $Message
}

function ConvertTo-DistroNexusUsbDevice {
    <# .SYNOPSIS Converts only the documented version-specific usbipd JSON fields. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Row,
        [Parameter(Mandatory)][ValidateSet(4, 5)][int]$Major
    )

    # Major-specific fields are deliberately exact.  A changed producer shape falls back to
    # the conservative table parser rather than being silently merged with another major.
    $busId = if ($Major -eq 4 -and $Row.PSObject.Properties['busId']) { [string]$Row.busId } elseif ($Major -eq 5 -and $Row.PSObject.Properties['bus_id']) { [string]$Row.bus_id } else { $null }
    if ($busId -notmatch '^[0-9A-Fa-f]{1,3}-[0-9A-Fa-f]{1,3}$') { return $null }
    $hardwareId = if ($Major -eq 4 -and $Row.PSObject.Properties['hardwareId']) { [string]$Row.hardwareId } elseif ($Major -eq 5 -and $Row.PSObject.Properties['vidPid']) { [string]$Row.vidPid } else { $null }
    if ($hardwareId -notmatch '^[0-9A-Fa-f]{4}:[0-9A-Fa-f]{4}$') { return $null }
    $description = if ($Major -eq 4 -and $Row.PSObject.Properties['description']) { [string]$Row.description } elseif ($Major -eq 5 -and $Row.PSObject.Properties['device']) { [string]$Row.device } else { $null }
    $state = if ($Major -eq 4 -and $Row.PSObject.Properties['state']) { [string]$Row.state } elseif ($Major -eq 5 -and $Row.PSObject.Properties['status']) { [string]$Row.status } else { $null }
    if ([string]::IsNullOrWhiteSpace($description) -or $null -eq $state) { return $null }
    if ($state -cnotin @('Not shared', 'Shared', 'Attached', 'Unknown')) { return $null }
    $distribution = if ($Row.PSObject.Properties['distribution']) { [string]$Row.distribution } elseif ($Row.PSObject.Properties['client']) { [string]$Row.client } else { $null }
    return [PSCustomObject]@{ PSTypeName = 'DistroNexus.UsbDevice'; BusId = $busId.ToUpperInvariant(); HardwareId = $hardwareId; Description = $description; State = $state; Distribution = $distribution }
}

function Get-DistroNexusUsbDeviceRows {
    [CmdletBinding()]
    param([Parameter(Mandatory)][PSCustomObject]$Status)

    $rows = @()
    if ($Status.Major -in 4, 5) {
        $jsonResult = Invoke-DistroNexusUsbNative -FilePath $Status.Command.Source -ArgumentList @('list', '--json')
        $json = $jsonResult.Output
        try {
            if ($json -and $jsonResult.ExitCode -eq 0) { $rows = @($json | ConvertFrom-Json -ErrorAction Stop) }
            if ($rows.Count -eq 1 -and $rows[0].PSObject.Properties['devices']) { $rows = @($rows[0].devices) }
            $devices = @($rows | ForEach-Object { ConvertTo-DistroNexusUsbDevice -Row $_ -Major $Status.Major } | Where-Object { $null -ne $_ })
            # A malformed or cross-major row invalidates this JSON response.  Fall back to the
            # independently validated table contract; never return a partial trusted device list.
            if ($rows.Count -eq 0 -or $devices.Count -eq $rows.Count) { return $devices }
        } catch { }
    }

    $tableResult = Invoke-DistroNexusUsbNative -FilePath $Status.Command.Source -ArgumentList 'list'
    $table = $tableResult.Output
    if ($tableResult.ExitCode -ne 0) { throw 'usbipd list failed' }
    foreach ($line in $table) {
        if ($line -match '^\s*(?<bus>[0-9A-Fa-f]{1,3}-[0-9A-Fa-f]{1,3})\s+(?<id>[0-9A-Fa-f]{4}:[0-9A-Fa-f]{4})\s+(?<desc>.+?)\s{2,}(?<state>Not shared|Shared|Attached|Unknown)\s*$') {
            $devices += [PSCustomObject]@{ PSTypeName = 'DistroNexus.UsbDevice'; BusId = $Matches.bus.ToUpperInvariant(); HardwareId = $Matches.id; Description = $Matches.desc.Trim(); State = $Matches.state; Distribution = $null }
        }
    }
    return @($devices)
}

function Get-DistroNexusUsbActionPreflight {
    <# .SYNOPSIS Generates a mutation preview only after all read-only contract gates pass. #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Attach', 'Detach')][string]$Action,
        [Parameter(Mandatory)][string]$BusId,
        [string]$Distribution,
        [Guid]$PreviewToken
    )

    $previewStore = Get-Variable -Name DistroNexusUsbPreviewGrants -Scope Global -ValueOnly -ErrorAction SilentlyContinue
    if ($null -eq $previewStore) { $previewStore = @{}; Set-Variable -Name DistroNexusUsbPreviewGrants -Scope Global -Value $previewStore }

    $status = Get-DistroNexusUsbIpdStatus
    if (-not $status.Installed) { return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.Unavailable'; Category = [System.Management.Automation.ErrorCategory]::ObjectNotFound; Message = 'DN-8006: usbipd-win is unavailable. No USB operation was performed.' } }
    if (-not $status.ServiceRunning) { return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.ServiceStopped'; Category = [System.Management.Automation.ErrorCategory]::ResourceUnavailable; Message = 'DN-8012: The usbipd service is not running. No USB operation was performed.' } }
    if (-not $status.SupportsMutation) { return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.UnsupportedVersion'; Category = [System.Management.Automation.ErrorCategory]::NotImplemented; Message = 'DN-8007: The detected usbipd version is not approved for mutation.' } }
    try { $device = @(Get-DistroNexusUsbDeviceRows -Status $status | Where-Object { $_.BusId -eq $BusId.ToUpperInvariant() } | Select-Object -First 1)[0] }
    catch { return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.ListFailed'; Category = [System.Management.Automation.ErrorCategory]::InvalidData; Message = 'DN-8008: USB devices could not be refreshed. No USB operation was performed.' } }
    if (-not $device) { return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.StaleBusId'; Category = [System.Management.Automation.ErrorCategory]::ObjectNotFound; Message = 'DN-8008: The selected USB device is no longer connected. Refresh and select it again.' } }
    $legal = if ($Action -eq 'Attach') { $device.State -ceq 'Shared' } else { $device.State -ceq 'Attached' }
    if (-not $legal) { return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.StateChanged'; Category = [System.Management.Automation.ErrorCategory]::InvalidOperation; Message = 'DN-8014: This USB operation is not legal for the device current state. Refresh and review it again.' } }
    $effects = if ($Action -eq 'Attach') { @("Attach USB device $($device.BusId) to WSL distribution $Distribution.") } else { @("Detach USB device $($device.BusId) from the shared WSL VM.") }
    if ($null -ne $PreviewToken -and $PreviewToken -ne [Guid]::Empty) {
        $grant = $previewStore[$PreviewToken]
        if ($null -eq $grant -or $grant.ExpiresAt -le [DateTimeOffset]::UtcNow -or
            $grant.Action -cne $Action -or $grant.BusId -cne $device.BusId -or
            $grant.HardwareId -cne $device.HardwareId -or $grant.Distribution -cne $Distribution) {
            return [PSCustomObject]@{ Succeeded = $false; ErrorId = 'DistroNexus.Usb.PreviewRequired'; Category = [System.Management.Automation.ErrorCategory]::InvalidOperation; Message = 'DN-8009: Generate and explicitly confirm a current USB operation preview.' }
        }
        $null = $previewStore.Remove($PreviewToken)
        return [PSCustomObject]@{ Succeeded = $true; Preview = $grant }
    }
    $token = [Guid]::NewGuid()
    $preview = [PSCustomObject]@{ PSTypeName = 'DistroNexus.UsbDeviceActionPreview'; Token = $token; Action = $Action; BusId = $device.BusId; HardwareId = $device.HardwareId; Distribution = $Distribution; RequiresConfirmation = $true; Effects = $effects; Warnings = @('USB/IP attachment is visible to the running WSL 2 VM and is not isolated to one distribution.') }
    $previewStore[$token] = [PSCustomObject]@{ Token = $token; Action = $Action; BusId = $device.BusId; HardwareId = $device.HardwareId; Distribution = $Distribution; ExpiresAt = [DateTimeOffset]::UtcNow.AddMinutes(2); Preview = $preview }
    return [PSCustomObject]@{ Succeeded = $true; Preview = $preview }
}
