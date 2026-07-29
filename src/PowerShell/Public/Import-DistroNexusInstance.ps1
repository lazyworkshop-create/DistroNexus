function Import-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][ValidateNotNullOrEmpty()][ValidatePattern('^[^\r\n\0]{1,256}$')][string]$Name,
        [Parameter(Mandatory, Position = 1)][ValidateNotNullOrEmpty()][ValidatePattern('^[^\r\n\0]{1,1024}$')][string]$Source,
        [Parameter(Mandatory, Position = 2)][ValidateNotNullOrEmpty()][ValidatePattern('^[^\r\n\0]{1,1024}$')][string]$InstallPath
    )

    process {
        if (-not $PSCmdlet.ShouldProcess($Name, 'Import reviewed WSL instance')) {
            return [pscustomobject]@{ Succeeded = $false; Operation = 'Import'; InstanceName = $Name; OutcomeCode = 'WhatIf' }
        }

        $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.import.preview.v1' -Payload @{ Name = $Name; Source = $Source; InstallPath = $InstallPath }
        Invoke-DistroNexusWorkspaceBridge -Operation 'instance.import.execute.v1' -Payload @{ PreviewToken = $preview.PreviewToken }
    }
}
