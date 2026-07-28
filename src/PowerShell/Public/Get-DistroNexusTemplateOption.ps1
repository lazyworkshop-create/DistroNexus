function Get-DistroNexusTemplateOption {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [ValidateLength(1, 256)]
        [string]$TemplateId
    )

    Invoke-DistroNexusWorkspaceBridge -Operation 'template.catalog.options.v1' -Payload @{ TemplateId = $TemplateId }
}
