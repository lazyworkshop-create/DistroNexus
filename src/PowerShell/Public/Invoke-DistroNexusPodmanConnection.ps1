function Invoke-DistroNexusPodmanConnection {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param([Parameter(Mandatory,ValueFromPipeline)][psobject]$Preview)
    process {
        if ([string]::IsNullOrWhiteSpace($Preview.Token) -or [string]::IsNullOrWhiteSpace($Preview.InstanceName) -or [string]::IsNullOrWhiteSpace($Preview.Name) -or [string]::IsNullOrWhiteSpace($Preview.Endpoint)) { throw 'A current Core-issued Podman connection preview is required.' }
        if (-not $PSCmdlet.ShouldProcess("$($Preview.InstanceName):$($Preview.Name)", 'Configure Podman connection')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
        Invoke-DistroNexusWorkspaceBridge -Operation executePodmanConnection -Token $Preview.Token -Payload @{ InstanceName=$Preview.InstanceName; Name=$Preview.Name; Endpoint=$Preview.Endpoint }
    }
}
