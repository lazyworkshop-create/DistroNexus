function Get-DistroNexusInstanceResources {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name)
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.resources.get.v1' -Payload @{ Name = $Name }
}

function Get-DistroNexusInstanceSparsePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][bool]$Enabled)
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.sparse.preview.v1' -Payload @{ Name = $Name; Enabled = $Enabled }
}
