function Get-DistroNexusContainerRuntimeStatus {
    <#
    .SYNOPSIS
        Returns read-only Docker Desktop and Podman runtime diagnostics for a WSL distribution.
    .DESCRIPTION
        This command never changes Docker contexts, Podman machine state, containers, images, or storage.
        Podman-in-WSL and Podman Desktop on Windows are deliberately reported as separate facts.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern('^[^\r\n\0]+$')]
        [string]$Name
    )

    Invoke-DistroNexusWorkspaceBridge -Operation containerRuntimeStatus -Payload @{ InstanceName = $Name }
}
