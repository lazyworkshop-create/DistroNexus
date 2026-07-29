function Get-DistroNexusCapability {
    <#
    .SYNOPSIS
    Returns the Core capability snapshot for the host or one WSL distribution.
    .DESCRIPTION
    This read-only command is a thin adapter over the packaged Core bridge. It does
    not infer support from the Windows build or invoke a shell command itself.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Host')]
        [Alias('Host')]
        [switch]$HostSnapshot,
        [Parameter(Mandatory, ParameterSetName = 'Instance')]
        [ValidatePattern('^[^\r\n\0]+$')]
        [string]$Name,
        [switch]$AsJson
    )
    $value = if ($PSCmdlet.ParameterSetName -eq 'Host') {
        Invoke-DistroNexusWorkspaceBridge -Operation 'capability.host.v1'
    }
    else {
        Invoke-DistroNexusWorkspaceBridge -Operation 'capability.instance.v1' -Payload @{ InstanceName = $Name }
    }
    if ($AsJson) { return ($value | ConvertTo-Json -Depth 16) }
    $value
}
