function Get-DistroNexusDockerIntegration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name)
    Invoke-DistroNexusWorkspaceBridge -Operation 'docker.integration.get.v1' -Payload @{ Name = $Name }
}
