function Set-DistroNexusWslConfig {
    <#
    .SYNOPSIS
        Updates global WSL configuration in ~/.wslconfig.

    .DESCRIPTION
        Performs line-based INI replacement in the [wsl2] section of ~/.wslconfig.
        Only the parameters you supply are changed; existing keys and comments are
        preserved. Creates the file (and [wsl2] section) if absent.
        Warns when Memory > 80% of host RAM.

    .PARAMETER Memory
        Maximum memory allocation, e.g. "4GB" or "512MB".

    .PARAMETER Processors
        Number of CPU processors to assign to WSL2 VMs (1 or more).

    .PARAMETER Swap
        Swap size, e.g. "2GB".

    .PARAMETER LocalhostForwarding
        Whether to forward localhost ports between host and WSL2.

    .PARAMETER NetworkingMode
        Network mode: "NAT" (default), "mirrored", etc.

    .EXAMPLE
        Set-DistroNexusWslConfig -Memory "4GB" -Processors 2

    .EXAMPLE
        Set-DistroNexusWslConfig -NetworkingMode "mirrored"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]$Memory,

        [Parameter(Mandatory = $false)]
        [ValidateScript({
            $max = [System.Environment]::ProcessorCount
            if ($_ -lt 1 -or $_ -gt $max) {
                throw "processors must be between 1 and $max (host logical CPU count)."
            }
            $true
        })]
        [int]$Processors,

        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]$Swap,

        [Parameter(Mandatory = $false)]
        [nullable[bool]]$LocalhostForwarding,

        [Parameter(Mandatory = $false)]
        [ValidateNotNullOrEmpty()]
        [string]$NetworkingMode
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        # ErrorId = "DistroNexus.WslConfigWriteFailed"

        $wslconfigPath = Join-Path $env:USERPROFILE ".wslconfig"

        # Memory > 80% of host RAM warning
        if ($Memory) {
            try {
                $hostRamBytes = [System.Environment]::WorkingSet
                # Parse memory value (basic: handles GB/MB)
                if ($Memory -match '^(\d+(?:\.\d+)?)(GB|MB)$') {
                    $amount = [double]$matches[1]
                    $unit   = $matches[2]
                    $reqBytes = if ($unit -eq 'GB') { $amount * 1GB } else { $amount * 1MB }
                    if ($hostRamBytes -gt 0 -and $reqBytes -gt ($hostRamBytes * 0.8)) {
                        Write-Warning "Requested Memory ($Memory) exceeds 80% of detected host RAM. This may cause performance issues."
                    }
                }
            }
            catch {
                Write-Verbose "Could not compare memory to host RAM: $_"
            }
        }

        # Read existing lines
        $lines = [System.Collections.Generic.List[string]]::new()
        if (Test-Path $wslconfigPath) {
            $lines.AddRange([string[]](Get-Content -Path $wslconfigPath))
        }

        # Build updates hashtable
        $updates = @{}
        if ($PSBoundParameters.ContainsKey('Memory'))              { $updates['memory']              = $Memory }
        if ($PSBoundParameters.ContainsKey('Processors'))          { $updates['processors']          = [string]$Processors }
        if ($PSBoundParameters.ContainsKey('Swap'))                { $updates['swap']                = $Swap }
        if ($PSBoundParameters.ContainsKey('LocalhostForwarding')) { $updates['localhostForwarding'] = $LocalhostForwarding.ToString().ToLower() }
        if ($PSBoundParameters.ContainsKey('NetworkingMode'))      { $updates['networkingMode']      = $NetworkingMode }

        if ($updates.Count -eq 0) {
            Write-Verbose "No parameters supplied; .wslconfig unchanged."
            return
        }

        # Ensure [wsl2] section exists
        $sectionIdx = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i].Trim() -eq '[wsl2]') { $sectionIdx = $i; break }
        }
        if ($sectionIdx -lt 0) {
            if ($lines.Count -gt 0 -and $lines[$lines.Count - 1].Trim() -ne '') {
                $lines.Add('')
            }
            $lines.Add('[wsl2]')
            $sectionIdx = $lines.Count - 1
        }

        $applied   = @{}
        $inWsl2    = $false
        $wsl2EndIdx = $lines.Count

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i].Trim()

            if ($line -match '^\[') {
                if ($inWsl2) { $wsl2EndIdx = $i; break }
                $inWsl2 = $line -eq '[wsl2]'
                continue
            }

            if (-not $inWsl2) { continue }
            if ($line -match '^[#;]' -or $line -notmatch '=') { continue }

            $idx = $line.IndexOf('=')
            $key = $line.Substring(0, $idx).Trim()

            if ($updates.ContainsKey($key)) {
                $lines[$i] = "$key=$($updates[$key])"
                $applied[$key] = $true
            }
        }

        # Append new keys
        foreach ($kv in $updates.GetEnumerator()) {
            if (-not $applied.ContainsKey($kv.Key)) {
                $lines.Insert($wsl2EndIdx, "$($kv.Key)=$($kv.Value)")
                $wsl2EndIdx++
            }
        }

        $lines | Set-Content -Path $wslconfigPath -Encoding UTF8 -Force
        Write-DistroNexusLog "Updated .wslconfig at $wslconfigPath" -FileOnly
        Write-Verbose ".wslconfig updated."
    }
}
