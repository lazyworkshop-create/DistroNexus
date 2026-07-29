function Invoke-DistroNexusPodmanConnection {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory, ValueFromPipeline, ParameterSetName='Preview')][psobject]$Preview,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidateNotNullOrEmpty()][string]$PreviewToken,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidatePattern('^[^\r\n\0]+$')][string]$InstanceName,
        [Parameter(Mandatory, ParameterSetName='Scalar')][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$')][string]$ConnectionName,
        [Parameter(Mandatory, ParameterSetName='Scalar')][uri]$Endpoint
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Scalar') {
            if ($Endpoint.UserInfo -or $Endpoint.Query -or $Endpoint.Fragment -or (($Endpoint.Scheme -ne 'unix' -or $Endpoint.AbsolutePath -notmatch '^/run/user/.+/podman/podman\.sock$') -and (($Endpoint.Scheme -notin @('tcp','http')) -or -not $Endpoint.IsLoopback -or $Endpoint.Port -lt 1))) { throw 'Only a credential-free local Podman Unix socket or loopback TCP endpoint is permitted.' }
            $Preview = [pscustomobject]@{ Token=$PreviewToken; InstanceName=$InstanceName; Name=$ConnectionName; Endpoint=$Endpoint.GetComponents([UriComponents]::AbsoluteUri, [UriFormat]::UriEscaped) }
        }
        if ([string]::IsNullOrWhiteSpace($Preview.Token) -or [string]::IsNullOrWhiteSpace($Preview.InstanceName) -or [string]::IsNullOrWhiteSpace($Preview.Name) -or [string]::IsNullOrWhiteSpace($Preview.Endpoint)) { throw 'A current Core-issued Podman connection preview is required.' }
        if (-not $PSCmdlet.ShouldProcess("$($Preview.InstanceName):$($Preview.Name)", 'Configure Podman connection')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
        Invoke-DistroNexusWorkspaceBridge -Operation executePodmanConnection -Token $Preview.Token -Payload @{ InstanceName=$Preview.InstanceName; Name=$Preview.Name; Endpoint=$Preview.Endpoint }
    }
}
