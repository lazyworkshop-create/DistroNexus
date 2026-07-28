function Get-DistroNexusDockerIntegrationPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][bool]$Enabled)
    Invoke-DistroNexusWorkspaceBridge -Operation 'docker.integration.preview-set.v1' -Payload @{ Name = $Name; Enabled = $Enabled }
}
