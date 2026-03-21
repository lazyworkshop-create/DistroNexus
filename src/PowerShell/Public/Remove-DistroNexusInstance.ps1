function Remove-DistroNexusInstance {
    <#
    .SYNOPSIS
        Removes a WSL distribution instance.

    .DESCRIPTION
        Unregisters and optionally deletes the VHDX files of a WSL distribution.

    .PARAMETER Name
        The name of the WSL distribution to remove.

    .PARAMETER Force
        Skip confirmation prompt.

    .PARAMETER KeepFiles
        If specified, only unregisters but keeps VHDX files.

    .EXAMPLE
        Remove-DistroNexusInstance -Name "Ubuntu-Test"

    .EXAMPLE
        Remove-DistroNexusInstance -Name "Ubuntu-Test" -Force

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Name,
        
        [Parameter(Mandatory = $false)]
        [switch]$Force,
        
        [Parameter(Mandatory = $false)]
        [switch]$KeepFiles
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        # Validate instance exists
        $instance = Get-DistroNexusInstance -Name $Name | Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-DistroNexusLog "Instance not found: $Name" -Level ERROR
            return $false
        }
        
        if (-not $Force -and -not $PSCmdlet.ShouldProcess($Name, "Remove WSL instance")) {
            Write-DistroNexusLog "Operation cancelled by user" -Level WARN
            return $false
        }
        
        Write-DistroNexusLog "Removing WSL instance: $Name"
        
        $basePath = $instance.BasePath
        
        try {
            # Unregister
            $result = wsl --unregister $Name 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-DistroNexusLog "Successfully unregistered instance: $Name"
                
                # Update configuration
                try {
                    Get-DistroNexusInstance -ForceUpdate | Out-Null
                }
                catch {
                    Write-Verbose "Failed to update instance configuration: $_"
                }
                
                # Optionally delete files
                if (-not $KeepFiles -and $basePath -and (Test-Path $basePath)) {
                    Write-DistroNexusLog "Cleaning up files at: $basePath"
                    try {
                        Remove-Item -Path $basePath -Recurse -Force -ErrorAction Stop
                        Write-DistroNexusLog "Successfully removed files"
                    }
                    catch {
                        Write-DistroNexusLog "Failed to remove files: $_" -Level WARN
                    }
                }
                
                return $true
            }
            else {
                Write-DistroNexusLog "Failed to unregister instance: $result" -Level ERROR
                return $false
            }
        }
        catch {
            Write-DistroNexusLog "Exception while removing instance: $_" -Level ERROR
            Write-Error -Message $_.Exception.Message `
                        -ErrorId "DistroNexus.RemoveFailed" `
                        -Category OperationStopped `
                        -TargetObject $Name `
                        -ErrorAction Stop
            return $false
        }
    }
}
