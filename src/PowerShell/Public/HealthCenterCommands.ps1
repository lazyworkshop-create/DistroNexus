function Invoke-DistroNexusHealthScan {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation healthScan
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Get-DistroNexusHealthRepairPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Finding)
    process { if (-not $Finding.Id -or -not $Finding.RepairId) { throw 'A repairable Core health finding is required.' }; Invoke-DistroNexusWorkspaceBridge -Operation healthRepairPreview -Payload @{ Finding=$Finding } }
}
function Repair-DistroNexusHealthFinding {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Finding, [Parameter(Mandatory)][psobject]$Preview)
    process {
        if (-not $Finding.Id -or -not $Finding.RepairId -or -not $Preview.PreviewToken) { throw 'A current Core-issued health repair preview is required.' }
        if (-not $PSCmdlet.ShouldProcess($Finding.Id, "Repair $($Finding.Title)")) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
        Invoke-DistroNexusWorkspaceBridge -Operation healthRepairExecute -Token $Preview.PreviewToken -Payload @{ Finding=$Finding; Confirmed=$true }
    }
}
