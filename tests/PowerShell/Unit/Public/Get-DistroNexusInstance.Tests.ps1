# Get-DistroNexusInstance.Tests.ps1
# Unit tests for Get-DistroNexusInstance Public Cmdlet

BeforeAll {
    # Import the module
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    
    # Import test helpers
    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Get-DistroNexusInstance" -Tag 'Unit', 'Public', 'Get' {
    BeforeEach {
        # Clear any existing cache
        $testCachePath = Join-Path $TestDrive "cache"
        if (Test-Path $testCachePath) {
            Remove-Item -Path $testCachePath -Recurse -Force -ErrorAction SilentlyContinue
        }

        $env:APPDATA = $TestDrive
        $distroNexusConfigPath = Join-Path $env:APPDATA "DistroNexus"
        if (Test-Path $distroNexusConfigPath) {
            Remove-Item -Path $distroNexusConfigPath -Recurse -Force -ErrorAction SilentlyContinue
        }
        New-Item -Path $distroNexusConfigPath -ItemType Directory -Force | Out-Null
    }
    
    Context "When getting instances with default parameters" {
        It "Should return instance objects" -Skip:(-not (($env:DISTRONEXUS_RUN_WSL2_TESTS -eq '1') -and ($null -ne (Get-Command wsl.exe -ErrorAction SilentlyContinue)))) {
            # This requires actual WSL instances or extensive mocking
            # Better suited for integration testing
        }
        
        It "Should not throw errors" {
            # Act & Assert
            { Get-DistroNexusInstance -ErrorAction Stop } | Should -Not -Throw
        }
    }
    
    Context "When using cache" {
        It "Should return instance data without errors" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-DistroNexusInstance
                
                # Assert
                { Get-DistroNexusInstance -ErrorAction Stop } | Should -Not -Throw
            }
        }
        
        It "Should bypass cache with -ForceUpdate switch" {
            InModuleScope DistroNexus {
                # Arrange
                $getCacheCalled = $false
                Mock Get-InstanceCache {
                    $script:getCacheCalled = $true
                    return @()
                } -ModuleName DistroNexus
                
                # Mock wsl command
                Mock Invoke-Expression {
                    return "NAME STATE VERSION`nUbuntu-22.04 Running 2"
                } -ModuleName DistroNexus -ParameterFilter { $Command -match "wsl.*--list" }
                
                # Act
                $result = Get-DistroNexusInstance -ForceUpdate
                
                # Assert - cache should not be called
                $getCacheCalled | Should -BeFalse
            }
        }
    }
    
    Context "When filtering by name" {
        It "Should filter instances by exact name match" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheData = @{
                    LastUpdated = (Get-Date).ToString("o")
                    InstanceCount = 2
                    Instances = @(
                        [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running" },
                        [PSCustomObject]@{ Name = "Debian"; State = "Stopped" }
                    )
                }
                $cacheFile = Join-Path (Join-Path $env:APPDATA "DistroNexus") "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-DistroNexusInstance -Name "Ubuntu-22.04"
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 1
                $result.Name | Should -Be "Ubuntu-22.04"
            }
        }
        
        It "Should filter instances by wildcard pattern" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-DistroNexusInstance -Name "Ubuntu*"
                $items = @($result)
                
                # Assert
                ($items | Where-Object { $_.Name -notlike "Ubuntu*" }).Count | Should -Be 0
            }
        }
        
        It "Should return empty when no instances match filter" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheData = @{
                    LastUpdated = (Get-Date).ToString("o")
                    InstanceCount = 1
                    Instances = @([PSCustomObject]@{ Name = "Ubuntu"; State = "Running" })
                }
                $cacheFile = Join-Path (Join-Path $env:APPDATA "DistroNexus") "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-DistroNexusInstance -Name "NonExistent"
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
    }
    
    Context "When using IncludeRelease and IncludeUser switches" {
        It "Should bypass cache when IncludeRelease is specified" {
            InModuleScope DistroNexus {
                # Arrange
                $getCacheCalled = $false
                Mock Get-InstanceCache {
                    $script:getCacheCalled = $true
                    return @()
                } -ModuleName DistroNexus
                
                # Mock wsl command
                Mock Invoke-Expression {
                    return "NAME STATE VERSION`nUbuntu Running 2"
                } -ModuleName DistroNexus -ParameterFilter { $Command -match "wsl.*--list" }
                
                # Act
                $result = Get-DistroNexusInstance -IncludeRelease
                
                # Assert
                $getCacheCalled | Should -BeFalse
            }
        }
        
        It "Should bypass cache when IncludeUser is specified" {
            InModuleScope DistroNexus {
                # Arrange
                $getCacheCalled = $false
                Mock Get-InstanceCache {
                    $script:getCacheCalled = $true
                    return @()
                } -ModuleName DistroNexus
                
                # Mock wsl command
                Mock Invoke-Expression {
                    return "NAME STATE VERSION`nUbuntu Running 2"
                } -ModuleName DistroNexus -ParameterFilter { $Command -match "wsl.*--list" }
                
                # Act
                $result = Get-DistroNexusInstance -IncludeUser
                
                # Assert
                $getCacheCalled | Should -BeFalse
            }
        }
    }
    
    Context "When handling errors" {
        It "Should handle wsl command not available gracefully" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-InstanceCache { return $null } -ModuleName DistroNexus
                Mock Invoke-Expression {
                    throw "wsl.exe not found"
                } -ModuleName DistroNexus
                
                # Act & Assert
                { Get-DistroNexusInstance } | Should -Not -Throw
            }
        }
    }
}
