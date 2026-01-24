
function Setup-Logger {
    param(
        [string]$LogFileName
    )
    $LocalLogDir = Join-Path $PSScriptRoot "..\logs"
    
    # Try to use local logs folder (Portable Mode preference), fallback to LocalAppData if permission denied (Installed Mode)
    try {
        if (-not (Test-Path $LocalLogDir)) { New-Item -ItemType Directory -Path $LocalLogDir -ErrorAction Stop | Out-Null }
        
        # Test write permission
        $TestFile = Join-Path $LocalLogDir "write_test.tmp"
        New-Item -Path $TestFile -ItemType File -Force -ErrorAction Stop | Out-Null
        Remove-Item $TestFile -Force
        
        $Global:LogDir = $LocalLogDir
    } catch {
        # Permission denied or readonly, fallback to %LocalAppData%
        $Global:LogDir = Join-Path $env:LOCALAPPDATA "DistroNexus\logs"
    }

    if (-not (Test-Path $Global:LogDir)) { New-Item -ItemType Directory -Path $Global:LogDir -Force | Out-Null }
    
    $Global:LogFile = Join-Path $Global:LogDir $LogFileName

    # Rotation Logic
    if (Test-Path $Global:LogFile) {
        try {
            $fileItem = Get-Item $Global:LogFile
            # Rotate if larger than 5MB
            if ($fileItem.Length -gt 5MB) {
                $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
                $backupName = "$($Global:LogFile).$timestamp.bak"
                Rename-Item -Path $Global:LogFile -NewName $backupName -ErrorAction Stop
                
                # Keep only last 5 backups
                $logNamePattern = "$([System.IO.Path]::GetFileNameWithoutExtension($LogFileName))*.bak"
                $backups = Get-ChildItem -Path $Global:LogDir -Filter $logNamePattern | Sort-Object CreationTime -Descending
                if ($backups.Count -gt 5) {
                    $backups | Select-Object -Skip 5 | Remove-Item -Force
                }
                
                # Create fresh log file
                New-Item -Path $Global:LogFile -ItemType File -Force | Out-Null
                Log-Message "Log rotated. Previous log archived as $backupName"
            }
        } catch {
            Write-Warning "Log rotation failed: $_"
        }
    }
}

function Log-Message {
    param(
        [string]$Message, 
        [string]$Level="INFO",
        [switch]$FileOnly
    )
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogLine = "[$Timestamp] [$Level] $Message"
    
    if ($Global:LogFile) {
        try {
            Add-Content -Path $Global:LogFile -Value $LogLine -ErrorAction SilentlyContinue
        } catch {
            # Ignore log write errors to prevent script crash
        }
    }

    if (-not $FileOnly) {
        if ($Level -eq "ERROR") {
            Write-Host $Message -ForegroundColor Red
        } elseif ($Level -eq "WARN") {
            Write-Host $Message -ForegroundColor Yellow
        } else {
            Write-Host $Message
        }
    }
}
