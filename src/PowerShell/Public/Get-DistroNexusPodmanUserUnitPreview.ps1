function Get-DistroNexusPodmanUserUnitPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidateSet('Service','Socket')][string]$Unit, [Parameter(Mandatory)][ValidateSet('Start','Stop')][string]$Action)
    Invoke-DistroNexusWorkspaceBridge -Operation previewPodmanUnit -Payload @{ InstanceName=$Name; Unit=$Unit; Action=$Action }
}
