function Get-DistroNexusWslConfig {
    <#
    .SYNOPSIS
        Reads the global WSL configuration from ~/.wslconfig.

    .DESCRIPTION
        Parses the [wsl2] section of ~/.wslconfig as an INI file and returns a
        PSCustomObject with Memory, Processors, Swap, LocalhostForwarding, and
        NetworkingMode properties.

    .OUTPUTS
        PSCustomObject with Memory, Processors, Swap, LocalhostForwarding, NetworkingMode.

    .EXAMPLE
        Get-DistroNexusWslConfig
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $wslconfigPath = Join-Path $env:USERPROFILE ".wslconfig"

        $result = [PSCustomObject]@{
            PSTypeName          = 'DistroNexus.WslConfig'
            Memory              = $null
            Processors          = $null
            Swap                = $null
            LocalhostForwarding = $null
            NetworkingMode      = $null
            ConfigPath          = $wslconfigPath
        }

        if (-not (Test-Path $wslconfigPath)) {
            Write-Verbose ".wslconfig not found at $wslconfigPath; returning defaults."
            return $result
        }

        $inWsl2Section = $false
        foreach ($line in Get-Content -Path $wslconfigPath) {
            $line = $line.Trim()

            if ($line -match '^\[(.+)\]$') {
                $inWsl2Section = $matches[1] -eq 'wsl2'
                continue
            }

            if (-not $inWsl2Section) { continue }
            if ($line -match '^[#;]' -or $line -notmatch '=') { continue }

            $idx = $line.IndexOf('=')
            $key = $line.Substring(0, $idx).Trim().ToLower()
            $val = $line.Substring($idx + 1).Trim()

            switch ($key) {
                'memory'              { $result.Memory              = $val }
                'processors'          { $result.Processors          = $val }
                'swap'                { $result.Swap                = $val }
                'localhostforwarding' { $result.LocalhostForwarding = $val }
                'networkingmode'      { $result.NetworkingMode      = $val }
            }
        }

        return $result
    }
}
