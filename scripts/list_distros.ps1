# PowerShell script to list WSL instances as JSON
# Used by GUI to populate Uninstall list

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Get-WslDistros {
    $LxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
    if (-not (Test-Path $LxssPath)) { return @() }
    
    # 1. Get Running State and Version
    $WslStatus = @{}
    # Force UTF-8 output from wsl.exe if possible or just parse
    $cliOutput = wsl --list --verbose
    if ($cliOutput) {
        foreach ($line in $cliOutput) {
            $line = $line -replace "`0", "" 
            if ($line -match "NAME") { continue }
            $cleanArgs = $line.Replace("*", " ").Trim() -split "\s+"
            if ($cleanArgs.Count -ge 3) {
                 $n = $cleanArgs[0]
                 $WslStatus[$n] = @{
                     State = $cleanArgs[1]
                     Version = $cleanArgs[2]
                 }
            }
        }
    }

    $Distros = @()
    $Keys = Get-ChildItem -Path $LxssPath

    foreach ($Key in $Keys) {
        $Props = Get-ItemProperty -Path $Key.PSPath
        
        $Name = $Props.DistributionName
        if (-not $Name) { continue }
        
        $BasePath = $Props.BasePath
        
        # Status Info
        $State = "Stopped"
        $WslVer = "?"
        if ($WslStatus.Contains($Name)) {
            $State = $WslStatus[$Name].State
            $WslVer = $WslStatus[$Name].Version
        }
        
        $Distros += [ordered]@{
            Name        = $Name
            BasePath    = $BasePath
            State       = $State
            WslVer      = $WslVer
        }
    }
    return $Distros
}

$data = Get-WslDistros
$data | ConvertTo-Json -Depth 2
