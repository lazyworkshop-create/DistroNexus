function Stop-DistroNexusInstance {
    <#
    .SYNOPSIS
        Stops a running WSL distribution instance.

    .DESCRIPTION
        Terminates the specified WSL distribution. Optionally prompts for confirmation.

    .PARAMETER Name
        The name of the WSL distribution to stop.

    .PARAMETER Force
        Skip confirmation prompt.

    .EXAMPLE
        Stop-DistroNexusInstance -Name "Ubuntu-22.04"

    .EXAMPLE
        Stop-DistroNexusInstance -Name "Ubuntu-22.04" -Force

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Name,
        
        [Parameter(Mandatory = $false)]
        [switch]$Force
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        # Check if instance is running
        $instance = Get-DistroNexusInstance -Name $Name | Where-Object { $_.Name -eq $Name }
        if (-not $instance) {
            Write-DistroNexusLog "Instance not found: $Name" -Level ERROR
            return $false
        }
        
        if ($instance.State -eq "Stopped") {
            Write-DistroNexusLog "Instance is already stopped: $Name" -Level WARN
            return $true
        }
        
        # Confirm action unless -Force specified
        if (-not $Force -and -not $PSCmdlet.ShouldProcess($Name, "Stop WSL instance")) {
            Write-DistroNexusLog "Operation cancelled by user" -Level WARN
            return $false
        }
        
        Write-DistroNexusLog "Stopping WSL instance: $Name"
        
        try {
            $result = wsl --terminate $Name 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-DistroNexusLog "Successfully stopped instance: $Name"
                return $true
            }
            else {
                Write-DistroNexusLog "Failed to stop instance: $Name - $result" -Level ERROR
                return $false
            }
        }
        catch {
            Write-DistroNexusLog "Exception while stopping instance: $_" -Level ERROR
            return $false
        }
    }
}
