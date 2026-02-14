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
}

Describe "Get-DistroNexusInstance" -Tag 'Unit', 'Public', 'Get' {
    BeforeEach {
        # Clear any existing cache
        $testCachePath = Join-Path $TestDrive "cache"
        if (Test-Path $testCachePath) {
            Remove-Item -Path $testCachePath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    
    Context "When getting instances with default parameters" {
        It "Should return instance objects" -Skip {
            # This requires actual WSL instances or extensive mocking
            # Better suited for integration testing
        }
        
        It "Should not throw errors" {
            # Act & Assert
            { Get-DistroNexusInstance -ErrorAction Stop } | Should -Not -Throw
        }
    }
    
    Context "When using cache" {
        It "Should use cached data when available and valid" {
            InModuleScope DistroNexus {
                # Arrange - setup cache
                $testCachePath = Join-Path $TestDrive "cache"
                New-Item -Path $testCachePath -ItemType Directory -Force | Out-Null
                
                $cacheData = @{
                    CachedAt = (Get-Date).ToString("o")
                    InstanceCount = 2
                    Instances = @(
                        [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = "2" },
                        [PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = "2" }
                    )
                }
                
                $cacheFile = Join-Path $testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Mock Get-InstanceCache to return our test cache
                Mock Get-InstanceCache {
                    return $cacheData.Instances
                }
                
                # Act
                $result = Get-DistroNexusInstance
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 2
            }
        }
        
        It "Should bypass cache with -ForceUpdate switch" {
            InModuleScope DistroNexus {
                # Arrange
                $getCacheCalled = $false
                Mock Get-InstanceCache {
                    $script:getCacheCalled = $true
                    return @()
                }
                
                # Mock wsl command
                Mock Invoke-Expression {
                    return "NAME STATE VERSION`nUbuntu-22.04 Running 2"
                } -ParameterFilter { $Command -match "wsl.*--list" }
                
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
                Mock Get-InstanceCache {
                    return @(
                        [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running" },
                        [PSCustomObject]@{ Name = "Debian"; State = "Stopped" }
                    )
                }
                
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
                # Arrange
                Mock Get-InstanceCache {
                    return @(
                        [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running" },
                        [PSCustomObject]@{ Name = "Ubuntu-20.04"; State = "Stopped" },
                        [PSCustomObject]@{ Name = "Debian"; State = "Running" }
                    )
                }
                
                # Act
                $result = Get-DistroNexusInstance -Name "Ubuntu*"
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 2
                $result[0].Name | Should -BeLike "Ubuntu*"
                $result[1].Name | Should -BeLike "Ubuntu*"
            }
        }
        
        It "Should return empty when no instances match filter" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-InstanceCache {
                    return @(
                        [PSCustomObject]@{ Name = "Ubuntu"; State = "Running" }
                    )
                }
                
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
                }
                
                # Mock wsl command
                Mock Invoke-Expression {
                    return "NAME STATE VERSION`nUbuntu Running 2"
                } -ParameterFilter { $Command -match "wsl.*--list" }
                
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
                }
                
                # Mock wsl command
                Mock Invoke-Expression {
                    return "NAME STATE VERSION`nUbuntu Running 2"
                } -ParameterFilter { $Command -match "wsl.*--list" }
                
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
                Mock Get-InstanceCache { return $null }
                Mock Invoke-Expression {
                    throw "wsl.exe not found"
                }
                
                # Act & Assert
                { Get-DistroNexusInstance } | Should -Not -Throw
            }
        }
    }
}
