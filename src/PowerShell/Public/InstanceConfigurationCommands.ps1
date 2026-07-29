function Get-DistroNexusInstanceConfiguration {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name)
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.config.read.v1' -Payload @{ Name = $Name }
}

function Get-DistroNexusInstanceConfigurationRecoveryOffer {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name)
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.config.recovery.v1' -Payload @{ Name = $Name }
}

function Save-DistroNexusInstanceConfiguration {
    [CmdletBinding(DefaultParameterSetName = 'Preview', SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Preview')][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory, ParameterSetName = 'Preview')][ValidateNotNull()][hashtable]$Changes,
        [Parameter(Mandatory, ParameterSetName = 'Execute')][ValidatePattern('^[0-9A-Fa-f]{64}$')][string]$PreviewToken
    )
    if ($PSCmdlet.ParameterSetName -eq 'Preview') {
        return Invoke-DistroNexusWorkspaceBridge -Operation 'instance.config.preview.v1' -Payload @{ Name = $Name; Changes = $Changes }
    }
    if ($PSCmdlet.ShouldProcess('instance configuration', 'Save')) {
        return Invoke-DistroNexusWorkspaceBridge -Operation 'instance.config.execute.v1' -Payload @{ PreviewToken = $PreviewToken }
    }
}
