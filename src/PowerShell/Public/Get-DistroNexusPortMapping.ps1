function Get-DistroNexusPortMapping {
    <#
    .SYNOPSIS
        Lists listening TCP/UDP ports inside a WSL instance and cross-references Windows portproxy rules.

    .DESCRIPTION
        Runs `ss -tlnp` and `ss -ulnp` inside the instance to enumerate listening ports,
        then checks `netsh interface portproxy show all` for matching Windows-side forwarding rules.
        Also returns the instance's WSL IP address from `hostname -I`.

    .PARAMETER Name
        The WSL instance name to query.

    .PARAMETER Protocol
        Optional protocol filter: TCP, UDP, or All (default: All).

    .OUTPUTS
        PSCustomObject with Protocol, LocalAddress, Port, ProcessName, Pid, HasWindowsProxy properties.

    .EXAMPLE
        Get-DistroNexusPortMapping -Name "Ubuntu-22.04"

    .EXAMPLE
        Get-DistroNexusPortMapping -Name "Ubuntu-22.04" -Protocol TCP
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $false)]
        [ValidateSet("TCP", "UDP", "All")]
        [string]$Protocol = "All"
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        Write-DistroNexusLog "Get-DistroNexusPortMapping: querying ports for '$Name'"

        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-Error "Instance '$Name' not found." -ErrorId "DistroNexus.InstanceNotFound"
            return
        }

        # Only available for running instances
        if ($instance.State -ne "Running") {
            Write-Warning "Instance '$Name' is not running. Port mapping information is unavailable."
            return @()
        }

        # Get WSL IP address (always fresh — changes on each WSL start)
        $ipAddress = $null
        try {
            $ipRaw = wsl -d $Name -e hostname -I 2>&1
            if ($LASTEXITCODE -eq 0) {
                $ipAddress = ($ipRaw -split '\s+')[0].Trim()
            }
        }
        catch {
            Write-DistroNexusLog "Could not get WSL IP for '$Name': $_" -Level WARN
        }

        # Collect netsh portproxy rules for cross-reference
        $proxyPorts = @{}
        try {
            $proxyOutput = netsh interface portproxy show all 2>&1
            foreach ($line in $proxyOutput) {
                if ($line -match '^\s*\S+\s+(\d+)\s+') {
                    $proxyPorts[$Matches[1]] = $true
                }
            }
        }
        catch {
            Write-DistroNexusLog "Could not query netsh portproxy: $_" -Level WARN
        }

        $results = [System.Collections.Generic.List[PSCustomObject]]::new()

        # TCP ports
        if ($Protocol -eq "All" -or $Protocol -eq "TCP") {
            $tcpLines = wsl -d $Name -e ss -tlnp 2>&1
            if ($LASTEXITCODE -eq 0) {
                foreach ($line in ($tcpLines | Select-Object -Skip 1)) {
                    $entry = ConvertFrom-SsLine -Line $line -Protocol "TCP" -ProxyPorts $proxyPorts
                    if ($entry) { $results.Add($entry) }
                }
            }
            else {
                Write-Warning "ss command not available in instance '$Name'. Install iproute2 to use this feature."
            }
        }

        # UDP ports
        if ($Protocol -eq "All" -or $Protocol -eq "UDP") {
            $udpLines = wsl -d $Name -e ss -ulnp 2>&1
            if ($LASTEXITCODE -eq 0) {
                foreach ($line in ($udpLines | Select-Object -Skip 1)) {
                    $entry = ConvertFrom-SsLine -Line $line -Protocol "UDP" -ProxyPorts $proxyPorts
                    if ($entry) { $results.Add($entry) }
                }
            }
        }

        Write-DistroNexusLog "Port mapping query complete for '$Name': $($results.Count) entries"

        # Return results annotated with IP address
        return $results | ForEach-Object {
            $_.PSObject.Properties.Add([System.Management.Automation.PSNoteProperty]::new('InstanceIpAddress', $ipAddress))
            $_
        }
    }
}

# ── Module-private helper ─────────────────────────────────────────────────────

function ConvertFrom-SsLine {
    <#
    .SYNOPSIS
        Parses a single line from `ss -tlnp` or `ss -ulnp` output into a port mapping object.
    #>
    param(
        [string]$Line,
        [string]$Protocol,
        [hashtable]$ProxyPorts
    )

    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }

    # ss format: Netid State Recv-Q Send-Q Local_Addr:Port Peer_Addr:Port [Process]
    # The Local Address:Port column contains the address
    $pattern = '(?:LISTEN|UNCONN)\s+\d+\s+\d+\s+(\S+):(\d+)\s+\S+(?:\s+users:\(\("?([^"]+)"?,pid=(\d+))?'

    if ($Line -match $pattern) {
        $localAddr  = $Matches[1]
        $port       = $Matches[2]
        $procName   = if ($Matches[3]) { $Matches[3] } else { '' }
        $procId     = if ($Matches[4]) { [int]$Matches[4] } else { 0 }
        $hasProxy   = $ProxyPorts.ContainsKey($port)

        return [PSCustomObject]@{
            PSTypeName      = 'DistroNexus.PortMapping'
            Protocol        = $Protocol
            LocalAddress    = $localAddr
            Port            = [int]$port
            ProcessName     = $procName
            Pid             = $procId
            HasWindowsProxy = $hasProxy
        }
    }

    return $null
}
