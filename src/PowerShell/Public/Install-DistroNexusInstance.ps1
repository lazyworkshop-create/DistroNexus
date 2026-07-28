function Install-DistroNexusInstance {
    <#
    .SYNOPSIS
        Installs a WSL distribution to a custom location.

    .DESCRIPTION
        Downloads (if needed) and installs a WSL distribution to the specified path.
        Supports custom user configuration, interactive mode, automatic download, and terminal launching.

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

    .PARAMETER Interactive
        Use interactive mode to select distribution and configure settings.

    .PARAMETER AutoDownload
        Automatically download the package if not found in cache.

    .PARAMETER OpenTerminal
        Open a terminal window after successful installation.

    .PARAMETER Shell
        Default shell for the user (e.g., "bash", "zsh", "fish"). Defaults to "bash".

    .PARAMETER Locale
        Locale setting (e.g., "en_US.UTF-8"). If not specified, keeps system default.

    .PARAMETER SetAsDefault
        Set this instance as the default WSL distribution.

    .EXAMPLE
        Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu"

    .EXAMPLE
        Install-DistroNexusInstance -Interactive -AutoDownload -OpenTerminal

    .EXAMPLE
        $pass = ConvertTo-SecureString "MyPassword" -AsPlainText -Force
        Install-DistroNexusInstance -DistroName "Debian" -InstallPath "E:\Linux" `
            -Username "admin" -Password $pass -Shell "zsh" -Locale "en_US.UTF-8"

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(DefaultParameterSetName = 'Standard', SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Standard')]
        [Parameter(Mandatory = $false, ParameterSetName = 'Interactive')]
        [string]$DistroName,
        
        [Parameter(Mandatory = $true, ParameterSetName = 'Standard')]
        [Parameter(Mandatory = $false, ParameterSetName = 'Interactive')]
        [string]$InstallPath,
        
        [Parameter(Mandatory = $false)]
        [string]$InstanceName,
        
        [Parameter(Mandatory = $false)]
        [string]$Username = "root",
        
        [Parameter(Mandatory = $false)]
        [SecureString]$Password,
        
        [Parameter(Mandatory = $false, ParameterSetName = 'Interactive')]
        [switch]$Interactive,
        
        [Parameter(Mandatory = $false)]
        [switch]$AutoDownload,
        
        [Parameter(Mandatory = $false)]
        [switch]$OpenTerminal,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet("bash", "zsh", "fish", "sh")]
        [string]$Shell = "bash",
        
        [Parameter(Mandatory = $false)]
        [string]$Locale,
        
        [Parameter(Mandatory = $false)]
        [switch]$SetAsDefault
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        # Interactive mode
        if ($Interactive) {
            Write-Host "`n=== DistroNexus Interactive Installation ===" -ForegroundColor Cyan
            
            # Select distribution
            if (-not $DistroName) {
                $packages = Get-DistroNexusPackage
                $selected = $packages | Out-GridView -Title "Select Distribution to Install" -OutputMode Single
                
                if (-not $selected) {
                    Write-DistroNexusLog "No distribution selected" -Level WARN
                    return $false
                }
                
                $DistroName = $selected.DefaultName
            }
            
            # Get install path
            if (-not $InstallPath) {
                $defaultPath = Join-Path $env:USERPROFILE "WSL\$DistroName"
                $InstallPath = Read-Host "Install path [$defaultPath]"
                if ([string]::IsNullOrWhiteSpace($InstallPath)) {
                    $InstallPath = $defaultPath
                }
            }
            
            # Get instance name
            if (-not $InstanceName) {
                $InstanceName = Read-Host "Instance name [$DistroName]"
                if ([string]::IsNullOrWhiteSpace($InstanceName)) {
                    $InstanceName = $DistroName
                }
            }
            
            # Get username
            if ($Username -eq "root") {
                $inputUser = Read-Host "Username [root]"
                if (-not [string]::IsNullOrWhiteSpace($inputUser)) {
                    $Username = $inputUser
                }
            }
            
            # Get password if username is not root
            if ($Username -ne "root" -and -not $Password) {
                $Password = Read-Host -AsSecureString -Prompt "Password for $Username"
            }
        }
        
        # Set default instance name
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
            
            # Find distro in catalog (flat array format only)
            $distroInfo = $null
            $distroList = @()
            
            if ($config.Distros -is [Array]) {
                $distroList = $config.Distros
            }
            elseif ($config.Distros) {
                 $distroList = @($config.Distros)
            }
            
            # Find by Name (exact match) or Version/DefaultName
            $distroInfo = $distroList | Where-Object { 
                $_.Name -eq $DistroName -or $_.Version -eq $DistroName 
            } | Select-Object -First 1
            
            if (-not $distroInfo) {
                throw "Distribution '$DistroName' not found in catalog"
            }

            $catalogDefaultName = $null
            if ($distroInfo.PSObject.Properties['DefaultName'] -and -not [string]::IsNullOrWhiteSpace($distroInfo.DefaultName)) {
                $catalogDefaultName = [string]$distroInfo.DefaultName
            }
            elseif (-not [string]::IsNullOrWhiteSpace($DistroName)) {
                $catalogDefaultName = [string]$DistroName
            }
            
            # Get package path from distro info
            $packagePath = $distroInfo.LocalPath
            
            # Check if package is already cached
            if (-not $packagePath -or -not (Test-Path $packagePath)) {
                # Get package info using Get-DistroNexusPackage to check cache status
                $packageInfo = Get-DistroNexusPackage | Where-Object { 
                    $_.DefaultName -eq $catalogDefaultName -or
                    $_.Version -eq $DistroName -or
                    $_.Name -eq $DistroName -or
                    $_.Name -eq $catalogDefaultName
                } | Select-Object -First 1
                
                if ($packageInfo -and $packageInfo.IsCached -and $packageInfo.LocalPath) {
                    $packagePath = $packageInfo.LocalPath
                    Write-DistroNexusLog "Using cached package: $packagePath"
                }
            }
            
            # Auto-download if not found and AutoDownload is specified
            if ((-not $packagePath -or -not (Test-Path $packagePath)) -and $AutoDownload) {
                Write-DistroNexusLog "Package not cached, downloading using Save-DistroNexusPackage..."
                
                # Use Save-DistroNexusPackage for consistent download logic
                $downloadResult = Save-DistroNexusPackage -DefaultName $catalogDefaultName -ShowSpeed $true -SkipExisting $false
                
                if (-not $downloadResult) {
                    throw "Failed to download package for '$catalogDefaultName'"
                }

                if ($downloadResult.PSObject.Properties['Success'] -and -not $downloadResult.Success) {
                    $failedPackagesMessage = $null
                    if ($downloadResult.PSObject.Properties['FailedPackages'] -and $downloadResult.FailedPackages) {
                        $failedPackagesMessage = ($downloadResult.FailedPackages -join ', ')
                    }

                    if ($failedPackagesMessage) {
                        throw "Failed to download package for '$catalogDefaultName'. Failed packages: $failedPackagesMessage"
                    }

                    throw "Failed to download package for '$catalogDefaultName'."
                }
                
                # Get the package path after download
                $packageInfo = Get-DistroNexusPackage | Where-Object { 
                    $_.DefaultName -eq $catalogDefaultName -or
                    $_.Version -eq $DistroName -or
                    $_.Name -eq $DistroName -or
                    $_.Name -eq $catalogDefaultName
                } | Select-Object -First 1
                
                if ($packageInfo -and $packageInfo.LocalPath) {
                    $packagePath = $packageInfo.LocalPath
                    Write-DistroNexusLog "Download completed: $packagePath"
                }
                else {
                    throw "Package download succeeded but file not found"
                }
            }
            
            if (-not $packagePath -or -not (Test-Path $packagePath)) {
                throw "Package not found. Use -AutoDownload or run Save-DistroNexusPackage -DefaultName '$DistroName' first."
            }
            
            # Handle package format
            # .wsl, .tar, and .tar.gz files can be imported directly
            # Other formats need to be extracted first
            $finalPackagePath = $packagePath
            if ($packagePath -notmatch '\.(wsl|tar|tar\.gz)$') {
                Write-DistroNexusLog "Converting package format..."
                $extractedPath = Expand-DistroPackage -PackagePath $packagePath
                if ($extractedPath) {
                    $finalPackagePath = $extractedPath
                }
                else {
                    throw "Failed to extract package"
                }
            }
            
            # Create install directory
            if (-not (Test-Path $InstallPath)) {
                New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
            }
            
            # Import distribution
            Write-DistroNexusLog "Importing from: $finalPackagePath"
            Write-Progress -Activity "Installing WSL Instance" -Status "Importing $DistroName..." -PercentComplete 30
            
            wsl --import $InstanceName $InstallPath $finalPackagePath
            
            if ($LASTEXITCODE -ne 0) {
                throw "WSL import failed with exit code: $LASTEXITCODE"
            }
            
            Write-Progress -Activity "Installing WSL Instance" -Status "Configuring instance..." -PercentComplete 60
            
            # Configure user if not root
            if ($Username -ne "root") {
                Write-DistroNexusLog "Creating user: $Username"
                
                # Detect distro family for appropriate user creation commands
                $useWheel = $distroFamily -match "Fedora|RHEL|Rocky|Alma|openSUSE"
                $sudoGroup = if ($useWheel) { "wheel" } else { "sudo" }
                
                # Create user with home directory
                wsl --distribution $InstanceName -- bash -c "useradd -m -s /bin/$Shell $Username 2>/dev/null || true"
                
                # Set password if provided
                if ($Password) {
                    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
                        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
                    wsl --distribution $InstanceName -- bash -c "echo '${Username}:${plainPassword}' | chpasswd"
                }
                
                # Add to sudo/wheel group
                wsl --distribution $InstanceName -- bash -c "usermod -aG $sudoGroup $Username 2>/dev/null || true"
                
                # Configure wsl.conf for default user
                $wslConf = @"
[user]
default=$Username

[boot]
systemd=true

[network]
generateResolvConf=true
"@
                $wslConfEncoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($wslConf))
                wsl --distribution $InstanceName -- bash -c "echo '$wslConfEncoded' | base64 -d | sudo tee /etc/wsl.conf > /dev/null"
                
                Write-DistroNexusLog "User '$Username' created and configured as default"
            }
            
            # Set locale if specified
            if ($Locale) {
                Write-DistroNexusLog "Configuring locale: $Locale"
                wsl --distribution $InstanceName -- bash -c "locale-gen $Locale 2>/dev/null || true"
                wsl --distribution $InstanceName -- bash -c "update-locale LANG=$Locale 2>/dev/null || true"
            }
            
            # Set as default distribution if requested
            if ($SetAsDefault) {
                Write-DistroNexusLog "Setting as default distribution"
                wsl --set-default $InstanceName
            }
            
            # Terminate to apply wsl.conf changes
            if ($Username -ne "root") {
                Write-DistroNexusLog "Restarting instance to apply configuration..."
                wsl --terminate $InstanceName
                Start-Sleep -Seconds 2
            }
            
            Write-Progress -Activity "Installing WSL Instance" -Status "Complete" -PercentComplete 100
            Write-Progress -Activity "Installing WSL Instance" -Completed
            
            Write-DistroNexusLog "Successfully installed instance: $InstanceName"
            
            # Update configuration by seeding the cache with correct Distribution name
            try {
                $currentCache = Get-InstanceCache
                if (-not $currentCache) { $currentCache = @() }
                
                # Check if exists (should not, but safety check)
                $existing = $currentCache | Where-Object { $_.Name -eq $InstanceName }
                if ($existing) {
                    $existing.Distribution = $DistroName
                } else {
                    # Create temporary stub with correct Distribution name
                    # Get-DistroNexusInstance will populate the rest (DiskSize etc)
                    $stub = [PSCustomObject]@{
                        Name = $InstanceName
                        Distribution = $DistroName
                        State = "Stopped"
                        Version = 2
                        BasePath = $InstallLocation
                    }
                    $currentCache += $stub
                }
                
                Set-InstanceCache -Instances $currentCache
            } 
            catch {
                 Write-Verbose "Failed to pre-seed instance configuration: $_"
            }

            # Update configuration by rescanning instances
            try {
                Get-DistroNexusInstance -ForceUpdate | Out-Null
            }
            catch {
                Write-Verbose "Failed to update instance configuration: $_"
            }
            
            # Open terminal if requested
            if ($OpenTerminal) {
                Write-DistroNexusLog "Launching terminal..."
                Start-DistroNexusTerminal -Name $InstanceName
            }
            
            return $true
        }
        catch {
            Write-Progress -Activity "Installing WSL Instance" -Completed
            Write-DistroNexusLog "Installation failed: $_" -Level ERROR
            Write-Error -Message $_.Exception.Message `
                        -ErrorId "DistroNexus.InstallFailed" `
                        -Category OperationStopped `
                        -TargetObject $InstanceName `
                        -ErrorAction Continue
            
            # Cleanup on failure
            $existingAfterError = Get-DistroNexusInstance -Name $InstanceName -ForceUpdate | 
                Where-Object { $_.Name -eq $InstanceName }
            if ($existingAfterError) {
                Write-DistroNexusLog "Cleaning up failed installation..." -FileOnly
                wsl --unregister $InstanceName 2>$null
            }
            
            return $false
        }
    }
}
