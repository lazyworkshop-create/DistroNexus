function Get-DistroNexusTemplate {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param (
        [Parameter(ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Id,

        [Parameter(ValueFromPipelineByPropertyName = $true)]
        [string]$Category,
        [switch]$ForceRefresh,
        [string]$Query
    )

    process {
        if ($PSBoundParameters.ContainsKey('Id')) { return Invoke-DistroNexusWorkspaceBridge -Operation 'template.catalog.get.v1' -Payload @{ TemplateId = $Id } }
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.catalog.list.v1' -Payload @{ ForceRefresh = [bool]$ForceRefresh; Query = $Query; Category = $Category }
    }
}
