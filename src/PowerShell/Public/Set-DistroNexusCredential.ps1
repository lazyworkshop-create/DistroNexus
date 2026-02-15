function Set-DistroNexusCredential {
    <#
    .SYNOPSIS
        Sets or resets user credentials for a WSL distribution.

    .DESCRIPTION
        Changes the default username and/or password for a WSL instance.

    .PARAMETER Name
        The name of the WSL distribution.

    .PARAMETER Username
        The username to set as default.

    .PARAMETER Password
        The password for the user (SecureString).

    .EXAMPLE
        $pass = Read-Host -AsSecureString -Prompt "Password"
        Set-DistroNexusCredential -Name "Ubuntu-22.04" -Username "admin" -Password $pass

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        
        [Parameter(Mandatory = $true)]
        [string]$Username,
        
        [Parameter(Mandatory = $false)]
        [SecureString]$Password
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Setting credentials for instance: $Name"
        
        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name | Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-DistroNexusLog "Instance not found: $Name" -Level ERROR
            return $false
        }
        
        if (-not $PSCmdlet.ShouldProcess($Name, "Set credentials")) {
            return $false
        }
        
        try {
            # Set default user
            Write-DistroNexusLog "Setting default user to: $Username"
            
            # Create user if doesn't exist
            wsl -d $Name --exec useradd -m $Username 2>&1 | Out-Null
            
            # Set password if provided
            if ($Password) {
                $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
                    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
                )
                $chpasswdCmd = "echo '${Username}:${plainPassword}' | chpasswd"
                wsl -d $Name --exec bash -c $chpasswdCmd
                $plainPassword = $null # Clear from memory
            }
            
            # Add to sudo group
            wsl -d $Name --exec usermod -aG sudo $Username 2>&1 | Out-Null
            
            # Set as default user (requires registry edit)
            $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
            $keys = Get-ChildItem -Path $lxssPath
            foreach ($key in $keys) {
                $props = Get-ItemProperty -Path $key.PSPath
                if ($props.DistributionName -eq $Name) {
                    # Get UID
                    $uidOutput = wsl -d $Name --exec id -u $Username
                    if ($LASTEXITCODE -eq 0) {
                        $uid = [int]$uidOutput.Trim()
                        Set-ItemProperty -Path $key.PSPath -Name "DefaultUid" -Value $uid
                        Write-DistroNexusLog "Successfully set credentials for: $Name"
                        return $true
                    }
                }
            }
            
            Write-DistroNexusLog "Failed to update registry" -Level ERROR
            return $false
        }
        catch {
            Write-DistroNexusLog "Failed to set credentials: $_" -Level ERROR
            return $false
        }
    }
}
