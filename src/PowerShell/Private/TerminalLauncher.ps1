function Find-TerminalPath {
    <#
    .SYNOPSIS
        Locates available terminal applications on the system.

    .DESCRIPTION
        Internal helper function to detect and return paths to available terminal applications.
        Checks for Windows Terminal (wt.exe) and Command Prompt (cmd.exe).
        Returns the preferred terminal based on availability.

    .PARAMETER PreferredTerminal
        Preferred terminal to use: "WindowsTerminal", "CMD", or "Auto" (default).
        Auto will prefer Windows Terminal if available, otherwise fallback to CMD.

    .EXAMPLE
        $terminal = Find-TerminalPath
        # Returns path to preferred terminal

    .EXAMPLE
        $terminal = Find-TerminalPath -PreferredTerminal "CMD"
        # Forces CMD usage

    .OUTPUTS
        PSCustomObject with properties: Path, Type, Arguments
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false)]
        [ValidateSet('Auto', 'WindowsTerminal', 'CMD')]
        [string]$PreferredTerminal = 'Auto'
    )
    
    # Windows Terminal detection
    $wtPath = $null
    try {
        $wtCommand = Get-Command wt.exe -ErrorAction SilentlyContinue
        if ($wtCommand) {
            $wtPath = $wtCommand.Source
            Write-Verbose "Windows Terminal found: $wtPath"
        }
    }
    catch {
        Write-Verbose "Windows Terminal not found"
    }
    
    # CMD detection (always available on Windows)
    $cmdPath = Join-Path $env:SystemRoot "System32\cmd.exe"
    if (-not (Test-Path $cmdPath)) {
        # Fallback path
        $cmdPath = "cmd.exe"
    }
    Write-Verbose "Command Prompt found: $cmdPath"
    
    # Determine which terminal to use
    switch ($PreferredTerminal) {
        'WindowsTerminal' {
            if ($wtPath) {
                return [PSCustomObject]@{
                    Path = $wtPath
                    Type = 'WindowsTerminal'
                    DisplayName = 'Windows Terminal'
                }
            }
            else {
                Write-Warning "Windows Terminal not found, falling back to CMD"
                return [PSCustomObject]@{
                    Path = $cmdPath
                    Type = 'CMD'
                    DisplayName = 'Command Prompt'
                }
            }
        }
        
        'CMD' {
            return [PSCustomObject]@{
                Path = $cmdPath
                Type = 'CMD'
                DisplayName = 'Command Prompt'
            }
        }
        
        'Auto' {
            if ($wtPath) {
                return [PSCustomObject]@{
                    Path = $wtPath
                    Type = 'WindowsTerminal'
                    DisplayName = 'Windows Terminal'
                }
            }
            else {
                return [PSCustomObject]@{
                    Path = $cmdPath
                    Type = 'CMD'
                    DisplayName = 'Command Prompt'
                }
            }
        }
    }
}

