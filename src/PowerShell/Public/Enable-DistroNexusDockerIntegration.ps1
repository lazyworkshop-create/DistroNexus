function Enable-DistroNexusDockerIntegration {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name)
    $preview = Get-DistroNexusDockerIntegrationPreview -Name $Name -Enabled $true
    if (-not $PSCmdlet.ShouldProcess($Name, 'Enable Docker Desktop integration')) { return [PSCustomObject]@{ Succeeded=$false; OutcomeCode='WhatIf'; RestartRequired=$false } }
    Set-DistroNexusDockerIntegration -Name $Name -Enabled $true -Preview $preview.Token -Confirm:$false
}
