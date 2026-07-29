function Invoke-DistroNexusBackup {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name,[ValidatePattern('^[^\r\n\0]+$')][string]$Destination,[Parameter(Mandatory)][ValidateRange(1,30)][int]$RetentionCount)
    # Destination is retained only for callers of the legacy cmdlet.  It can influence preview validation,
    # but is deliberately absent from the single-use execution contract.
    $proceed = if ($null -eq $PSCmdlet) { -not $WhatIfPreference } else { $PSCmdlet.ShouldProcess($Name, 'Run fixed backup') }
    if (-not $proceed) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    $payload = @{ InstanceName=$Name; RetentionCount=$RetentionCount }
    if ($PSBoundParameters.ContainsKey('Destination')) { $payload.Destination = $Destination }
    $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'backup.manual.preview.v1' -Payload $payload
    Invoke-DistroNexusWorkspaceBridge -Operation 'backup.execute.v1' -Payload @{ PreviewToken=$preview.Token }
}
