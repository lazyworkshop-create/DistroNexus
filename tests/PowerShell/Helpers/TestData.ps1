# Test Data Generator for DistroNexus Tests

<#
.SYNOPSIS
    Generate test WSL instance list output
#>
function Get-TestWslListOutput {
    param(
        [int]$InstanceCount = 3,
        [switch]$IncludeDefault
    )
    
    $instances = @(
        "Ubuntu-22.04 (Default)"
        "Debian"
        "Ubuntu-20.04"
        "Kali-Linux"
        "openSUSE-Leap-15.3"
    )
    
    $output = @()
    for ($i = 0; $i -lt [Math]::Min($InstanceCount, $instances.Count); $i++) {
        if ($i -eq 0 -and -not $IncludeDefault) {
            $output += $instances[$i] -replace " \(Default\)", ""
        } else {
            $output += $instances[$i]
        }
    }
    
    return $output -join "`n"
}

<#
.SYNOPSIS
    Generate test cache data
#>
function Get-TestCacheData {
    param(
        [int]$MinutesOld = 0,
        [int]$InstanceCount = 2
    )
    
    $instances = @()
    for ($i = 0; $i -lt $InstanceCount; $i++) {
        $instances += @{
            Name = "Ubuntu-$i"
            State = if ($i % 2 -eq 0) { "Running" } else { "Stopped" }
            Version = "2"
            BasePath = "C:\WSL\Ubuntu-$i"
            IsDefault = ($i -eq 0)
        }
    }
    
    return @{
        CachedAt = (Get-Date).AddMinutes(-$MinutesOld).ToString("o")
        Instances = $instances
        CacheVersion = "1.0"
    }
}

<#
.SYNOPSIS
    Generate test distro catalog
#>
function Get-TestDistroFile {
    param(
        [int]$DistroCount = 5,
        [switch]$IncludeReleaseVersions
    )
    
    $distros = @()
    
    # Ubuntu family
    $distros += @{
        Name = "Ubuntu 22.04 LTS"
        DistroId = "ubuntu-22.04"
        Family = "Ubuntu"
        Publisher = "Canonical"
        Version = "22.04"
        Architecture = "x64"
        PackageUrl = "https://cloud-images.ubuntu.com/releases/jammy/release/ubuntu-22.04-wsl-amd64-wsl.rootfs.tar.gz"
        PackageFormat = ".tar.gz"
        ReleaseDate = "2022-04-21"
        IsRelease = $IncludeReleaseVersions.IsPresent
    }
    
    $distros += @{
        Name = "Ubuntu 20.04 LTS"
        DistroId = "ubuntu-20.04"
        Family = "Ubuntu"
        Publisher = "Canonical"
        Version = "20.04"
        Architecture = "x64"
        PackageUrl = "https://cloud-images.ubuntu.com/releases/focal/release/ubuntu-20.04-wsl-amd64-wsl.rootfs.tar.gz"
        PackageFormat = ".tar.gz"
        ReleaseDate = "2020-04-23"
        IsRelease = $true
    }
    
    # Debian
    $distros += @{
        Name = "Debian 12 (Bookworm)"
        DistroId = "debian-12"
        Family = "Debian"
        Publisher = "Debian"
        Version = "12"
        Architecture = "x64"
        PackageUrl = "https://example.com/debian-12.tar.gz"
        PackageFormat = ".tar.gz"
        ReleaseDate = "2023-06-10"
        IsRelease = $true
    }
    
    # Kali Linux
    $distros += @{
        Name = "Kali Linux Rolling"
        DistroId = "kali-linux-rolling"
        Family = "Kali"
        Publisher = "Kali"
        Version = "rolling"
        Architecture = "x64"
        PackageUrl = "https://example.com/kali-rolling.tar.gz"
        PackageFormat = ".tar.gz"
        ReleaseDate = "2024-01-15"
        IsRelease = $false
    }
    
    # Arch Linux
    $distros += @{
        Name = "Arch Linux"
        DistroId = "arch-linux"
        Family = "Arch"
        Publisher = "Arch"
        Version = "current"
        Architecture = "x64"
        PackageUrl = "https://example.com/arch.tar.gz"
        PackageFormat = ".tar.gz"
        ReleaseDate = "2024-01-01"
        IsRelease = $true
    }
    
    return @{
        LastUpdated = (Get-Date).ToString("o")
        Version = "2.0"
        Distros = $distros[0..([Math]::Min($DistroCount, $distros.Count) - 1)]
    }
}

<#
.SYNOPSIS
    Generate test package file
#>
function New-TestPackageFile {
    param(
        [string]$OutputPath,
        [ValidateSet('.appx', '.tar.gz', '.zip')]
        [string]$Format = '.tar.gz',
        [int]$SizeKB = 100
    )
    
    $fileName = "test-distro$Format"
    $filePath = Join-Path $OutputPath $fileName
    
    # Create mock package file
    $content = "Mock package content " * ($SizeKB * 10)
    $content | Out-File -FilePath $filePath -Force
    
    return $filePath
}

<#
.SYNOPSIS
    Generate test terminal configuration
#>
function Get-TestTerminalConfig {
    return @{
        PreferredTerminal = "WindowsTerminal"
        FallbackTerminal = "cmd"
        TerminalPaths = @{
            WindowsTerminal = "C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.18.0.0_x64__8wekyb3d8bbwe\wt.exe"
            VSCode = "C:\Users\Test\AppData\Local\Programs\Microsoft VS Code\Code.exe"
            Cmd = "C:\Windows\System32\cmd.exe"
        }
    }
}

<#
.SYNOPSIS
    Create test JSON files in TestDrive
#>
function Initialize-TestDataFiles {
    param(
        [string]$TestDrivePath
    )
    
    # Create catalog file
    $catalogPath = Join-Path $TestDrivePath "catalog.json"
    Get-TestDistroFile -DistroCount 5 | ConvertTo-Json -Depth 5 | Set-Content $catalogPath
    
    # Create cache file (expired)
    $cachePath = Join-Path $TestDrivePath "cache"
    New-Item -Path $cachePath -ItemType Directory -Force | Out-Null
    
    $cacheFile = Join-Path $cachePath "instances.json"
    Get-TestCacheData -MinutesOld 15 -InstanceCount 3 | ConvertTo-Json -Depth 5 | Set-Content $cacheFile
    
    # Create terminal config
    $configPath = Join-Path $TestDrivePath "terminal-config.json"
    Get-TestTerminalConfig | ConvertTo-Json -Depth 5 | Set-Content $configPath
    
    return @{
        CatalogPath = $catalogPath
        CacheFile = $cacheFile
        ConfigPath = $configPath
        CachePath = $cachePath
    }
}

if ($ExecutionContext.SessionState.Module) {
    Export-ModuleMember -Function @(
        'Get-TestWslListOutput',
        'Get-TestCacheData',
        'Get-TestDistroFile',
        'New-TestPackageFile',
        'Get-TestTerminalConfig',
        'Initialize-TestDataFiles'
    )
}
