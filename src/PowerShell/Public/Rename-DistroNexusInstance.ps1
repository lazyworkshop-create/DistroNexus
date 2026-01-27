function Rename-DistroNexusInstance {
    <#
    .SYNOPSIS
        Renames a WSL distribution instance.

    .DESCRIPTION
        Changes the registered name of a WSL distribution by export/import workflow.

    .PARAMETER Name
        The current name of the WSL distribution.

    .PARAMETER NewName
        The new name for the distribution.

    .EXAMPLE
        Rename-DistroNexusInstance -Name "Ubuntu" -NewName "Ubuntu-Dev"

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        
        [Parameter(Mandatory = $true)]
        [string]$NewName
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Renaming instance from '$Name' to '$NewName'"
        
        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name | Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-DistroNexusLog "Instance not found: $Name" -Level ERROR
            return $false
        }
        
        # Check new name doesn't exist
        $existing = Get-DistroNexusInstance -Name $NewName | Where-Object { $_.Name -eq $NewName }
        if ($existing) {
            Write-DistroNexusLog "Instance with name '$NewName' already exists" -Level ERROR
            return $false
        }
        
        if (-not $PSCmdlet.ShouldProcess($Name, "Rename to $NewName")) {
            return $false
        }
        
        $tempExport = Join-Path $env:TEMP "$Name-rename.tar"
        $originalPath = $instance.BasePath
        
        try {
            # Export
            Write-DistroNexusLog "Exporting instance..."
            wsl --export $Name $tempExport
            if ($LASTEXITCODE -ne 0) { throw "Export failed" }
            
            # Unregister old
            Write-DistroNexusLog "Unregistering old instance..."
            wsl --unregister $Name
            if ($LASTEXITCODE -ne 0) { throw "Unregister failed" }
            
            # Import with new name
            Write-DistroNexusLog "Importing with new name..."
            $newPath = $originalPath -replace [regex]::Escape($Name), $NewName
            wsl --import $NewName $newPath $tempExport
            if ($LASTEXITCODE -ne 0) { throw "Import failed" }
            
            Write-DistroNexusLog "Successfully renamed instance to: $NewName"
            return $true
        }
        catch {
            Write-DistroNexusLog "Failed to rename instance: $_" -Level ERROR
            return $false
        }
        finally {
            if (Test-Path $tempExport) {
                Remove-Item $tempExport -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
