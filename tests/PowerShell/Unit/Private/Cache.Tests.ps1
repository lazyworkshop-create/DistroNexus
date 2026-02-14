# Cache.Tests.ps1
# Unit tests for Cache.ps1 Private functions

BeforeAll {
    # Import the module
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    
    # Import module
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    
    # Import private function using InModuleScope
    InModuleScope DistroNexus {
        # Module is now loaded in scope
    }
    
    # Import test helpers
    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")
}

Describe "Get-InstanceCache" -Tag 'Unit', 'Cache' {
    BeforeEach {
        # Create test environment
        $script:testCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $script:testCachePath -ItemType Directory -Force | Out-Null
    }
    
    Context "When cache file exists and is valid" {
        It "Should return cached instances" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheData = @{
                    CachedAt = (Get-Date).ToString("o")
                    InstanceCount = 2
                    Instances = @(
                        [PSCustomObject]@{
                            Name = "Ubuntu-22.04"
                            State = "Running"
                            Version = "2"
                        },
                        [PSCustomObject]@{
                            Name = "Debian"
                            State = "Stopped"
                            Version = "2"
                        }
                    )
                }
                
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 2
                $result[0].Name | Should -Be "Ubuntu-22.04"
                $result[1].Name | Should -Be "Debian"
            }
        }
        
        It "Should return instances when cache is less than 10 minutes old" {
            InModuleScope DistroNexus {
                # Arrange - cache 5 minutes old
                $cacheData = @{
                    CachedAt = (Get-Date).AddMinutes(-5).ToString("o")
                    InstanceCount = 1
                    Instances = @(
                        [PSCustomObject]@{ Name = "Ubuntu"; State = "Running"; Version = "2" }
                    )
                }
                
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 1
            }
        }
    }
    
    Context "When cache is expired" {
        It "Should return null when cache is older than 10 minutes" {
            InModuleScope DistroNexus {
                # Arrange - cache 15 minutes old
                $cacheData = @{
                    CachedAt = (Get-Date).AddMinutes(-15).ToString("o")
                    Instances = @()
                }
                
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cacheData | ConvertTo-Json | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
        
        It "Should return null when cache is exactly 11 minutes old" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheData = @{
                    CachedAt = (Get-Date).AddMinutes(-11).ToString("o")
                    Instances = @([PSCustomObject]@{ Name = "Test" })
                }
                
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
    }
    
    Context "When cache file does not exist" {
        It "Should return null" {
            InModuleScope DistroNexus {
                # Act - cache file not created
                $result = Get-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
    }
    
    Context "When cache file is corrupted" {
        It "Should return null and not throw" {
            InModuleScope DistroNexus {
                # Arrange - invalid JSON
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                "Invalid JSON content {{{" | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
    }
}

Describe "Set-InstanceCache" -Tag 'Unit', 'Cache' {
    BeforeEach {
        $script:testCachePath = Join-Path $TestDrive "cache"
    }
    
    Context "When caching instances" {
        It "Should create cache file with instance data" {
            InModuleScope DistroNexus {
                # Arrange
                $instances = @(
                    [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = "2" },
                    [PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = "2" }
                )
                
                # Act
                Set-InstanceCache -Instances $instances -CachePath $using:testCachePath
                
                # Assert
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cacheFile | Should -Exist
                
                $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $cached.InstanceCount | Should -Be 2
                $cached.Instances.Count | Should -Be 2
                $cached.Instances[0].Name | Should -Be "Ubuntu-22.04"
                $cached.CachedAt | Should -Not -BeNullOrEmpty
            }
        }
        
        It "Should create cache directory if it does not exist" {
            InModuleScope DistroNexus {
                # Arrange
                $newCachePath = Join-Path $TestDrive "new_cache_dir"
                $instances = @([PSCustomObject]@{ Name = "Test"; State = "Running"; Version = "2" })
                
                # Act
                Set-InstanceCache -Instances $instances -CachePath $newCachePath
                
                # Assert
                Test-Path $newCachePath | Should -BeTrue
                Test-Path (Join-Path $newCachePath "instances.json") | Should -BeTrue
            }
        }
        
        It "Should overwrite existing cache file" {
            InModuleScope DistroNexus {
                # Arrange
                $instances1 = @([PSCustomObject]@{ Name = "Ubuntu"; State = "Running"; Version = "2" })
                $instances2 = @([PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = "2" })
                
                # Act - first cache
                Set-InstanceCache -Instances $instances1 -CachePath $using:testCachePath
                Start-Sleep -Milliseconds 100
                
                # Act - second cache
                Set-InstanceCache -Instances $instances2 -CachePath $using:testCachePath
                
                # Assert
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $cached.Instances.Count | Should -Be 1
                $cached.Instances[0].Name | Should -Be "Debian"
            }
        }
        
        It "Should include timestamp in ISO 8601 format" {
            InModuleScope DistroNexus {
                # Arrange
                $instances = @([PSCustomObject]@{ Name = "Test"; State = "Running"; Version = "2" })
                
                # Act
                Set-InstanceCache -Instances $instances -CachePath $using:testCachePath
                
                # Assert
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
                
                # Parse timestamp to validate format
                { [DateTime]::Parse($cached.CachedAt) } | Should -Not -Throw
                $cached.CachedAt | Should -Match '^\d{4}-\d{2}-\d{2}T'
            }
        }
    }
    
    Context "When caching empty instance list" {
        It "Should create cache with zero instances" {
            InModuleScope DistroNexus {
                # Arrange
                $instances = @()
                
                # Act
                Set-InstanceCache -Instances $instances -CachePath $using:testCachePath
                
                # Assert
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                $cacheFile | Should -Exist
                
                $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $cached.InstanceCount | Should -Be 0
                $cached.Instances.Count | Should -Be 0
            }
        }
    }
}

Describe "Update-InstanceCache" -Tag 'Unit', 'Cache' {
    BeforeEach {
        $script:testCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $script:testCachePath -ItemType Directory -Force | Out-Null
    }
    
    Context "When updating cache" {
        It "Should remove existing cache file" {
            InModuleScope DistroNexus {
                # Arrange - create existing cache
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                @{ CachedAt = (Get-Date).ToString("o"); Instances = @() } | ConvertTo-Json | Set-Content $cacheFile
                
                # Act
                Update-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                Test-Path $cacheFile | Should -BeFalse
            }
        }
        
        It "Should not throw if cache file does not exist" {
            InModuleScope DistroNexus {
                # Act & Assert
                { Update-InstanceCache -CachePath $using:testCachePath } | Should -Not -Throw
            }
        }
    }
}

Describe "Clear-InstanceCache" -Tag 'Unit', 'Cache' {
    BeforeEach {
        $script:testCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $script:testCachePath -ItemType Directory -Force | Out-Null
    }
    
    Context "When clearing cache" {
        It "Should remove cache file" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheFile = Join-Path $using:testCachePath "instances.json"
                @{ CachedAt = (Get-Date).ToString("o"); Instances = @() } | ConvertTo-Json | Set-Content $cacheFile
                
                # Act
                Clear-InstanceCache -CachePath $using:testCachePath
                
                # Assert
                Test-Path $cacheFile | Should -BeFalse
            }
        }
        
        It "Should not throw if cache file does not exist" {
            InModuleScope DistroNexus {
                # Act & Assert
                { Clear-InstanceCache -CachePath $using:testCachePath } | Should -Not -Throw
            }
        }
    }
}
