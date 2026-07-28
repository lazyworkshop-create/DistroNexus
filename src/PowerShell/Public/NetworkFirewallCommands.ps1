function Get-DistroNexusNetworkStatus { [CmdletBinding()] param() Invoke-DistroNexusWorkspaceBridge -Operation 'network.status.v1' }
function Test-FirewallRemoteScope {
    param([string]$Value)
    if ($Value -eq 'LocalSubnet') { return $true }
    $parts = $Value -split '/', 2
    $address = [System.Net.IPAddress]::None
    if (-not [System.Net.IPAddress]::TryParse($parts[0], [ref]$address)) { return $false }
    if ($parts.Count -eq 1) { return $true }
    $prefix = 0
    if (-not [int]::TryParse($parts[1], [ref]$prefix)) { return $false }
    $maximum = if ($address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) { 32 } else { 128 }
    return $prefix -ge 0 -and $prefix -le $maximum
}
function Get-DistroNexusInstanceIpAddress { [CmdletBinding()] param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')][string]$Name) Invoke-DistroNexusWorkspaceBridge -Operation 'network.ip.v1' -Payload @{ Name=$Name } }
function Test-DistroNexusNetworkProbe {
    [CmdletBinding()] param([Parameter(Mandatory)][ValidateSet('Dns','Gateway','Internet','WindowsHost','WslInstance','Localhost','TcpEndpoint')][string]$Kind,[Parameter(Mandatory)][Alias('Host')][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$')][string]$TargetHost,[ValidateRange(1,65535)][int]$Port,[ValidateRange(1,30)][int]$TimeoutSeconds=5,[ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')][string]$DistributionName)
    $request=@{ Kind=$Kind; Host=$TargetHost; Timeout="00:00:$TimeoutSeconds" }; if ($PSBoundParameters.ContainsKey('Port')) { $request.Port=$Port }; if ($PSBoundParameters.ContainsKey('DistributionName')) { $request.DistributionName=$DistributionName }; Invoke-DistroNexusWorkspaceBridge -Operation 'network.probe.v1' -Payload @{ Request=$request }
}
function Get-DistroNexusNetworkMode { [CmdletBinding()] param([Parameter(Mandatory)][ValidateSet('Nat','Mirrored','None','VirtioProxy','Bridged')][string]$Mode) Invoke-DistroNexusWorkspaceBridge -Operation 'network.mode.get.v1' -Payload @{ Mode=$Mode } }
function Get-DistroNexusNetworkModePreview { [CmdletBinding()] param([Parameter(Mandatory)][ValidateSet('Nat','Mirrored','None','VirtioProxy','Bridged')][string]$Mode) Invoke-DistroNexusWorkspaceBridge -Operation 'network.mode.preview.v1' -Payload @{ Mode=$Mode } }
function Set-DistroNexusNetworkMode { [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidateSet('Nat','Mirrored','None','VirtioProxy','Bridged')][string]$Mode,[Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9-]{16,128}$')][string]$PreviewToken) if ($PSCmdlet.ShouldProcess($Mode,'Apply reviewed WSL networking mode')) { Invoke-DistroNexusWorkspaceBridge -Operation 'network.mode.set.v1' -Token $PreviewToken -Payload @{ Mode=$Mode } } }
function New-NetworkSettingsPayload {
    param([hashtable]$Bound)
    $settings = @{}; foreach ($name in 'DnsTunneling','AutoProxy','Firewall','HostAddressLoopback','BestEffortDnsParsing','IgnoredPorts') { if ($Bound.ContainsKey($name)) { $settings[$name] = $Bound[$name] } }; if ($settings.Count -eq 0) { throw 'At least one modeled network setting is required.' }; return $settings
}
function Get-DistroNexusNetworkSettingsPreview {
    [CmdletBinding()] param([bool]$DnsTunneling,[bool]$AutoProxy,[bool]$Firewall,[bool]$HostAddressLoopback,[bool]$BestEffortDnsParsing,[ValidatePattern('^[0-9, -]{1,1024}$')][string]$IgnoredPorts)
    Invoke-DistroNexusWorkspaceBridge -Operation 'network.settings.preview.v1' -Payload @{ Settings=(New-NetworkSettingsPayload $PSBoundParameters) }
}
function Set-DistroNexusNetworkSettings {
    [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9-]{16,128}$')][string]$PreviewToken,[bool]$DnsTunneling,[bool]$AutoProxy,[bool]$Firewall,[bool]$HostAddressLoopback,[bool]$BestEffortDnsParsing,[ValidatePattern('^[0-9, -]{1,1024}$')][string]$IgnoredPorts)
    $settings = New-NetworkSettingsPayload $PSBoundParameters; if ($PSCmdlet.ShouldProcess('reviewed network settings','Apply reviewed WSL network settings')) { Invoke-DistroNexusWorkspaceBridge -Operation 'network.settings.set.v1' -Token $PreviewToken -Payload @{ Settings=$settings } }
}
function Get-DistroNexusFirewallRule { [CmdletBinding()] param() Invoke-DistroNexusWorkspaceBridge -Operation 'firewall.list.v1' }
function Get-DistroNexusFirewallRuleCreatePreview { [CmdletBinding()] param([Parameter(Mandatory)][ValidateSet('Inbound','Outbound')][string]$Direction,[Parameter(Mandatory)][ValidateSet('Tcp','Udp')][string]$Protocol,[Parameter(Mandatory)][ValidateRange(1,65535)][int]$Port,[Parameter(Mandatory)][ValidateSet('Domain','Private','Public')][string[]]$Profiles,[ValidateScript({ Test-FirewallRemoteScope $_ })][string]$RemoteScope,[ValidatePattern('^[A-Za-z]:\\[^\r\n\0]{1,240}$')][string]$ExecutableScope) Invoke-DistroNexusWorkspaceBridge -Operation 'firewall.preview-create.v1' -Payload @{ Request=@{Direction=$Direction;Protocol=$Protocol;Port=$Port;Profiles=$Profiles;RemoteScope=$RemoteScope;ExecutableScope=$ExecutableScope} } }
function New-DistroNexusFirewallRule { [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidatePattern('^DistroNexus-[A-F0-9]{16}$')][string]$PreviewRuleId) if ($PSCmdlet.ShouldProcess($PreviewRuleId,'Create reviewed DistroNexus firewall rule')) { Invoke-DistroNexusWorkspaceBridge -Operation 'firewall.create.v1' -Payload @{ PreviewRuleId=$PreviewRuleId } } }
function Get-DistroNexusFirewallRuleRemovePreview { [CmdletBinding()] param([Parameter(Mandatory)][ValidatePattern('^DistroNexus-[A-F0-9]{16}$')][string]$RuleId) Invoke-DistroNexusWorkspaceBridge -Operation 'firewall.preview-remove.v1' -Payload @{ RuleId=$RuleId } }
function Remove-DistroNexusFirewallRule { [CmdletBinding(SupportsShouldProcess)] param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9-]{16,128}$')][string]$PreviewToken) if ($PSCmdlet.ShouldProcess('reviewed DistroNexus firewall rule','Remove reviewed DistroNexus firewall rule')) { Invoke-DistroNexusWorkspaceBridge -Operation 'firewall.remove.v1' -Payload @{ PreviewToken=$PreviewToken } } }
