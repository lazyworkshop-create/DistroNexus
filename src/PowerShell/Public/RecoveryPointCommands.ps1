function Get-DistroNexusRecoveryPoint {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation recoveryList
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Test-DistroNexusRecoveryPoint {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id)
    Invoke-DistroNexusWorkspaceBridge -Operation recoveryVerify -Id $Id
}
function Get-DistroNexusRecoveryPointCreatePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$RecoveryName, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DestinationRoot, [ValidateSet('Tar','Vhdx')][string]$Format='Tar')
    Invoke-DistroNexusWorkspaceBridge -Operation recoveryPreviewCreate -Payload @{ Request=@{ SourceInstance=$Name; Name=$RecoveryName; DestinationRoot=$DestinationRoot; Format=$Format } }
}
function New-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Preview, [Parameter(Mandatory)][psobject]$Request)
    process { if (-not $Preview.Token) { throw 'A current Core-issued recovery preview is required.' }; if (-not $PSCmdlet.ShouldProcess($Request.SourceInstance, 'Create recovery point')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }; Invoke-DistroNexusWorkspaceBridge -Operation recoveryCreate -Token $Preview.Token -Payload @{ Request=$Request } }
}
function Get-DistroNexusRecoveryPointRestorePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$TargetInstance, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TargetDirectory)
    Invoke-DistroNexusWorkspaceBridge -Operation recoveryPreviewRestore -Payload @{ Request=@{ RecoveryPointId=$Id; TargetInstance=$TargetInstance; TargetDirectory=$TargetDirectory; VerifyChecksum=$true; ImportInPlace=$false } }
}
function Restore-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Preview, [Parameter(Mandatory)][psobject]$Request)
    process { if (-not $Preview.Token) { throw 'A current Core-issued recovery preview is required.' }; if (-not $PSCmdlet.ShouldProcess($Request.TargetInstance, 'Restore recovery point')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }; Invoke-DistroNexusWorkspaceBridge -Operation recoveryRestore -Token $Preview.Token -Payload @{ Request=$Request } }
}
function Get-DistroNexusRecoveryPointRemovePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id)
    Invoke-DistroNexusWorkspaceBridge -Operation recoveryPreviewRemove -Id $Id
}
function Remove-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][guid]$Id, [psobject]$Preview)
    if (-not $Preview) { $Preview = Get-DistroNexusRecoveryPointRemovePreview -Id $Id }
    if (-not $Preview.Token) { throw 'A current Core-issued recovery deletion preview is required.' }
    if (-not $PSCmdlet.ShouldProcess($Id, 'Remove recovery point')) { return $Preview }
    Invoke-DistroNexusWorkspaceBridge -Operation recoveryRemove -Id $Id -Token $Preview.Token
}
