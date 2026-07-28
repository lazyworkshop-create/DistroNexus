function Start-DistroNexusInstance {
    <#
    .SYNOPSIS
        Starts a WSL distribution instance.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,
        [switch]$KeepAlive
    )

    process {
        if (-not $PSCmdlet.ShouldProcess($Name, 'Start WSL instance')) { return $false }
        return Invoke-DistroNexusWorkspaceBridge -Operation 'instance.start.v1' -Payload @{ Name = $Name; KeepAlive = [bool]$KeepAlive }
    }
}
