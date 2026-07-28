function New-DistroNexusBackupSchedule {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name,[ValidatePattern('^[^\r\n\0]+$')][string]$Destination,[Parameter(Mandatory)][ValidateSet('Daily','Weekly:Monday','Weekly:Tuesday','Weekly:Wednesday','Weekly:Thursday','Weekly:Friday','Weekly:Saturday','Weekly:Sunday','Monthly:1','Monthly:2','Monthly:3','Monthly:4','Monthly:5','Monthly:6','Monthly:7','Monthly:8','Monthly:9','Monthly:10','Monthly:11','Monthly:12','Monthly:13','Monthly:14','Monthly:15','Monthly:16','Monthly:17','Monthly:18','Monthly:19','Monthly:20','Monthly:21','Monthly:22','Monthly:23','Monthly:24','Monthly:25','Monthly:26','Monthly:27','Monthly:28','Monthly:29','Monthly:30','Monthly:31')][string]$Frequency,[Parameter(Mandatory)][ValidateRange(1,30)][int]$RetentionCount,[Parameter(Mandatory)][TimeSpan]$Time)
    # Destination is a legacy preview-only field.  The fixed runtime never receives it at execute time.
    $proceed = if ($null -eq $PSCmdlet) { -not $WhatIfPreference } else { $PSCmdlet.ShouldProcess($Name, 'Create fixed backup schedule') }
    if (-not $proceed) { return [pscustomobject]@{ Succeeded=$false; OutcomeCode='WhatIf' } }
    $payload = @{ InstanceName=$Name; Frequency=$Frequency; RetentionCount=$RetentionCount; Time=$Time }
    if ($PSBoundParameters.ContainsKey('Destination')) { $payload.Destination = $Destination }
    $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'backup.schedule.preview.v1' -Payload $payload
    Invoke-DistroNexusWorkspaceBridge -Operation 'backup.execute.v1' -Payload @{ PreviewToken=$preview.Token }
}
