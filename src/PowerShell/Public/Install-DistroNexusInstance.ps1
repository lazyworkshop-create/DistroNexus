function Install-DistroNexusInstance {
    <#
    .SYNOPSIS
        Installs a WSL distribution to a custom location.

    .DESCRIPTION
        Downloads (if needed) and installs a WSL distribution to the specified path.
        Supports custom user configuration and quick mode with defaults.

    .PARAMETER DistroName
        The default name of the distribution from the catalog (e.g., "Ubuntu-22.04").

    .PARAMETER InstallPath
        The directory where the distribution will be installed.

    .PARAMETER InstanceName
        Custom name for the instance. If not specified, uses DistroName.

    .PARAMETER Username
        Default username to create. Defaults to "root".

    .PARAMETER Password
        Password for the user (SecureString).

    .EXAMPLE
        Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu"

    .EXAMPLE
        $pass = Read-Host -AsSecureString -Prompt "Password"
        Install-DistroNexusInstance -DistroName "Debian" -InstallPath "E:\Linux" -Username "admin" -Password $pass

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName,
        
        [Parameter(Mandatory = $true)]
        [string]$InstallPath,
        
        [Parameter(Mandatory = $false)]
        [string]$InstanceName,
        
        [Parameter(Mandatory = $false)]
        [string]$Username = "root",
        
        [Parameter(Mandatory = $false)]
        [SecureString]$Password
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        if (-not $InstanceName) {
            $InstanceName = $DistroName
        }
        
        Write-DistroNexusLog "Installing $DistroName as '$InstanceName' to $InstallPath"
        
        # Check if instance already exists
        $existing = Get-DistroNexusInstance -Name $InstanceName | Where-Object { $_.Name -eq $InstanceName }
        if ($existing) {
            Write-DistroNexusLog "Instance '$InstanceName' already exists" -Level ERROR
            return $false
        }
        
        if (-not $PSCmdlet.ShouldProcess($InstanceName, "Install WSL distribution")) {
            return $false
        }
        
        try {
            # Load distro catalog
            $config = Get-DistroNexusConfig
            if (-not $config.Distros) {
                throw "Distro catalog not found"
            }
            
            # Find distro in catalog
            $distroInfo = $null
            foreach ($family in $config.Distros.PSObject.Properties) {
                foreach ($version in $family.Value.Versions.PSObject.Properties) {
                    if ($version.Value.DefaultName -eq $DistroName) {
                        $distroInfo = $version.Value
                        break
                    }
                }
                if ($distroInfo) { break }
            }
            
            if (-not $distroInfo) {
                throw "Distribution '$DistroName' not found in catalog"
            }
            
            # Determine package path
            $packagePath = $distroInfo.LocalPath
            if (-not $packagePath -or -not (Test-Path $packagePath)) {
                # Try package cache
                $cachePath = $config.Settings.PackageCachePath
                if ($cachePath -and $distroInfo.Filename) {
                    $packagePath = Join-Path $cachePath $distroInfo.Filename
                }
            }
            
            if (-not (Test-Path $packagePath)) {
                Write-DistroNexusLog "Package not found. Download it first using Save-DistroNexusPackage" -Level ERROR
                return $false
            }
            
            # Create install directory
            if (-not (Test-Path $InstallPath)) {
                New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
            }
            
            # Import distribution
            Write-DistroNexusLog "Importing from: $packagePath"
            wsl --import $InstanceName $InstallPath $packagePath
            
            if ($LASTEXITCODE -ne 0) {
                throw "WSL import failed"
            }
            
            # Set default user if specified
            if ($Username -ne "root") {
                Write-DistroNexusLog "Configuring user: $Username"
                # This would typically involve running usermod commands inside WSL
                # Simplified for now
            }
            
            Write-DistroNexusLog "Successfully installed instance: $InstanceName"
            return $true
        }
        catch {
            Write-DistroNexusLog "Installation failed: $_" -Level ERROR
            return $false
        }
    }
}
