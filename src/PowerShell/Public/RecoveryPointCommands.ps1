function Get-DistroNexusRecoveryPoint {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.list.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Test-DistroNexusRecoveryPoint {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.verify.v1' -Id $Id
}
function Get-DistroNexusRecoveryPointCreatePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$RecoveryName, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DestinationRoot, [ValidateSet('Tar','Vhdx')][string]$Format='Tar')
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.preview-create.v1' -Payload @{ Request=@{ SourceInstance=$Name; Name=$RecoveryName; DestinationRoot=$DestinationRoot; Format=$Format } }
}
function New-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Preview, [Parameter(Mandatory)][psobject]$Request)
    process { if (-not $Preview.Token) { throw 'A current Core-issued recovery preview is required.' }; if (-not $PSCmdlet.ShouldProcess($Request.SourceInstance, 'Create recovery point')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }; Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.create.v1' -Payload @{ PreviewToken=$Preview.Token } }
}
function Get-DistroNexusRecoveryPointRestorePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$TargetInstance, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TargetDirectory)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.preview-restore.v1' -Payload @{ Request=@{ RecoveryPointId=$Id; TargetInstance=$TargetInstance; TargetDirectory=$TargetDirectory; VerifyChecksum=$true; ImportInPlace=$false } }
}
function Restore-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Preview, [Parameter(Mandatory)][psobject]$Request)
    process { if (-not $Preview.Token) { throw 'A current Core-issued recovery preview is required.' }; if (-not $PSCmdlet.ShouldProcess($Request.TargetInstance, 'Restore recovery point')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }; Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.restore.v1' -Payload @{ PreviewToken=$Preview.Token } }
}
function Get-DistroNexusRecoveryPointRemovePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.preview-remove.v1' -Id $Id
}
function Remove-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][guid]$Id, [psobject]$Preview)
    if (-not $Preview) { $Preview = Get-DistroNexusRecoveryPointRemovePreview -Id $Id }
    if (-not $Preview.Token) { throw 'A current Core-issued recovery deletion preview is required.' }
    if (-not $PSCmdlet.ShouldProcess($Id, 'Remove recovery point')) { return $Preview }
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.remove.v1' -Payload @{ PreviewToken=$Preview.Token }
}

function Get-DistroNexusRecoveryPointHistory {
    [CmdletBinding()]
    param([switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.history.v1'
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}
function Get-DistroNexusRecoveryPointRetention {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.retention.get.v1' -Payload @{ SourceInstance = $Name }
}
function Set-DistroNexusRecoveryPointRetention {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidateRange(1, 1000)][int]$Maximum, [Parameter(Mandatory)][psobject]$Preview)
    if (-not $Preview.Token -or $Preview.SourceInstance -ne $Name -or $Preview.Maximum -ne $Maximum) { throw 'A current Core-issued recovery retention preview is required.' }
    if (-not $PSCmdlet.ShouldProcess($Name, "Set recovery retention to $Maximum")) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.retention.set.v1' -Payload @{ PreviewToken = $Preview.Token }
}
function Get-DistroNexusRecoveryPointRetentionPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidateRange(1, 1000)][int]$Maximum)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.retention.preview.v1' -Payload @{ SourceInstance = $Name; Maximum = $Maximum }
}
function Set-DistroNexusRecoveryPointMetadata {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][psobject]$Preview)
    if (-not $Preview.Token) { throw 'A current Core-issued recovery metadata preview is required.' }
    if (-not $PSCmdlet.ShouldProcess($Preview.RecoveryPointId, 'Update recovery point metadata')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.notes.execute.v1' -Payload @{ PreviewToken=$Preview.Token }
}
function Get-DistroNexusRecoveryPointMetadataPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Description,[string[]]$Tag=@(),[switch]$Pinned)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.notes.preview.v1' -Payload @{ Id=$Id; Description=$Description; Tags=@($Tag); Pinned=[bool]$Pinned }
}
function Get-DistroNexusRecoveryPointClonePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Snapshot, [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$TargetInstance, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TargetDirectory, [switch]$ImportInPlace)
    Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.preview-clone.v1' -Payload @{ Request = @{ Snapshot = $Snapshot; TargetInstance = $TargetInstance; TargetDirectory = $TargetDirectory; ImportInPlace = [bool]$ImportInPlace } }
}
function Copy-DistroNexusRecoveryPoint {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory, ValueFromPipeline)][psobject]$Preview, [Parameter(Mandatory)][psobject]$Request)
    process { if (-not $Preview.Token) { throw 'A current Core-issued recovery preview is required.' }; if (-not $PSCmdlet.ShouldProcess($Request.TargetInstance, 'Clone recovery point')) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }; Invoke-DistroNexusWorkspaceBridge -Operation 'recovery.clone.v1' -Payload @{ PreviewToken=$Preview.Token } }
}
