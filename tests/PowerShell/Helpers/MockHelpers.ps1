# Mock Helper Functions for DistroNexus Tests

<#
.SYNOPSIS
    Mock wsl.exe command execution
#>
function Mock-WslCommand {
    param(
        [string]$ListOutput,
        [string]$StatusOutput,
        [int]$ExitCode = 0
    )
    
    Mock Invoke-Expression {
        param($Command)
        
        if ($Command -match 'wsl\.exe.*--list') {
            return $ListOutput
        }
        elseif ($Command -match 'wsl\.exe.*--status') {
            return $StatusOutput
        }
        
        return ""
    } -ModuleName DistroNexus
}

<#
.SYNOPSIS
    Mock Invoke-WebRequest for download testing
#>
function Mock-WebRequest {
    param(
        [string]$TestFilePath,
        [int]$StatusCode = 200,
        [hashtable]$Headers = @{}
    )
    
    Mock Invoke-WebRequest {
        param($Uri, $OutFile, $Headers)
        
        if ($StatusCode -ne 200) {
            throw "HTTP $StatusCode error"
        }
        
        # Create mock file if OutFile specified
        if ($OutFile) {
            "Mock downloaded content" | Out-File -FilePath $OutFile -Force
        }
        
        return [PSCustomObject]@{
            StatusCode = $StatusCode
            Headers = $Headers
            Content = "Mock content"
        }
    } -ModuleName DistroNexus
}

<#
.SYNOPSIS
    Mock registry access for distro detection
#>
function Mock-RegistryAccess {
    param(
        [hashtable]$RegistryData = @{}
    )
    
    Mock Get-ItemProperty {
        param($Path, $Name)
        
        $key = "$Path\$Name"
        if ($RegistryData.ContainsKey($key)) {
            return $RegistryData[$key]
        }
        
        return $null
    } -ModuleName DistroNexus
}

<#
.SYNOPSIS
    Mock Get-DistroNexusConfig for test isolation
#>
function Mock-DistroNexusConfig {
    param(
        [string]$CachePath,
        [string]$DataPath,
        [string]$CatalogPath,
        [int]$CacheExpirationMinutes = 10
    )
    
    Mock Get-DistroNexusConfig {
        return @{
            CachePath = $CachePath
            DataPath = $DataPath
            CatalogPath = $CatalogPath
            CacheExpirationMinutes = $CacheExpirationMinutes
        }
    } -ModuleName DistroNexus
}

<#
.SYNOPSIS
    Create mock WSL instance data
#>
function New-MockWslInstance {
    param(
        [string]$Name = "Ubuntu-22.04",
        [string]$State = "Running",
        [string]$Version = "2",
        [string]$BasePath = "C:\WSL\Ubuntu"
    )
    
    return [PSCustomObject]@{
        Name = $Name
        State = $State
        Version = $Version
        BasePath = $BasePath
        IsDefault = $false
    }
}

<#
.SYNOPSIS
    Create mock distro catalog entry
#>
function New-MockDistroEntry {
    param(
        [string]$Name = "Ubuntu 22.04 LTS",
        [string]$DistroId = "ubuntu-22.04",
        [string]$Publisher = "Canonical",
        [string]$Version = "22.04",
        [string]$Architecture = "x64",
        [string]$PackageUrl = "https://example.com/ubuntu.appx",
        [string]$PackageFormat = ".appx"
    )
    
    return [PSCustomObject]@{
        Name = $Name
        DistroId = $DistroId
        Publisher = $Publisher
        Version = $Version
        Architecture = $Architecture
        PackageUrl = $PackageUrl
        PackageFormat = $PackageFormat
        ReleaseDate = (Get-Date).ToString("yyyy-MM-dd")
    }
}

<#
.SYNOPSIS
    Setup common test environment
#>
function Initialize-TestEnvironment {
    param(
        [string]$TestDrivePath
    )
    
    # Create standard test directories
    $cachePath = Join-Path $TestDrivePath "cache"
    $dataPath = Join-Path $TestDrivePath "data"
    $catalogPath = Join-Path $dataPath "catalog.json"
    
    New-Item -Path $cachePath -ItemType Directory -Force | Out-Null
    New-Item -Path $dataPath -ItemType Directory -Force | Out-Null
    
    # Create mock catalog
    $mockCatalog = @{
        LastUpdated = (Get-Date).ToString("o")
        Distros = @(
            @{
                Name = "Ubuntu 22.04 LTS"
                DistroId = "ubuntu-22.04"
                Publisher = "Canonical"
                Version = "22.04"
                Architecture = "x64"
                PackageUrl = "https://example.com/ubuntu.appx"
                PackageFormat = ".appx"
            }
        )
    }
    
    $mockCatalog | ConvertTo-Json -Depth 5 | Set-Content $catalogPath
    
    return @{
        CachePath = $cachePath
        DataPath = $dataPath
        CatalogPath = $catalogPath
    }
}

<#
.SYNOPSIS
    Cleanup test environment
#>
function Clear-TestEnvironment {
    param(
        [string]$TestDrivePath
    )
    
    if (Test-Path $TestDrivePath) {
        Remove-Item -Path $TestDrivePath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($ExecutionContext.SessionState.Module) {
    Export-ModuleMember -Function @(
        'Mock-WslCommand',
        'Mock-WebRequest',
        'Mock-RegistryAccess',
        'Mock-DistroNexusConfig',
        'New-MockWslInstance',
        'New-MockDistroEntry',
        'Initialize-TestEnvironment',
        'Clear-TestEnvironment'
    )
}
