function Get-DistroNexusDiagnosticReportPreview {
    [CmdletBinding()]
    param(
        [ValidateSet('Markdown', 'Json')][string]$Format = 'Markdown',
        [ValidateNotNullOrEmpty()][string[]]$SelectedLogId = @(),
        [ValidateRange(1, 30000)][int]$DeadlineMilliseconds
    )

    $payload = @{ Format = $Format; SelectedLogIds = @($SelectedLogId) }
    if ($PSBoundParameters.ContainsKey('DeadlineMilliseconds')) { $payload.DeadlineMilliseconds = $DeadlineMilliseconds }
    Invoke-DistroNexusWorkspaceBridge -Operation 'diagnostics.preview.v1' -Payload $payload
}

function Export-DistroNexusDiagnosticReport {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{32}$')][string]$SnapshotToken,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DestinationFileName,
        [ValidateRange(1, 30000)][int]$DeadlineMilliseconds
    )

    process {
        if ($DestinationFileName -ne [System.IO.Path]::GetFileName($DestinationFileName)) {
            throw 'DestinationFileName must be a file name in the DistroNexus diagnostic export directory.'
        }
        if (-not $PSCmdlet.ShouldProcess($DestinationFileName, 'Export redacted DistroNexus diagnostic report')) { return $false }
        $payload = @{ DestinationFileName = $DestinationFileName }
        if ($PSBoundParameters.ContainsKey('DeadlineMilliseconds')) { $payload.DeadlineMilliseconds = $DeadlineMilliseconds }
        Invoke-DistroNexusWorkspaceBridge -Operation 'diagnostics.export.v1' -Token $SnapshotToken -Payload $payload
    }
}
