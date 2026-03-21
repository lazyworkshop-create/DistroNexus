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
        $global:testCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $global:testCachePath -ItemType Directory -Force | Out-Null
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
                
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
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
                
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 1
            }
        }
    }
    
    Context "When cache contains historical timestamp" {
        It "Should return instances even when LastUpdated is older than 10 minutes" {
            InModuleScope DistroNexus {
                # Arrange - persistent config can be old and still valid
                $cacheData = @{
                    LastUpdated = (Get-Date).AddMinutes(-15).ToString("o")
                    InstanceCount = 1
                    Instances = @([PSCustomObject]@{ Name = "Ubuntu"; State = "Running"; Version = "2" })
                }
                
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                $cacheData | ConvertTo-Json | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 1
            }
        }
        
        It "Should return instances when LastUpdated is exactly 11 minutes old" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheData = @{
                    LastUpdated = (Get-Date).AddMinutes(-11).ToString("o")
                    InstanceCount = 1
                    Instances = @([PSCustomObject]@{ Name = "Test" })
                }
                
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 1
            }
        }
    }
    
    Context "When cache file does not exist" {
        It "Should return null" {
            InModuleScope DistroNexus {
                # Act - cache file not created
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
    }
    
    Context "When cache file is corrupted" {
        It "Should return null and not throw" {
            InModuleScope DistroNexus {
                # Arrange - invalid JSON
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                "Invalid JSON content {{{" | Set-Content $cacheFile -Force
                
                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                $result | Should -BeNullOrEmpty
            }
        }
    }
}

Describe "Set-InstanceCache" -Tag 'Unit', 'Cache' {
    BeforeEach {
        $global:testCachePath = Join-Path $TestDrive "cache"
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
                Set-InstanceCache -Instances $instances -CachePath $global:testCachePath
                
                # Assert
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                $cacheFile | Should -Exist
                
                $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $cached.InstanceCount | Should -Be 2
                $cached.Instances.Count | Should -Be 2
                $cached.Instances[0].Name | Should -Be "Ubuntu-22.04"
                $cached.LastUpdated | Should -Not -BeNullOrEmpty
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
                Set-InstanceCache -Instances $instances1 -CachePath $global:testCachePath
                Start-Sleep -Milliseconds 100
                
                # Act - second cache
                Set-InstanceCache -Instances $instances2 -CachePath $global:testCachePath
                
                # Assert
                $cacheFile = Join-Path $global:testCachePath "instances.json"
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
                Set-InstanceCache -Instances $instances -CachePath $global:testCachePath
                
                # Assert
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                $cached = Get-Content $cacheFile -Raw | ConvertFrom-Json
                
                # Parse timestamp to validate format
                { [DateTimeOffset]::Parse($cached.LastUpdated) } | Should -Not -Throw
                $cached.LastUpdated | Should -Not -BeNullOrEmpty
            }
        }
    }
    
    Context "When caching empty instance list" {
        It "Should reject empty instance array" {
            InModuleScope DistroNexus {
                # Arrange
                $instances = @()
                
                # Act & Assert
                { Set-InstanceCache -Instances $instances -CachePath $global:testCachePath } | Should -Throw
            }
        }
    }
}

Describe "Legacy cache migration" -Tag 'Unit', 'Cache' {
    BeforeEach {
        $global:testCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $global:testCachePath -ItemType Directory -Force | Out-Null
    }
    
    Context "When reading old cache schema" {
        It "Should migrate CachedAt to LastUpdated" {
            InModuleScope DistroNexus {
                # Arrange - old schema
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                @{
                    CachedAt = (Get-Date).ToString("o")
                    InstanceCount = 1
                    Instances = @([PSCustomObject]@{ Name = "Ubuntu"; State = "Running"; Version = "2" })
                } | ConvertTo-Json -Depth 5 | Set-Content $cacheFile
                
                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -Be 1
                $saved = Get-Content $cacheFile -Raw | ConvertFrom-Json
                $saved.PSObject.Properties.Name | Should -Contain 'LastUpdated'
                $saved.PSObject.Properties.Name | Should -Not -Contain 'CachedAt'
            }
        }
        
        It "Should preserve instance data during migration" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                @{
                    CachedAt = (Get-Date).ToString("o")
                    InstanceCount = 2
                    Instances = @(
                        [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = "2" },
                        [PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = "2" }
                    )
                } | ConvertTo-Json -Depth 5 | Set-Content $cacheFile

                # Act
                $result = Get-InstanceCache -CachePath $global:testCachePath

                # Assert
                $result.Count | Should -Be 2
                $result[0].Name | Should -Be "Ubuntu-22.04"
                $result[1].Name | Should -Be "Debian"
            }
        }
    }
}

Describe "Clear-InstanceCache" -Tag 'Unit', 'Cache' {
    BeforeEach {
        $global:testCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $global:testCachePath -ItemType Directory -Force | Out-Null
    }
    
    Context "When clearing cache" {
        It "Should remove cache file" {
            InModuleScope DistroNexus {
                # Arrange
                $cacheFile = Join-Path $global:testCachePath "instances.json"
                @{ CachedAt = (Get-Date).ToString("o"); Instances = @() } | ConvertTo-Json | Set-Content $cacheFile
                
                # Act
                Clear-InstanceCache -CachePath $global:testCachePath
                
                # Assert
                Test-Path $cacheFile | Should -BeFalse
            }
        }
        
        It "Should not throw if cache file does not exist" {
            InModuleScope DistroNexus {
                # Act & Assert
                { Clear-InstanceCache -CachePath $global:testCachePath } | Should -Not -Throw
            }
        }
    }
}

Describe "Cache TTL expiry" -Tag 'Unit', 'Cache' {
    It "returns stale=true when cache is older than 10 minutes" {
        InModuleScope DistroNexus {
            Set-DistroNexusCache -Timestamp (Get-Date).AddMinutes(-11)
            $result = Test-DistroNexusCacheStale
            $result | Should -Be $true
        }
    }
    It "returns stale=false when cache is under 10 minutes old" {
        InModuleScope DistroNexus {
            Set-DistroNexusCache -Timestamp (Get-Date).AddMinutes(-5)
            $result = Test-DistroNexusCacheStale
            $result | Should -Be $false
        }
    }
    It "returns stale=true when no timestamp has been set" {
        InModuleScope DistroNexus {
            $script:__CacheState.CacheTimestamp = $null
            $result = Test-DistroNexusCacheStale
            $result | Should -Be $true
        }
    }
}
