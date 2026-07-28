function Get-DistroNexusTemplateSource {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.sources.v1'
}

function Get-DistroNexusTemplateMarketplaceEntry {
    [CmdletBinding()]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.discover.v1'
}

function Get-DistroNexusTemplateMarketplaceStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId,
        [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ManifestDigest
    )
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.status.v1' -Payload @{ SourceId = $SourceId; TemplateId = $TemplateId; ManifestDigest = $ManifestDigest }
}

function Add-DistroNexusTemplateSource {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Url,
        [ValidateSet('Remote', 'UserLocal')][string]$Kind = 'Remote',
        [switch]$AcceptNonHttps
    )
    if ($PSCmdlet.ShouldProcess($Url, 'Add template marketplace source')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.add-source.v1' -Payload @{ Url = $Url; Kind = $Kind; AcceptNonHttps = [bool]$AcceptNonHttps }
    }
}

function Set-DistroNexusTemplateSource {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId, [Parameter(Mandatory)][bool]$Enabled)
    if ($PSCmdlet.ShouldProcess($SourceId, $(if ($Enabled) { 'Enable template marketplace source' } else { 'Disable template marketplace source' }))) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.set-enabled.v1' -Payload @{ SourceId = $SourceId; Enabled = $Enabled }
    }
}

function Remove-DistroNexusTemplateSource {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId)
    if ($PSCmdlet.ShouldProcess($SourceId, 'Remove template marketplace source')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.remove-source.v1' -Payload @{ SourceId = $SourceId }
    }
}

function Get-DistroNexusTemplateMarketplaceReview {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId,
        [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ManifestDigest
    )
    if ($PSCmdlet.ShouldProcess($TemplateId, 'Download, verify, and create a template marketplace review')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.review.v1' -Payload @{ SourceId = $SourceId; TemplateId = $TemplateId; ManifestDigest = $ManifestDigest }
    }
}

function Approve-DistroNexusTemplateMarketplaceCandidate {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ReviewToken)
    if ($PSCmdlet.ShouldProcess('Core-issued marketplace review grant', 'Approve reviewed marketplace candidate')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.approve.v1' -Payload @{ ReviewToken = $ReviewToken }
    }
}

function Save-DistroNexusTemplateMarketplaceArtifact {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId,
        [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ManifestDigest
    )
    if ($PSCmdlet.ShouldProcess($TemplateId, 'Download and verify template marketplace artifact')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.download.v1' -Payload @{ SourceId = $SourceId; TemplateId = $TemplateId; ManifestDigest = $ManifestDigest }
    }
}

function Get-DistroNexusTemplateMarketplaceHistory {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId)
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.history.v1' -Payload @{ TemplateId = $TemplateId }
}

function Restore-DistroNexusTemplateMarketplaceArtifact {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId, [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ArtifactSha256)
    if ($PSCmdlet.ShouldProcess($TemplateId, "Restore verified marketplace artifact $ArtifactSha256")) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.marketplace.rollback.v1' -Payload @{ TemplateId = $TemplateId; ArtifactSha256 = $ArtifactSha256 }
    }
}
