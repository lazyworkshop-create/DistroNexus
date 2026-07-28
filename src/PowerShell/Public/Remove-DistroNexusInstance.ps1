function Remove-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory, ValueFromPipeline, ValueFromPipelineByPropertyName)][ValidateNotNullOrEmpty()][string]$Name, [switch]$Force, [switch]$KeepFiles)
    process {
        if (-not $PSCmdlet.ShouldProcess($Name, 'Remove reviewed WSL instance')) { return [pscustomobject]@{ Succeeded=$false; Operation='Remove'; InstanceName=$Name; OutcomeCode='WhatIf' } }
        $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.remove.preview.v1' -Payload @{ Name=$Name; KeepFiles=[bool]$KeepFiles }
        Invoke-DistroNexusWorkspaceBridge -Operation 'instance.remove.execute.v1' -Payload @{ PreviewToken=$preview.PreviewToken }
    }
}
