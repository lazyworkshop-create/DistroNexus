function New-DistroNexusTemplateApplyPreview {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1,256)][string]$InstanceName,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1,256)][string]$TemplateId,
        [hashtable]$Variables = @{},
        [switch]$DeclineRecoveryOffer)
    if ($Variables.Count -gt 64) { throw 'Template variables exceed the supported limit.' }
    foreach ($entry in $Variables.GetEnumerator()) { if ($entry.Key.ToString().Length -gt 128 -or $entry.Value.ToString().Length -gt 4096) { throw 'A template variable is invalid.' } }
    if ($PSCmdlet.ShouldProcess($InstanceName, "Preview reviewed template $TemplateId")) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.apply.preview.v1' -Payload @{ InstanceName=$InstanceName; TemplateId=$TemplateId; Variables=$Variables; DeclineRecoveryOffer=[bool]$DeclineRecoveryOffer }
    }
}
function Start-DistroNexusTemplateApply {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ShouldProcess('Reviewed template preview', 'Start template application')) { Invoke-DistroNexusWorkspaceBridge -Operation 'template.apply.execute.v1' -Payload @{ PreviewToken=$PreviewToken } }
}
function Get-DistroNexusTemplateApplyOperation {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$OperationId)
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.apply.status.v1' -Payload @{ OperationId=$OperationId }
}
function Stop-DistroNexusTemplateApply {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$OperationId)
    if ($PSCmdlet.ShouldProcess($OperationId, 'Request template application cancellation')) { Invoke-DistroNexusWorkspaceBridge -Operation 'template.apply.cancel.v1' -Payload @{ OperationId=$OperationId } }
}
