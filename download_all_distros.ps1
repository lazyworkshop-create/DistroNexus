# PowerShell script to download all available WSL2 distro packages
# Author: GitHub Copilot

$DistroCatalog = [ordered]@{
    "1" = @{
        Name = "Ubuntu"
        Versions = [ordered]@{
            "1" = @{ Name = "Ubuntu 24.04 LTS"; Url = "https://wslstorestorage.blob.core.windows.net/wslblob/Ubuntu2404-240425.AppxBundle"; Filename = "Ubuntu-24.04.AppxBundle" }
            "2" = @{ Name = "Ubuntu 22.04 LTS"; Url = "https://aka.ms/wslubuntu2204"; Filename = "Ubuntu-22.04.AppxBundle" }
            "3" = @{ Name = "Ubuntu 20.04 LTS"; Url = "https://aka.ms/wslubuntu2004"; Filename = "Ubuntu-20.04.AppxBundle" }
            "4" = @{ Name = "Ubuntu 18.04 LTS"; Url = "https://aka.ms/wsl-ubuntu-1804"; Filename = "Ubuntu-18.04.AppxBundle" }
        }
    }
    "2" = @{
        Name = "Debian"
        Versions = [ordered]@{
            "1" = @{ Name = "Debian GNU/Linux"; Url = "https://aka.ms/wsl-debian-gnulinux"; Filename = "Debian.appx" }
        }
    }
    "3" = @{
        Name = "Kali Linux"
        Versions = [ordered]@{
            "1" = @{ Name = "Kali Linux Rolling"; Url = "https://aka.ms/wsl-kali-linux-new"; Filename = "Kali.appx" }
        }
    }
    "4" = @{
        Name = "Oracle Linux"
        Versions = [ordered]@{
            "1" = @{ Name = "Oracle Linux 9.1"; Url = "https://publicwsldistros.blob.core.windows.net/wsldistrostorage/OracleLinux_9.1-230428.Appx"; Filename = "OracleLinux-9.1.appx" }
            "2" = @{ Name = "Oracle Linux 8.7"; Url = "https://publicwsldistros.blob.core.windows.net/wsldistrostorage/OracleLinux_8.7-230428.Appx"; Filename = "OracleLinux-8.7.appx" }
        }
    }
}

$BaseDir = Join-Path $PWD "distro"

Write-Host "=== WSL Distro Downloader ===" -ForegroundColor Cyan
Write-Host "Base Directory: $BaseDir"
Write-Host "============================`n"

foreach ($FamilyKey in $DistroCatalog.Keys) {
    $Family = $DistroCatalog[$FamilyKey]
    
    foreach ($VerKey in $Family.Versions.Keys) {
        $Version = $Family.Versions[$VerKey]
        
        # Organization: distro/FamilyName/VersionName/File
        $TargetDir = Join-Path $BaseDir $Family.Name
        $TargetDir = Join-Path $TargetDir $Version.Name
        
        if (-not (Test-Path $TargetDir)) {
            New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
        }

        $OutFile = Join-Path $TargetDir $Version.Filename
        
        Write-Host "[$($Family.Name)] $($Version.Name)" -NoNewline

        if (Test-Path $OutFile) {
            Write-Host " -> Skipped (Already exists)" -ForegroundColor Yellow
        } else {
            Write-Host " -> Downloading..." -ForegroundColor Green
            try {
                Invoke-WebRequest -Uri $Version.Url -OutFile $OutFile -UseBasicParsing -Verbose
            } catch {
                Write-Host " -> Failed: $_" -ForegroundColor Red
            }
        }
    }
}

Write-Host "`nAll downloads completed." -ForegroundColor Cyan
if (Test-Path $BaseDir) { Invoke-Item $BaseDir }
