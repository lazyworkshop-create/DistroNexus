function Get-DistroNexusCatalogSource {
    [CmdletBinding()]
    param()

    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.list.v1'
}

function Add-DistroNexusCatalogSource {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][uri]$Url,
        [string]$Description = '',
        [bool]$IsActive = $true
    )

    if (-not $PSCmdlet.ShouldProcess($Url.AbsoluteUri, "Add catalog source '$Name'")) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.add.v1' -Payload @{ Name = $Name; Url = $Url.AbsoluteUri; Description = $Description; IsActive = $IsActive }
}

function Set-DistroNexusCatalogSource {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][uri]$Url,
        [string]$Description = '',
        [bool]$IsActive = $true
    )

    if (-not $PSCmdlet.ShouldProcess($SourceId, 'Update catalog source')) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.update.v1' -Payload @{ SourceId = $SourceId; Name = $Name; Url = $Url.AbsoluteUri; Description = $Description; IsActive = $IsActive }
}

function Remove-DistroNexusCatalogSource {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId)

    if (-not $PSCmdlet.ShouldProcess($SourceId, 'Remove catalog source')) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.remove.v1' -Payload @{ SourceId = $SourceId }
}

function Test-DistroNexusCatalogSource {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][uri]$Url)

    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.test.v1' -Payload @{ Url = $Url.AbsoluteUri }
}

function Set-DistroNexusCatalogSourceActive {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId,
        [Parameter(Mandatory)][bool]$IsActive
    )

    if (-not $PSCmdlet.ShouldProcess($SourceId, "Set catalog source active state to $IsActive")) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.active.set.v1' -Payload @{ SourceId = $SourceId; IsActive = $IsActive }
}

function Set-DistroNexusCatalogSourceOrder {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$SourceId)

    if (-not $PSCmdlet.ShouldProcess('catalog sources', 'Reorder')) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.reorder.v1' -Payload @{ SourceIds = @($SourceId) }
}

function Get-DistroNexusDefaultCatalogSource {
    [CmdletBinding()]
    param()

    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.defaults.get.v1'
}

function Reset-DistroNexusCatalogSource {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param()

    if (-not $PSCmdlet.ShouldProcess('catalog sources', 'Reset to defaults')) { return $false }
    Invoke-DistroNexusWorkspaceBridge -Operation 'catalog-source.defaults.reset.v1'
}
