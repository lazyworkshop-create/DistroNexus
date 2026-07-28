function Compress-DistroNexusInstance {
    <#
    .SYNOPSIS
        Compacts a registered WSL instance through the reviewed DistroNexus bridge contract.

    .DESCRIPTION
        Creates a read-only compaction preview then consumes its short-lived, same-user token.
        The current VHDX size is reported as a measurement, never as an estimate of reclaimable space.
        The bridge owns the registered VHDX identity, fixed method, privilege checks, stop/restart, and recovery.
    #>
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [ValidateNotNullOrEmpty()]
        [string[]]$Name,

        # Retained only for compatible callers. It never bypasses WhatIf or ShouldProcess.
        [switch]$Force
    )

    process {
        foreach ($instanceName in $Name) {
            if (-not $PSCmdlet.ShouldProcess("registered instance '$instanceName'", 'Compact VHDX')) {
                [PSCustomObject]@{
                    Succeeded = $false
                    InstanceName = $instanceName
                    OutcomeCode = 'WhatIf'
                    BeforeBytes = $null
                    AfterBytes = $null
                    SavedBytes = $null
                    Method = $null
                    Restarted = $false
                    RecoveryAction = 'None'
                }
                continue
            }

            # No caller-provided path, method, command, elevation flag, or execute fields are accepted.
            $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.compact.preview.v1' -Payload @{ Name = $instanceName }
            Invoke-DistroNexusWorkspaceBridge -Operation 'instance.compact.execute.v1' -Payload @{ PreviewToken = $preview.PreviewToken }
        }
    }
}
