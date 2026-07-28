function Set-DistroNexusSettings {
    <#
    .SYNOPSIS
        Updates modeled global DistroNexus settings.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    param(
        [string]$DefaultInstallPath,
        [string]$PackageCachePath,
        [string]$TerminalStartPath,
        [ValidateSet(1, 2)][int]$DefaultWslVersion,
        [string]$DefaultUsername,
        [string]$DefaultDistributionId,
        [bool]$EnableLogging,
        [string]$LogPath,
        [bool]$CheckUpdatesOnStartup,
        [uri]$CatalogUrl,
        [ValidateSet('Light', 'Dark', 'Auto')][string]$Theme,
        [string]$Language,
        [bool]$ShowConfirmationDialogs,
        [ValidateRange(1, 64)][int]$MaxConcurrentDownloads,
        [bool]$AutoRetryDownloads,
        [ValidateRange(0, 100)][int]$MaxRetryAttempts,
        [bool]$AutoSaveEnabled,
        [ValidateRange(1, 86400)][int]$AutoSaveInterval,
        [AllowNull()][string]$PowerShellModulePath,
        [string]$LocalhostForwardingHealthEndpoint
    )

    if (-not @($PSBoundParameters.Keys | Where-Object { $_ -notin @('WhatIf', 'Confirm') }).Count) {
        throw 'Specify at least one modeled settings parameter.'
    }

    if ($PSBoundParameters.ContainsKey('PowerShellModulePath') -and -not [string]::IsNullOrWhiteSpace($PowerShellModulePath)) {
        throw 'Settings.ModulePathRetired'
    }

    if (-not $PSCmdlet.ShouldProcess('global DistroNexus settings', 'Save')) { return $false }

    $settings = Get-DistroNexusSettings
    foreach ($name in @(
        'DefaultInstallPath', 'PackageCachePath', 'TerminalStartPath', 'DefaultWslVersion', 'DefaultUsername',
        'DefaultDistributionId', 'EnableLogging', 'LogPath', 'CheckUpdatesOnStartup', 'Theme', 'Language',
        'ShowConfirmationDialogs', 'MaxConcurrentDownloads', 'AutoRetryDownloads', 'MaxRetryAttempts',
        'AutoSaveEnabled', 'AutoSaveInterval', 'LocalhostForwardingHealthEndpoint'
    )) {
        if ($PSBoundParameters.ContainsKey($name)) { $settings.$name = $PSBoundParameters[$name] }
    }
    if ($PSBoundParameters.ContainsKey('CatalogUrl')) { $settings.CatalogUrl = $CatalogUrl.AbsoluteUri }

    Invoke-DistroNexusWorkspaceBridge -Operation 'settings.save.v1' -Payload @{ Settings = $settings }
}
