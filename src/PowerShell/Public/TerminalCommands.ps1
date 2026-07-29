function Get-DistroNexusTerminalStatus { [CmdletBinding()] param() Invoke-DistroNexusWorkspaceBridge -Operation 'terminal.status.v1' }
function Start-DistroNexusTerminal {
    [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]{1,256}$')][string]$Name, [ValidatePattern('^(~|/(?!.*//)(?!.*(?:^|/)\.\.(?:/|$)).{0,1022})$')][string]$StartPath, [ValidateSet('Auto','WindowsTerminal','CommandPrompt')][string]$TerminalKind = 'Auto')
    if (-not $PSCmdlet.ShouldProcess($Name, 'Launch terminal')) { return [pscustomobject]@{ Succeeded=$false; SelectedKind=$TerminalKind; OutcomeCode='Terminal.NotStarted' } }
    $payload=@{ InstanceName=$Name; TerminalKind=$TerminalKind }; if ($PSBoundParameters.ContainsKey('StartPath')) { $payload.StartPath=$StartPath }; Invoke-DistroNexusWorkspaceBridge -Operation 'terminal.launch.v1' -Payload $payload
}
function Open-DistroNexusPackageCacheFolder { [CmdletBinding(SupportsShouldProcess)] param() if (-not $PSCmdlet.ShouldProcess('configured package cache', 'Open in Explorer')) { return [pscustomobject]@{ Succeeded=$false; SelectedKind='Auto'; OutcomeCode='PackageCache.NotOpened' } }; Invoke-DistroNexusWorkspaceBridge -Operation 'explorer.package-cache.v1' }
