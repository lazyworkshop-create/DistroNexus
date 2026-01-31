function Get-DistroNexusDiskSize {
    <#
    .SYNOPSIS
        Gets the disk size of a WSL instance.
    
    .DESCRIPTION
        Retrieves the size of the VHDX file for a specified WSL instance.
        
        ⚠️ WARNING: Accessing the VHDX file may auto-start a stopped instance.
        This cmdlet should only be used when:
        - The instance is already running, OR
        - The user explicitly requests disk size information
        
        For best performance and to avoid unintended instance startup,
        only call this on running instances or when necessary.
    
    .PARAMETER Name
        The name of the WSL instance to query.
    
    .EXAMPLE
        Get-DistroNexusDiskSize -Name "Ubuntu"
        # Gets disk size for Ubuntu instance
    
    .EXAMPLE
        Get-DistroNexusInstance | Where-Object State -eq "Running" | ForEach-Object {
            [PSCustomObject]@{
                Name = $_.Name
                DiskSize = (Get-DistroNexusDiskSize -Name $_.Name)
                DiskSizeGB = [math]::Round((Get-DistroNexusDiskSize -Name $_.Name) / 1GB, 2)
            }
        }
        # Safely get disk sizes for only running instances
    
    .EXAMPLE
        $instances = Get-DistroNexusInstance
        $runningInstances = $instances | Where-Object State -eq "Running"
        foreach ($instance in $runningInstances) {
            $size = Get-DistroNexusDiskSize -Name $instance.Name
            Write-Host "$($instance.Name): $([math]::Round($size / 1GB, 2)) GB"
        }
        # Display disk sizes for all running instances
    
    .OUTPUTS
        System.Int64
        The disk size in bytes, or 0 if unable to determine.
    
    .NOTES
        This function accesses the ext4.vhdx file, which may trigger automatic
        mounting and startup of stopped WSL instances. Use with caution.
    #>
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Name
    )
    
    begin {
        Initialize-DistroNexusLogger
        Write-DistroNexusLog "Getting disk size for instance: $Name" -FileOnly
    }
    
    process {
        try {
            # Get the instance's base path from registry
            $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
            if (-not (Test-Path $lxssPath)) {
                Write-Warning "WSL registry path not found"
                return 0
            }
            
            $basePath = $null
            $keys = Get-ChildItem -Path $lxssPath -ErrorAction SilentlyContinue
            
            foreach ($key in $keys) {
                try {
                    $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
                    if ($props.DistributionName -eq $Name) {
                        $basePath = $props.BasePath
                        break
                    }
                }
                catch {
                    # Skip problematic registry keys
                    continue
                }
            }
            
            if (-not $basePath) {
                Write-Warning "Instance '$Name' not found in registry"
                return 0
            }
            
            # ⚠️ WARNING: This may auto-start the instance!
            $vhdxPath = Join-Path $basePath "ext4.vhdx"
            
            if (Test-Path $vhdxPath) {
                $vhdxInfo = Get-Item $vhdxPath -ErrorAction Stop
                $diskSize = $vhdxInfo.Length
                
                Write-DistroNexusLog "Disk size for '$Name': $diskSize bytes" -FileOnly
                return $diskSize
            }
            else {
                Write-Warning "VHDX file not found for instance '$Name' at: $vhdxPath"
                return 0
            }
        }
        catch {
            Write-DistroNexusLog "Failed to get disk size for '$Name': $_" -Level ERROR
            Write-Error "Failed to get disk size for instance '$Name': $_"
            return 0
        }
    }
}
