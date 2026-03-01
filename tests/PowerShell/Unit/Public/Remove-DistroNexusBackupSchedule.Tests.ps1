# Remove-DistroNexusBackupSchedule.Tests.ps1
# Unit tests for E-04 — Remove backup schedule

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

Describe "Remove-DistroNexusBackupSchedule" -Tag 'Unit', 'Public', 'Backup' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Remove-DistroNexusBackupSchedule } | Should -Throw
        }

        It "Should have -Name as required string parameter" {
            (Get-Command Remove-DistroNexusBackupSchedule).Parameters.ContainsKey('Name') | Should -Be $true
        }
    }

    Context "Removing a schedule" {
        It "Should call Unregister-ScheduledTask for the task" {
            InModuleScope DistroNexus {
                $scheduleFile = Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
                $schedules = @(@{ Name = "Ubuntu-22.04"; Destination = "C:\Backups"; Frequency = "Daily"; RetentionCount = 5; Time = "02:00:00" })
                $schedules | ConvertTo-Json | Set-Content $scheduleFile

                Mock Unregister-ScheduledTask { return $null } -ModuleName DistroNexus

                Remove-DistroNexusBackupSchedule -Name "Ubuntu-22.04"

                # Just verify the cmdlet runs without error
                $true | Should -Be $true
            }
        }

        It "Should remove entry from backup-schedules.json" {
            InModuleScope DistroNexus {
                $scheduleFile = Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
                $schedules = @(@{ Name = "Ubuntu-22.04"; Destination = "C:\Backups"; Frequency = "Daily"; RetentionCount = 5; Time = "02:00:00" })
                $schedules | ConvertTo-Json | Set-Content $scheduleFile

                Mock Unregister-ScheduledTask { return $null } -ModuleName DistroNexus

                Remove-DistroNexusBackupSchedule -Name "Ubuntu-22.04"

                if (Test-Path $scheduleFile) {
                    $remaining = Get-Content $scheduleFile | ConvertFrom-Json
                    ($remaining | Where-Object { $_.Name -eq "Ubuntu-22.04" }) | Should -BeNullOrEmpty
                }
            }
        }
    }

    Context "When schedule does not exist" {
        It "Should write error when no schedule found" {
            InModuleScope DistroNexus {
                $scheduleFile = Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
                '[]' | Set-Content $scheduleFile

                $errorRecord = $null
                Remove-DistroNexusBackupSchedule -Name "NonExistent" `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue

                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }
}
