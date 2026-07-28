function Start-DistroNexusInstance {
    <#
    .SYNOPSIS
        Starts a WSL distribution instance.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name
    )

    process {
        if (-not $PSCmdlet.ShouldProcess($Name, 'Start WSL instance')) { return $false }
        return [bool](Invoke-DistroNexusWorkspaceBridge -Operation 'instance.start.v1' -Payload @{ Name = $Name })
    }
}
