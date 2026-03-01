# Cache.Invalidation.Tests.ps1
# Unit tests for E-07 Proactive Cache Invalidation — Invalidate-InstanceCache and Get-DistroNexusCache

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Invalidate-InstanceCache (internal)" -Tag 'Unit', 'Private', 'Cache' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Cache invalidation" {
        It "Should be callable without error" {
            InModuleScope DistroNexus {
                # Invalidate-InstanceCache is an internal function; call via InModuleScope
                { Invalidate-InstanceCache -Reason "Test" } | Should -Not -Throw
            }
        }

        It "Should mark cache as stale after invalidation" {
            InModuleScope DistroNexus {
                Invalidate-InstanceCache -Reason "UnitTest"

                $state = Get-CacheInvalidationState
                $state.IsStale | Should -Be $true
                $state.LastReason | Should -Be "UnitTest"
            }
        }

        It "Should reset staleness flag after refresh" {
            InModuleScope DistroNexus {
                Invalidate-InstanceCache -Reason "Test"
                Reset-CacheInvalidationState

                $state = Get-CacheInvalidationState
                $state.IsStale | Should -Be $false
            }
        }
    }
}

Describe "Get-DistroNexusCache" -Tag 'Unit', 'Public', 'Cache' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should accept no parameters" {
            { Get-DistroNexusCache } | Should -Not -Throw
        }
    }

    Context "Cache diagnostics" {
        It "Should return object with Age, EntryCount, LastReason, IsStale properties" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusCache
                $result | Should -Not -BeNullOrEmpty
                $result.PSObject.Properties.Name -contains 'IsStale'    | Should -Be $true
                $result.PSObject.Properties.Name -contains 'EntryCount' | Should -Be $true
                $result.PSObject.Properties.Name -contains 'LastReason' | Should -Be $true
                $result.PSObject.Properties.Name -contains 'CacheAge'   | Should -Be $true
            }
        }

        It "Should show IsStale = true after invalidation" {
            InModuleScope DistroNexus {
                Invalidate-InstanceCache -Reason "CacheTest"
                $result = Get-DistroNexusCache
                $result.IsStale | Should -Be $true
            }
        }
    }
}
