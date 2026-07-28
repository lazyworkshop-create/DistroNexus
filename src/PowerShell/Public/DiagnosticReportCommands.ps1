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
        [Parameter(Mandatory, ValueFromPipeline)][psobject]$Preview,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DestinationFileName,
        [ValidateRange(1, 30000)][int]$DeadlineMilliseconds
    )

    process {
        if (-not $Preview.SnapshotToken -or -not $Preview.Selection -or -not $Preview.Selection.IsRedacted) {
            throw 'A current redacted diagnostic preview is required.'
        }
        if ($DestinationFileName -ne [System.IO.Path]::GetFileName($DestinationFileName)) {
            throw 'DestinationFileName must be a file name in the DistroNexus diagnostic export directory.'
        }
        $extension = if ($Preview.Format -eq 'Json') { '.json' } else { '.md' }
        if ([System.IO.Path]::GetExtension($DestinationFileName) -ne $extension) {
            throw 'DestinationFileName extension must match the preview format.'
        }
        if (-not $PSCmdlet.ShouldProcess($DestinationFileName, 'Export redacted DistroNexus diagnostic report')) { return $false }
        $payload = @{ DestinationFileName = $DestinationFileName }
        if ($PSBoundParameters.ContainsKey('DeadlineMilliseconds')) { $payload.DeadlineMilliseconds = $DeadlineMilliseconds }
        Invoke-DistroNexusWorkspaceBridge -Operation 'diagnostics.export.v1' -Token $Preview.SnapshotToken -Payload $payload
    }
}
