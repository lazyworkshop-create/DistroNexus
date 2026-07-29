function Invoke-DistroNexusHealthScan {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'health.scan.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Get-DistroNexusHealthHistory {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'health.history.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Get-DistroNexusDiagnosticLogOption {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'diagnostics.log-options.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 8) }; $value
}
function Get-DistroNexusHealthRepairPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Finding)
    process { if (-not $Finding.Id -or -not $Finding.RepairId) { throw 'A repairable Core health finding is required.' }; Invoke-DistroNexusWorkspaceBridge -Operation 'health.repair-preview.v1' -Payload @{ Finding=$Finding } }
}
function Repair-DistroNexusHealthFinding {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{32}$')][string]$PreviewToken)
    process {
        if (-not $PSCmdlet.ShouldProcess('Core-issued health repair preview', 'Repair health finding')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
        Invoke-DistroNexusWorkspaceBridge -Operation 'health.repair.v1' -Token $PreviewToken -Payload @{}
    }
}
