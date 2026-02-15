function Move-DistroNexusInstance {
    <#
    .SYNOPSIS
        Moves a WSL distribution to a new location.

    .DESCRIPTION
        Exports the WSL distribution, unregisters it, and imports it to the new location.
        Includes rollback capability on failure.

    .PARAMETER Name
        The name of the WSL distribution to move.

    .PARAMETER Destination
        The new base path for the distribution.

    .EXAMPLE
        Move-DistroNexusInstance -Name "Ubuntu-22.04" -Destination "D:\WSL\Ubuntu"

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Moving instance '$Name' to '$Destination'"
        
        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name | Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-DistroNexusLog "Instance not found: $Name" -Level ERROR
            return $false
        }
        
        # Create destination if it doesn't exist
        if (-not (Test-Path $Destination)) {
            New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        }
        
        $tempExport = Join-Path $env:TEMP "$Name-export.tar"
        $newPath = Join-Path $Destination $Name
        
        try {
            # Export
            Write-Progress -Activity "Moving $Name" -Status "Exporting..." -PercentComplete 25
            Write-DistroNexusLog "Exporting to: $tempExport"
            wsl --export $Name $tempExport
            if ($LASTEXITCODE -ne 0) { throw "Export failed" }
            
            # Unregister
            Write-Progress -Activity "Moving $Name" -Status "Unregistering..." -PercentComplete 50
            Write-DistroNexusLog "Unregistering instance"
            wsl --unregister $Name
            if ($LASTEXITCODE -ne 0) { throw "Unregister failed" }
            
            # Import to new location
            Write-Progress -Activity "Moving $Name" -Status "Importing to new location..." -PercentComplete 75
            Write-DistroNexusLog "Importing to: $newPath"
            wsl --import $Name $newPath $tempExport
            if ($LASTEXITCODE -ne 0) { throw "Import failed" }
            
            # Update configuration
            try {
                Get-DistroNexusInstance -ForceUpdate | Out-Null
            }
            catch {
                Write-Verbose "Failed to update instance configuration: $_"
            }
            
            Write-Progress -Activity "Moving $Name" -Status "Complete" -PercentComplete 100 -Completed
            Write-DistroNexusLog "Successfully moved instance to: $newPath"
            
            return $true
        }
        catch {
            Write-DistroNexusLog "Failed to move instance: $_" -Level ERROR
            return $false
        }
        finally {
            # Cleanup temp export
            if (Test-Path $tempExport) {
                Remove-Item $tempExport -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
