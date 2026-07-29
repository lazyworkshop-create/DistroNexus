function Invoke-DistroNexusPodmanUserUnit {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory, ValueFromPipeline, ParameterSetName='Preview')][psobject]$Preview,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidateNotNullOrEmpty()][string]$PreviewToken,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidatePattern('^[^\r\n\0]+$')][string]$InstanceName,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidateSet('Service','Socket')][string]$Unit,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidateSet('Start','Stop')][string]$Action
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Scalar') {
            $Preview = [pscustomobject]@{ Token=$PreviewToken; InstanceName=$InstanceName; Unit=$Unit; Action=$Action }
        }
        if ($Preview.Unit -notin @('Service','Socket') -or $Preview.Action -notin @('Start','Stop') -or [string]::IsNullOrWhiteSpace($Preview.Token)) { throw 'A current Core-issued Podman service/socket preview is required.' }
        if (-not $PSCmdlet.ShouldProcess("$($Preview.InstanceName):$($Preview.Unit)", $Preview.Action)) { return [PSCustomObject]@{ Succeeded=$false; OutcomeCode='WhatIf'; Token=$null } }
        Invoke-DistroNexusWorkspaceBridge -Operation executePodmanUnit -Token $Preview.Token -Payload @{ InstanceName=$Preview.InstanceName; Unit=$Preview.Unit; Action=$Preview.Action }
    }
}
