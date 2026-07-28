function Stop-DistroNexusInstance {
    <#
    .SYNOPSIS
        Stops a running WSL distribution instance.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,
        [switch]$Force
    )

    process {
        if ($Force) { $ConfirmPreference = 'None' }
        if (-not $PSCmdlet.ShouldProcess($Name, 'Stop WSL instance')) { return $false }
        return [bool](Invoke-DistroNexusWorkspaceBridge -Operation 'instance.stop.v1' -Payload @{ Name = $Name })
    }
}
