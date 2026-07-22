function Invoke-DistroNexusPodmanUserUnit {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param([Parameter(Mandatory,ValueFromPipeline)][psobject]$Preview)
    process {
        if ($Preview.Unit -notin @('Service','Socket') -or $Preview.Action -notin @('Start','Stop') -or [string]::IsNullOrWhiteSpace($Preview.Token)) { throw 'A current Core-issued Podman service/socket preview is required.' }
        if (-not $PSCmdlet.ShouldProcess("$($Preview.InstanceName):$($Preview.Unit)", $Preview.Action)) { return [PSCustomObject]@{ Succeeded=$false; OutcomeCode='WhatIf'; Token=$null } }
        Invoke-DistroNexusWorkspaceBridge -Operation executePodmanUnit -Token $Preview.Token -Payload @{ InstanceName=$Preview.InstanceName; Unit=$Preview.Unit; Action=$Preview.Action }
    }
}
