function Get-DistroNexusMonitoringSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [ValidateSet(1,2,5,10)][int]$IntervalSeconds = 2, [switch]$AsJson)
    $value = Invoke-DistroNexusWorkspaceBridge -Operation 'monitoring.snapshot.v1' -Payload @{ Name=$Name; IntervalSeconds=$IntervalSeconds }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }; $value
}

function Get-DistroNexusMonitoringProcessActionPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$SnapshotToken, [Parameter(Mandatory)][ValidateRange(2,2147483647)][int]$ProcessId, [Parameter(Mandatory)][ValidateSet('Terminate','Kill','Renice')][string]$Action)
    Invoke-DistroNexusWorkspaceBridge -Operation 'monitoring.process.preview.v1' -Payload @{ SnapshotToken=$SnapshotToken; ProcessId=$ProcessId; Action=$Action }
}

function Invoke-DistroNexusMonitoringProcessAction {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ShouldProcess('WSL process', 'Apply approved monitoring process action')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'monitoring.process.execute.v1' -Payload @{ PreviewToken=$PreviewToken }
    }
}
