function Export-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidateNotNullOrEmpty()][ValidatePattern('^[^\r\n\0]{1,256}$')][string]$Name,
        [Parameter(Mandatory, Position = 1)][ValidateNotNullOrEmpty()][ValidatePattern('^[^\r\n\0]{1,1024}$')][string]$Destination,
        [switch]$StopRunning,
        [switch]$Force
    )

    process {
        if (-not $PSCmdlet.ShouldProcess($Name, 'Export reviewed WSL instance')) {
            return [pscustomobject]@{ Succeeded = $false; Operation = 'Export'; InstanceName = $Name; OutcomeCode = 'WhatIf' }
        }

        $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.export.preview.v1' -Payload @{ Name = $Name; Destination = $Destination; StopRunning = [bool]$StopRunning }
        Invoke-DistroNexusWorkspaceBridge -Operation 'instance.export.execute.v1' -Payload @{ PreviewToken = $preview.PreviewToken }
    }
}
