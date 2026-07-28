function Get-DistroNexusSystemdServicePreview {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name,
        [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit,
        [Parameter(Mandatory)][ValidateSet('Start', 'Stop', 'Restart', 'Enable', 'Disable', 'Reload')][string]$Action,
        [ValidateSet('User', 'System')][string]$Scope = 'User'
    )
    Invoke-DistroNexusWorkspaceBridge -Operation 'systemd.preview.v1' -Payload @{ InstanceName = $Name; Unit = $Unit; Action = $Action; Scope = $Scope }
}

function Invoke-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory, ValueFromPipeline)][ValidatePattern('^[a-fA-F0-9]{32}$')][string]$PreviewToken)
    process {
        if (-not $PSCmdlet.ShouldProcess('reviewed systemd operation', 'Execute')) {
            return [PSCustomObject]@{ Succeeded = $false; OutcomeCode = 'WhatIf'; PreviewToken = $null }
        }
        Invoke-DistroNexusWorkspaceBridge -Operation 'systemd.execute.v1' -Payload @{ PreviewToken = $PreviewToken }
    }
}

function Start-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    $preview = Get-DistroNexusSystemdServicePreview -Name $Name -Unit $Unit -Action Start -Scope $Scope
    if ($WhatIfPreference) { return $preview }
    if ($PSCmdlet.ShouldProcess("${Name}:$Unit", 'Start systemd service')) { Invoke-DistroNexusSystemdService -PreviewToken $preview.PreviewToken -Confirm:$false }
}

function Stop-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    $preview = Get-DistroNexusSystemdServicePreview -Name $Name -Unit $Unit -Action Stop -Scope $Scope
    if ($WhatIfPreference) { return $preview }
    if ($PSCmdlet.ShouldProcess("${Name}:$Unit", 'Stop systemd service')) { Invoke-DistroNexusSystemdService -PreviewToken $preview.PreviewToken -Confirm:$false }
}

function Restart-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    $preview = Get-DistroNexusSystemdServicePreview -Name $Name -Unit $Unit -Action Restart -Scope $Scope
    if ($WhatIfPreference) { return $preview }
    if ($PSCmdlet.ShouldProcess("${Name}:$Unit", 'Restart systemd service')) { Invoke-DistroNexusSystemdService -PreviewToken $preview.PreviewToken -Confirm:$false }
}

function Enable-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    $preview = Get-DistroNexusSystemdServicePreview -Name $Name -Unit $Unit -Action Enable -Scope $Scope
    if ($WhatIfPreference) { return $preview }
    if ($PSCmdlet.ShouldProcess("${Name}:$Unit", 'Enable systemd service')) { Invoke-DistroNexusSystemdService -PreviewToken $preview.PreviewToken -Confirm:$false }
}

function Disable-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    $preview = Get-DistroNexusSystemdServicePreview -Name $Name -Unit $Unit -Action Disable -Scope $Scope
    if ($WhatIfPreference) { return $preview }
    if ($PSCmdlet.ShouldProcess("${Name}:$Unit", 'Disable systemd service')) { Invoke-DistroNexusSystemdService -PreviewToken $preview.PreviewToken -Confirm:$false }
}

function Reload-DistroNexusSystemdService {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]+$')][string]$Name, [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9@_.-]+\.(service|socket)$')][string]$Unit, [ValidateSet('User', 'System')][string]$Scope = 'User')
    $preview = Get-DistroNexusSystemdServicePreview -Name $Name -Unit $Unit -Action Reload -Scope $Scope
    if ($WhatIfPreference) { return $preview }
    if ($PSCmdlet.ShouldProcess("${Name}:$Unit", 'Reload systemd service')) { Invoke-DistroNexusSystemdService -PreviewToken $preview.PreviewToken -Confirm:$false }
}
