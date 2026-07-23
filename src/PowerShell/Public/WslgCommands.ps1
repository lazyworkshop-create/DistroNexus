function Get-DistroNexusWslgApplication {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation wslgDiscover -Payload @{ InstanceName = $Name }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Start-DistroNexusWslgApplication {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Application)
    process {
        if (-not $Application.Id -or -not $Application.InstanceName -or -not $Application.Executable) { throw 'A Core-discovered WSLg application is required.' }
        if (-not $PSCmdlet.ShouldProcess("$($Application.InstanceName):$($Application.Name)", 'Start WSLg application')) { return [pscustomobject]@{ Succeeded=$false; Detail='WhatIf' } }
        Invoke-DistroNexusWorkspaceBridge -Operation wslgLaunch -Payload @{ Application = $Application }
    }
}
