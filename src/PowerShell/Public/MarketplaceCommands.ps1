function Get-DistroNexusTemplateSource {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()
    Invoke-DistroNexusWorkspaceBridge -Operation marketplaceListSources
}

function Add-DistroNexusTemplateSource {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Url,
        [ValidateSet('Remote', 'UserLocal')][string]$Kind = 'Remote',
        [switch]$AcceptNonHttpsOrLocalSource
    )
    $payload = @{ Url = $Url; Kind = $Kind; ExplicitlyAcceptedNonHttps = [bool]$AcceptNonHttpsOrLocalSource }
    if ($WhatIfPreference) { return [PSCustomObject]@{ Operation = 'AddTemplateSource'; Url = $Url; Kind = $Kind; ExplicitConfirmationRequired = ($Kind -eq 'UserLocal' -or -not $Url.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) } }
    if ($PSCmdlet.ShouldProcess($Url, 'Add template marketplace source')) { Invoke-DistroNexusWorkspaceBridge -Operation marketplaceAddSource -Payload $payload }
}

function Set-DistroNexusTemplateSourceEnabled {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId,
        [Parameter(Mandatory)][bool]$Enabled
    )
    $payload = @{ SourceId = $SourceId; Enabled = $Enabled }
    if ($WhatIfPreference) { return [PSCustomObject]@{ Operation = 'SetTemplateSourceEnabled'; SourceId = $SourceId; Enabled = $Enabled; ExplicitConfirmationRequired = $true } }
    if ($PSCmdlet.ShouldProcess($SourceId, $(if ($Enabled) { 'Enable template marketplace source' } else { 'Disable template marketplace source' }))) { Invoke-DistroNexusWorkspaceBridge -Operation marketplaceSetSourceEnabled -Payload $payload }
}

function Remove-DistroNexusTemplateSource {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId)
    $payload = @{ SourceId = $SourceId }
    if ($WhatIfPreference) { return [PSCustomObject]@{ Operation = 'RemoveTemplateSource'; SourceId = $SourceId; ExplicitConfirmationRequired = $true } }
    if ($PSCmdlet.ShouldProcess($SourceId, 'Remove template marketplace source')) { Invoke-DistroNexusWorkspaceBridge -Operation marketplaceRemoveSource -Payload $payload }
}

function Approve-DistroNexusTemplateMarketplaceCandidate {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ReviewToken
    )
    $payload = @{ ReviewToken = $ReviewToken }
    if ($WhatIfPreference) { return [PSCustomObject]@{ Operation = 'ApproveTemplateMarketplaceCandidate'; ReviewToken = '<Core-issued>'; ExplicitReviewRequired = $true } }
    if ($PSCmdlet.ShouldProcess('Core-issued marketplace review grant', 'Approve reviewed marketplace candidate')) { Invoke-DistroNexusWorkspaceBridge -Operation marketplaceApproveCandidate -Payload $payload }
}

function Get-DistroNexusTemplateMarketplaceReviewGrant {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId, [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$Sha256)
    Invoke-DistroNexusWorkspaceBridge -Operation marketplaceCreateReviewGrant -Payload @{ SourceId = $SourceId; Sha256 = $Sha256 }
}

function Save-DistroNexusTemplateMarketplaceArtifact {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PSCustomObject])]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SourceId, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId, [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ManifestDigest)
    $payload = @{ SourceId = $SourceId; TemplateId = $TemplateId; ManifestDigest = $ManifestDigest }
    if ($WhatIfPreference) { return [PSCustomObject]@{ Operation = 'DownloadTemplateArtifact'; SourceId = $SourceId; TemplateId = $TemplateId; ManifestDigest = $ManifestDigest; WritesCache = $true } }
    if ($PSCmdlet.ShouldProcess($SourceId, 'Download and verify template marketplace artifact')) {
        Invoke-DistroNexusWorkspaceBridge -Operation marketplaceDownloadArtifact -Payload $payload
    }
}

function Get-DistroNexusTemplateMarketplaceArtifactHistory {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId)
    Invoke-DistroNexusWorkspaceBridge -Operation marketplaceArtifactHistory -Payload @{ TemplateId = $TemplateId }
}

function Get-DistroNexusTemplateMarketplaceScriptDiff {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId,
        [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$Sha256
    )
    Invoke-DistroNexusWorkspaceBridge -Operation marketplaceScriptDiff -Payload @{ TemplateId = $TemplateId; Sha256 = $Sha256 }
}

function Restore-DistroNexusTemplateMarketplaceArtifact {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$TemplateId,
        [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$Sha256
    )
    $payload = @{ TemplateId = $TemplateId; Sha256 = $Sha256 }
    if ($WhatIfPreference) { return [PSCustomObject]@{ Operation = 'RestoreTemplateMarketplaceArtifact'; TemplateId = $TemplateId; Sha256 = $Sha256; ExplicitConfirmationRequired = $true } }
    if ($PSCmdlet.ShouldProcess($TemplateId, "Restore verified marketplace artifact $Sha256")) { Invoke-DistroNexusWorkspaceBridge -Operation marketplaceRollback -Payload $payload }
}
