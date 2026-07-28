function Get-DistroNexusLifecycleOperationPreview {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Remove', 'Move', 'Rename', 'Export', 'Import')][string]$Operation,
        [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]{1,256}$')][string]$Name,
        [string]$Destination, [string]$NewName, [string]$Source, [string]$InstallPath,
        [switch]$KeepFiles, [switch]$StopRunning
    )
    $routes = @{ Remove='instance.remove.preview.v1'; Move='instance.move.preview.v1'; Rename='instance.rename.preview.v1'; Export='instance.export.preview.v1'; Import='instance.import.preview.v1' }
    $payload = @{ Name = $Name }
    switch ($Operation) {
        'Remove' { $payload.KeepFiles = [bool]$KeepFiles }
        'Move' { if ([string]::IsNullOrWhiteSpace($Destination)) { throw 'Destination is required.' }; $payload.Destination = $Destination }
        'Rename' { if ([string]::IsNullOrWhiteSpace($NewName)) { throw 'NewName is required.' }; $payload.NewName = $NewName }
        'Export' { if ([string]::IsNullOrWhiteSpace($Destination)) { throw 'Destination is required.' }; $payload.Destination = $Destination; $payload.StopRunning = [bool]$StopRunning }
        'Import' { if ([string]::IsNullOrWhiteSpace($Source) -or [string]::IsNullOrWhiteSpace($InstallPath)) { throw 'Source and InstallPath are required.' }; $payload.Source = $Source; $payload.InstallPath = $InstallPath }
    }
    Invoke-DistroNexusWorkspaceBridge -Operation $routes[$Operation] -Payload $payload
}

function Invoke-DistroNexusLifecycleOperation {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if (-not $PSCmdlet.ShouldProcess('reviewed lifecycle operation', 'Execute')) {
        return [pscustomobject]@{ Succeeded = $false; OutcomeCode = 'WhatIf' }
    }
    Invoke-DistroNexusWorkspaceBridge -Operation 'instance.lifecycle.execute.v1' -Payload @{ PreviewToken = $PreviewToken }
}
