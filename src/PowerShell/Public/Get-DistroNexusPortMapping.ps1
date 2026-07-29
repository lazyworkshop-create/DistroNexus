function Get-DistroNexusPortMapping {
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param([Parameter(Mandatory, Position = 0)][ValidateNotNullOrEmpty()][string]$Name, [ValidateSet('TCP','UDP','All')][string]$Protocol = 'All')
    $payload = @{ Name = $Name }
    if ($Protocol -ne 'All') { $payload.Protocol = $Protocol }
    @(Invoke-DistroNexusWorkspaceBridge -Operation 'network.port-mappings.v1' -Payload $payload)
}
