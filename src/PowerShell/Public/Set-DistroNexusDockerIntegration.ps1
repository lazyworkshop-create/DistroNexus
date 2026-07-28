function Set-DistroNexusDockerIntegration {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][bool]$Enabled, [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$Preview)
    if (-not $PSCmdlet.ShouldProcess($Name, $(if ($Enabled) { 'Enable Docker Desktop integration' } else { 'Disable Docker Desktop integration' }))) { return [PSCustomObject]@{ Succeeded=$false; OutcomeCode='WhatIf'; RestartRequired=$false } }
    Invoke-DistroNexusWorkspaceBridge -Operation 'docker.integration.set.v1' -Token $Preview -Payload @{ Name = $Name; Enabled = $Enabled }
}
