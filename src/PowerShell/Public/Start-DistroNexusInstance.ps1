function Start-DistroNexusInstance {
    <#
    .SYNOPSIS
        Starts a WSL distribution instance.

    .DESCRIPTION
        Starts the specified WSL distribution in the background.

    .PARAMETER Name
        The name of the WSL distribution to start.

    .EXAMPLE
        Start-DistroNexusInstance -Name "Ubuntu-22.04"

    .OUTPUTS
        Boolean indicating success or failure
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Name
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        Write-DistroNexusLog "Starting WSL instance: $Name"
        
        try {
            # Start the distribution in background
            $result = wsl -d $Name --exec echo "started" 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-DistroNexusLog "Successfully started instance: $Name"
                
                # Update configuration to refresh instance state
                try {
                    Get-DistroNexusInstance -Name $Name -SkipDiskSize -ForceUpdate | Out-Null
                }
                catch {
                    Write-Verbose "Failed to update instance configuration: $_"
                }
                
                return $true
            }
            else {
                Write-DistroNexusLog "Failed to start instance: $Name - $result" -Level ERROR
                return $false
            }
        }
        catch {
            Write-DistroNexusLog "Exception while starting instance: $_" -Level ERROR
            Write-Error -Message $_.Exception.Message `
                        -ErrorId "DistroNexus.StartFailed" `
                        -Category OperationStopped `
                        -TargetObject $Name `
                        -ErrorAction Stop
            return $false
        }
    }
}
