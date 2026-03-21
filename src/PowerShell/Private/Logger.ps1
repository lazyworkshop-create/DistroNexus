function Initialize-DistroNexusLogger {
    <#
    .SYNOPSIS
        Initializes the logging system for DistroNexus module.

    .DESCRIPTION
        Sets up log directory and file with automatic rotation.
        Tries local logs folder first (portable mode), falls back to LocalAppData.

    .PARAMETER LogFileName
        Name of the log file. Defaults to 'distronexus.log'.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$LogFileName = 'distronexus.log'
    )
    
    # Standard Mode: Always use Roaming AppData for logs
    $script:LogDir = Join-Path $env:APPDATA "DistroNexus\logs"
    Write-Verbose "Logging to: $script:LogDir"
    
    if (-not (Test-Path $script:LogDir)) {
        New-Item -ItemType Directory -Path $script:LogDir -Force | Out-Null
    }
    
    $script:LogFile = Join-Path $script:LogDir $LogFileName
    
    # Rotation logic
    if (Test-Path $script:LogFile) {
        try {
            $fileItem = Get-Item $script:LogFile
            if ($fileItem.Length -gt 5MB) {
                $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
                $backupName = "$($script:LogFile).$timestamp.bak"
                Rename-Item -Path $script:LogFile -NewName $backupName -ErrorAction Stop
                
                # Keep only last 5 backups
                $logNamePattern = "$([System.IO.Path]::GetFileNameWithoutExtension($LogFileName))*.bak"
                $backups = Get-ChildItem -Path $script:LogDir -Filter $logNamePattern | Sort-Object CreationTime -Descending
                if ($backups.Count -gt 5) {
                    $backups | Select-Object -Skip 5 | Remove-Item -Force
                }
                
                New-Item -Path $script:LogFile -ItemType File -Force | Out-Null
                Write-DistroNexusLog "Log rotated. Previous log archived as $backupName"
            }
        }
        catch {
            Write-Warning "Log rotation failed: $_"
        }
    }
}

function Write-DistroNexusLog {
    <#
    .SYNOPSIS
        Writes log messages to file and console with automatic rotation.

    .DESCRIPTION
        Internal logging function for DistroNexus module. Handles log file rotation
        when size exceeds 5MB and maintains up to 5 backup files.

    .PARAMETER Message
        The message to log.

    .PARAMETER Level
        Log level: INFO, WARN, or ERROR. Default is INFO.

    .PARAMETER FileOnly
        If specified, writes only to file without console output.

    .EXAMPLE
        Write-DistroNexusLog "Installation completed successfully"

    .EXAMPLE
        Write-DistroNexusLog "Failed to connect" -Level ERROR
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet('INFO', 'WARN', 'ERROR')]
        [string]$Level = 'INFO',
        
        [Parameter(Mandatory = $false)]
        [switch]$FileOnly,

        [Parameter(Mandatory = $false)]
        [string]$ErrorCode
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $errorCodePart = if ($ErrorCode) { " [$ErrorCode]" } else { "" }
    $logLine = "[$timestamp] [$Level]$errorCodePart $Message"
    
    # Get or initialize log file path
    if (-not $script:LogFile) {
        Initialize-DistroNexusLogger
    }
    
    if ($script:LogFile) {
        try {
            Add-Content -Path $script:LogFile -Value $logLine -ErrorAction SilentlyContinue
        }
        catch {
            # Silently ignore log write errors to prevent script crash
        }
    }
    
    # Console output
    if (-not $FileOnly) {
        switch ($Level) {
            'ERROR' { Write-Host $Message -ForegroundColor Red }
            'WARN'  { Write-Host $Message -ForegroundColor Yellow }
            default { Write-Host $Message }
        }
    }
}