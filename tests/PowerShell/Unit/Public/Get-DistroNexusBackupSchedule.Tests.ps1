# Get-DistroNexusBackupSchedule.Tests.ps1
# Unit tests for E-04 — Get backup schedule

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Get-DistroNexusBackupSchedule" -Tag 'Unit', 'Public', 'Backup' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null

        $scheduleFile = Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
        $testSchedules = @(
            @{ Name = "Ubuntu-22.04"; Destination = "C:\Backups"; Frequency = "Daily"; RetentionCount = 7; Time = "02:00:00" },
            @{ Name = "Debian"; Destination = "D:\Backups"; Frequency = "Weekly:Monday"; RetentionCount = 4; Time = "03:00:00" }
        )
        $testSchedules | ConvertTo-Json | Set-Content $scheduleFile
    }

    Context "Parameter validation" {
        It "Should have optional -Name parameter" {
            (Get-Command Get-DistroNexusBackupSchedule).Parameters.ContainsKey('Name') | Should -Be $true
        }
    }

    Context "Listing all schedules" {
        It "Should return all schedules when no Name is specified" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusBackupSchedule
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -BeGreaterOrEqual 2
            }
        }
    }

    Context "Filtering by name" {
        It "Should return only the matching schedule when -Name is specified" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusBackupSchedule -Name "Ubuntu-22.04"
                $result | Should -Not -BeNullOrEmpty
                $result.Name | Should -Be "Ubuntu-22.04"
            }
        }

        It "Should return empty when schedule not found" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusBackupSchedule -Name "NonExistent"
                $result | Should -BeNullOrEmpty
            }
        }
    }

    Context "Schedule object shape" {
        It "Should return objects with Name, Destination, Frequency, RetentionCount, Time properties" {
            InModuleScope DistroNexus {
                $result = Get-DistroNexusBackupSchedule -Name "Ubuntu-22.04"
                $result | Should -Not -BeNullOrEmpty
                $result.PSObject.Properties.Name -contains 'Name'          | Should -Be $true
                $result.PSObject.Properties.Name -contains 'Destination'   | Should -Be $true
                $result.PSObject.Properties.Name -contains 'Frequency'     | Should -Be $true
                $result.PSObject.Properties.Name -contains 'RetentionCount'| Should -Be $true
                $result.PSObject.Properties.Name -contains 'Time'          | Should -Be $true
            }
        }
    }

    Context "When no schedules file exists" {
        It "Should return empty without error" {
            InModuleScope DistroNexus {
                $scheduleFile = Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
                Remove-Item $scheduleFile -Force -ErrorAction SilentlyContinue

                $result = Get-DistroNexusBackupSchedule
                $result | Should -BeNullOrEmpty
            }
        }
    }
}
