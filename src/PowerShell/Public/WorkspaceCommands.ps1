function New-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][string]$Instance,[string]$ProjectPath='/',[Parameter(Mandatory)][Int64]$ExpectedRevision)
    $definition = [ordered]@{ Id = [guid]::NewGuid(); DisplayName = $Name; InstanceName = $Instance; ProjectPath = $ProjectPath; PreflightChecks = @(); ActionGroups = @(); ClosePolicy = @{ Mode = 'None'; ServiceNames = @() }; TrustState = 'Trusted'; Revision = 0; TrustedAt = $null; MissingInstanceRemediation = 'BlockWithGuidance' }
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewSave -Payload $definition -ExpectedRevision $ExpectedRevision }
    if ($PSCmdlet.ShouldProcess($Name, 'Create workspace')) { Invoke-DistroNexusWorkspaceBridge -Operation save -Payload $definition -ExpectedRevision $ExpectedRevision }
}

function Set-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][guid]$Id,[string]$Name,[string]$Instance,[Parameter(Mandatory)][Int64]$ExpectedRevision)
    $workspace = @(Invoke-DistroNexusWorkspaceBridge -Operation list | Where-Object { $_.Id -eq $Id })[0]
    if ($null -eq $workspace) { throw 'Workspace not found.' }
    if ($workspace.Revision -ne $ExpectedRevision) { throw 'Workspace.ConflictOrState: Workspace revision conflict.' }
    if ($PSBoundParameters.ContainsKey('Name')) { $workspace.DisplayName = $Name }
    if ($PSBoundParameters.ContainsKey('Instance')) { $workspace.InstanceName = $Instance }
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewSave -Payload $workspace -ExpectedRevision $ExpectedRevision }
    if ($PSCmdlet.ShouldProcess($Id, 'Update workspace')) { Invoke-DistroNexusWorkspaceBridge -Operation save -Payload $workspace -ExpectedRevision $ExpectedRevision }
}

function Copy-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][string]$Name,[Parameter(Mandatory)][Int64]$ExpectedRevision)
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewDuplicate -Id $Id -Name $Name -ExpectedRevision $ExpectedRevision }
    if ($PSCmdlet.ShouldProcess($Id, 'Duplicate workspace')) { Invoke-DistroNexusWorkspaceBridge -Operation duplicate -Id $Id -Name $Name -ExpectedRevision $ExpectedRevision }
}

function Remove-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][Int64]$ExpectedRevision)
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewRemove -Id $Id -ExpectedRevision $ExpectedRevision }
    if ($PSCmdlet.ShouldProcess($Id, 'Remove workspace')) { Invoke-DistroNexusWorkspaceBridge -Operation remove -Id $Id -ExpectedRevision $ExpectedRevision | Out-Null }
}

function Get-DistroNexusWorkspaceImportPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)
    $payload = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    Invoke-DistroNexusWorkspaceBridge -Operation previewImport -Payload $payload
}

function Import-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Path,[string]$ImportToken,[Parameter(Mandatory)][Int64]$ExpectedRevision)
    $payload = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewImportDryRun -Payload $payload -ExpectedRevision $ExpectedRevision }
    if ([string]::IsNullOrWhiteSpace($ImportToken)) { throw 'An import preview token is required.' }
    if ($PSCmdlet.ShouldProcess($Path, 'Import workspace as untrusted')) { Invoke-DistroNexusWorkspaceBridge -Operation import -Payload $payload -Token $ImportToken -ExpectedRevision $ExpectedRevision }
}

function Get-DistroNexusWorkspaceLaunchPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id)
    Invoke-DistroNexusWorkspaceBridge -Operation previewLaunch -Id $Id
}

function Approve-DistroNexusWorkspaceTrust {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][Int64]$ExpectedRevision)
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewApproveTrust -Id $Id -ExpectedRevision $ExpectedRevision }
    if ($PSCmdlet.ShouldProcess($Id, 'Approve workspace command trust')) { Invoke-DistroNexusWorkspaceBridge -Operation approveTrust -Id $Id -ExpectedRevision $ExpectedRevision }
}

function Invoke-DistroNexusWorkspace {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][Int64]$ExpectedRevision,[string]$LaunchToken,[switch]$Preview)
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewLaunchDryRun -Id $Id }
    $launchPreview = Get-DistroNexusWorkspaceLaunchPreview -Id $Id
    if ($Preview) { return $launchPreview }
    if ([string]::IsNullOrWhiteSpace($LaunchToken)) { throw 'A launch preview token is required.' }
    if ($PSCmdlet.ShouldProcess($Id, 'Request workspace launch from Core')) { Invoke-DistroNexusWorkspaceBridge -Operation launch -Id $Id -ExpectedRevision $ExpectedRevision -Token $LaunchToken }
}

function Get-DistroNexusWorkspaceActionRetryPreview {
    [CmdletBinding()]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][guid]$ActionId)
    Invoke-DistroNexusWorkspaceBridge -Operation previewRetry -Id $Id -ActionId $ActionId
}

function Retry-DistroNexusWorkspaceAction {
    [CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
    param([Parameter(Mandatory)][guid]$Id,[Parameter(Mandatory)][guid]$ActionId,[Parameter(Mandatory)][Int64]$ExpectedRevision,[string]$RetryToken)
    if ($WhatIfPreference) { return Invoke-DistroNexusWorkspaceBridge -Operation previewRetryDryRun -Id $Id -ActionId $ActionId -ExpectedRevision $ExpectedRevision }
    if ([string]::IsNullOrWhiteSpace($RetryToken)) { throw 'A retry preview token is required.' }
    if ($PSCmdlet.ShouldProcess($Id, 'Retry selected workspace action from Core')) { Invoke-DistroNexusWorkspaceBridge -Operation retry -Id $Id -ActionId $ActionId -ExpectedRevision $ExpectedRevision -Token $RetryToken }
}
