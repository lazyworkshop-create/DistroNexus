function Remove-DistroNexusBackupSchedule {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name)
    $proceed = if ($null -eq $PSCmdlet) { -not $WhatIfPreference } else { $PSCmdlet.ShouldProcess($Name, 'Remove fixed backup schedule') }
    if (-not $proceed) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'backup.schedule.remove.preview.v1' -Payload @{ InstanceName=$Name }
    Invoke-DistroNexusWorkspaceBridge -Operation 'backup.execute.v1' -Payload @{ PreviewToken=$preview.Token }
}
