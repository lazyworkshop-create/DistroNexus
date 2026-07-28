function Test-DistroNexusTemplateCompatibility {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 256)][string]$TemplateId, [Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 256)][string]$DistributionName)
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.compatibility.v1' -Payload @{ TemplateId = $TemplateId; DistributionName = $DistributionName }
}

function Get-DistroNexusTemplateImportPreview {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 1048576)][string]$Content)
    if ($PSCmdlet.ShouldProcess('bounded template content', 'Validate template import')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.import-preview.v1' -Payload @{ Content = $Content }
    }
}

function Import-DistroNexusTemplate {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ShouldProcess('Core-issued template import preview', 'Import template')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.import-execute.v1' -Payload @{ PreviewToken = $PreviewToken }
    }
}

function Get-DistroNexusTemplateExportPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 256)][string]$TemplateId)
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.export-preview.v1' -Payload @{ TemplateId = $TemplateId }
}

function Export-DistroNexusTemplate {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.export-execute.v1' -Payload @{ PreviewToken = $PreviewToken }
}

function Get-DistroNexusTemplateRemovePreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateNotNullOrEmpty()][ValidateLength(1, 256)][string]$TemplateId)
    Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.remove-preview.v1' -Payload @{ TemplateId = $TemplateId }
}

function Remove-DistroNexusTemplate {
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)
    if ($PSCmdlet.ShouldProcess('Core-issued template remove preview', 'Remove custom template')) {
        Invoke-DistroNexusWorkspaceBridge -Operation 'template.local.remove-execute.v1' -Payload @{ PreviewToken = $PreviewToken }
    }
}
