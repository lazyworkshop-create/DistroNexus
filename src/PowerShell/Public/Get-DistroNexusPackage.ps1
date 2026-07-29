function Get-DistroNexusPackage {
    <# .SYNOPSIS Gets catalog packages through the typed WorkspaceBridge contract. #>
    [CmdletBinding(DefaultParameterSetName = 'List')]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(ParameterSetName = 'List')][ValidateLength(1,256)][string]$Family,
        [Parameter(Mandatory, ParameterSetName = 'Search')][ValidateLength(1,256)][string]$Query,
        [Parameter(Mandatory, ParameterSetName = 'Get')][ValidateLength(1,256)][string]$Id,
        [Parameter(ParameterSetName = 'List')][switch]$ForceReload
    )
    process {
        switch ($PSCmdlet.ParameterSetName) {
            'Search' { Invoke-DistroNexusWorkspaceBridge -Operation 'catalog.search.v1' -Payload @{ Query = $Query }; break }
            'Get' { Invoke-DistroNexusWorkspaceBridge -Operation 'catalog.get.v1' -Payload @{ Id = $Id }; break }
            default {
                $payload = @{ ForceReload = [bool]$ForceReload }
                if ($Family) { $payload.Family = $Family }
                Invoke-DistroNexusWorkspaceBridge -Operation 'catalog.list.v1' -Payload $payload
            }
        }
    }
}
