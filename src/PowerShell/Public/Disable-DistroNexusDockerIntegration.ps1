function Disable-DistroNexusDockerIntegration {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name)
    $preview = Get-DistroNexusDockerIntegrationPreview -Name $Name -Enabled $false
    if (-not $PSCmdlet.ShouldProcess($Name, 'Disable Docker Desktop integration')) { return [PSCustomObject]@{ Succeeded=$false; OutcomeCode='WhatIf'; RestartRequired=$false } }
    Set-DistroNexusDockerIntegration -Name $Name -Enabled $false -Preview $preview.Token -Confirm:$false
}