function Invoke-Terminal {
    <#
    .SYNOPSIS
        Launches a terminal window with the specified WSL instance.

    .DESCRIPTION
        Internal helper function to open a terminal (Windows Terminal or CMD)
        and start a WSL distribution within it.

    .PARAMETER InstanceName
        Name of the WSL instance to start in the terminal.

    .PARAMETER StartPath
        Optional starting directory inside the WSL instance.
        If not specified, starts in the user's home directory.

    .PARAMETER PreferredTerminal
        Preferred terminal to use: "WindowsTerminal", "CMD", or "Auto" (default).

    .PARAMETER NoWait
        Returns immediately without waiting for the terminal to close.
        Default is $true (don't wait).

    .EXAMPLE
        Invoke-Terminal -InstanceName "Ubuntu-22.04"
        # Opens terminal with Ubuntu

    .EXAMPLE
        Invoke-Terminal -InstanceName "Debian" -StartPath "/var/www"
        # Opens terminal and navigates to /var/www

    .EXAMPLE
        Invoke-Terminal -InstanceName "Ubuntu" -PreferredTerminal "CMD"
        # Forces CMD usage

    .OUTPUTS
        Boolean indicating success.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstanceName,
        
        [Parameter(Mandatory = $false)]
        [string]$StartPath,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet('Auto', 'WindowsTerminal', 'CMD')]
        [string]$PreferredTerminal = 'Auto',
        
        [Parameter(Mandatory = $false)]
        [bool]$NoWait = $true
    )
    
    if (-not $PSCmdlet.ShouldProcess($InstanceName, "Launch terminal")) {
        return $false
    }
    
    try {
        $terminal = Find-TerminalPath -PreferredTerminal $PreferredTerminal
        Write-DistroNexusLog "Launching $($terminal.DisplayName) for instance: $InstanceName" -FileOnly
        
        # Build command arguments based on terminal type
        $arguments = @()
        
        switch ($terminal.Type) {
            'WindowsTerminal' {
                # Windows Terminal arguments: wt -w 0 wsl -d <name>
                # -w 0 opens in existing window (window 0)
                $arguments += '-w', '0'
                
                if ($StartPath) {
                    # Use wsl with --cd parameter
                    $arguments += 'wsl', '-d', $InstanceName, '--cd', $StartPath
                }
                else {
                    $arguments += 'wsl', '-d', $InstanceName
                }
                
                Write-Verbose "Windows Terminal command: $($terminal.Path) $($arguments -join ' ')"
            }
            
            'CMD' {
                # CMD arguments: cmd /k wsl -d <name>
                # /k keeps the window open after command execution
                $arguments += '/k'
                
                if ($StartPath) {
                    # WSL command with cd
                    $wslCommand = "wsl -d $InstanceName --cd `"$StartPath`""
                }
                else {
                    $wslCommand = "wsl -d $InstanceName"
                }
                
                $arguments += $wslCommand
                Write-Verbose "CMD command: $($terminal.Path) $($arguments -join ' ')"
            }
        }
        
        # Start the terminal process
        $processParams = @{
            FilePath = $terminal.Path
            ArgumentList = $arguments
        }
        
        if ($NoWait) {
            $processParams['PassThru'] = $true
        }
        else {
            $processParams['Wait'] = $true
        }
        
        $process = Start-Process @processParams
        
        if ($NoWait -and $process) {
            Write-Verbose "Terminal launched with PID: $($process.Id)"
        }
        
        Write-DistroNexusLog "Terminal launched successfully for $InstanceName" -FileOnly
        return $true
    }
    catch {
        Write-DistroNexusLog "Failed to launch terminal for ${InstanceName}: $_" -Level ERROR
        throw
    }
}

function Test-TerminalAvailable {
    <#
    .SYNOPSIS
        Tests if a terminal application is available.

    .DESCRIPTION
        Internal helper function to check if Windows Terminal is installed.

    .PARAMETER TerminalType
        Type of terminal to check: "WindowsTerminal" or "CMD".

    .EXAMPLE
        if (Test-TerminalAvailable -TerminalType "WindowsTerminal") {
            # Windows Terminal is available
        }

    .OUTPUTS
        Boolean indicating if the terminal is available.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('WindowsTerminal', 'CMD')]
        [string]$TerminalType
    )
    
    switch ($TerminalType) {
        'WindowsTerminal' {
            try {
                $null = Get-Command wt.exe -ErrorAction Stop
                return $true
            }
            catch {
                return $false
            }
        }
        
        'CMD' {
            # CMD is always available on Windows
            $cmdPath = Join-Path $env:SystemRoot "System32\cmd.exe"
            return (Test-Path $cmdPath)
        }
    }
    
    return $false
}

function Get-AvailableTerminals {
    <#
    .SYNOPSIS
        Gets a list of all available terminal applications.

    .DESCRIPTION
        Internal helper function to enumerate all terminal applications
        available on the system.

    .EXAMPLE
        $terminals = Get-AvailableTerminals
        # Returns array of available terminals

    .OUTPUTS
        Array of PSCustomObject with properties: Type, Path, DisplayName, Available
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param()
    
    $terminals = @()
    
    # Check Windows Terminal
    $wtAvailable = Test-TerminalAvailable -TerminalType 'WindowsTerminal'
    $wtPath = if ($wtAvailable) {
        (Get-Command wt.exe -ErrorAction SilentlyContinue).Source
    }
    else {
        $null
    }
    
    $terminals += [PSCustomObject]@{
        Type = 'WindowsTerminal'
        Path = $wtPath
        DisplayName = 'Windows Terminal'
        Available = $wtAvailable
    }
    
    # Check CMD
    $cmdPath = Join-Path $env:SystemRoot "System32\cmd.exe"
    $terminals += [PSCustomObject]@{
        Type = 'CMD'
        Path = $cmdPath
        DisplayName = 'Command Prompt'
        Available = (Test-Path $cmdPath)
    }
    
    return $terminals
}
