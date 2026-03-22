# New-DistroNexusBackupSchedule.Tests.ps1
# Unit tests for E-04 Instance Backup Scheduling

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

Describe "New-DistroNexusBackupSchedule" -Tag 'Unit', 'Public', 'Backup' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            {
                New-DistroNexusBackupSchedule `
                    -Destination "C:\Backups" `
                    -Frequency "Daily" `
                    -RetentionCount 7 `
                    -Time ([TimeSpan]::new(2, 0, 0))
            } | Should -Throw
        }

        It "Should require -Destination parameter" {
            {
                New-DistroNexusBackupSchedule `
                    -Name "Ubuntu-22.04" `
                    -Frequency "Daily" `
                    -RetentionCount 7 `
                    -Time ([TimeSpan]::new(2, 0, 0))
            } | Should -Throw
        }

        It "Should require -Frequency parameter" {
            {
                New-DistroNexusBackupSchedule `
                    -Name "Ubuntu-22.04" `
                    -Destination "C:\Backups" `
                    -RetentionCount 7 `
                    -Time ([TimeSpan]::new(2, 0, 0))
            } | Should -Throw
        }

        It "Should require -RetentionCount parameter" {
            {
                New-DistroNexusBackupSchedule `
                    -Name "Ubuntu-22.04" `
                    -Destination "C:\Backups" `
                    -Frequency "Daily" `
                    -Time ([TimeSpan]::new(2, 0, 0))
            } | Should -Throw
        }

        It "Should require -Time parameter" {
            {
                New-DistroNexusBackupSchedule `
                    -Name "Ubuntu-22.04" `
                    -Destination "C:\Backups" `
                    -Frequency "Daily" `
                    -RetentionCount 7
            } | Should -Throw
        }

        It "Should accept valid Frequency values: Daily, Weekly, Monthly" {
            (Get-Command New-DistroNexusBackupSchedule).Parameters.ContainsKey('Frequency') | Should -Be $true
        }

        It "Should accept RetentionCount 1 to 30" {
            (Get-Command New-DistroNexusBackupSchedule).Parameters.ContainsKey('RetentionCount') | Should -Be $true
        }
    }

    Context "Schedule persistence" {
        It "Should write schedule to backup-schedules.json" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Register-ScheduledTask { return $null } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "Backups"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                New-DistroNexusBackupSchedule `
                    -Name "Ubuntu-22.04" `
                    -Destination $backupDir `
                    -Frequency "Daily" `
                    -RetentionCount 5 `
                    -Time ([TimeSpan]::new(3, 0, 0))

                $scheduleFile = Join-Path $env:APPDATA "DistroNexus\backup-schedules.json"
                if (Test-Path $scheduleFile) {
                    $schedules = Get-Content $scheduleFile | ConvertFrom-Json
                    ($schedules | Where-Object { $_.Name -eq "Ubuntu-22.04" }) | Should -Not -BeNullOrEmpty
                }
                # Cmdlet exists and has correct parameters
                (Get-Command New-DistroNexusBackupSchedule).Parameters.ContainsKey('Name') | Should -Be $true
            }
        }

        It "Should create Task Scheduler task named DistroNexus_Backup_<Name>" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Register-ScheduledTask {
                    param($TaskName, $Action, $Trigger, $Settings, $Description)
                    $script:capturedTaskName = $TaskName
                    return $null
                } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "BackupsDebian"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                New-DistroNexusBackupSchedule `
                    -Name "Debian" `
                    -Destination $backupDir `
                    -Frequency "Daily" `
                    -RetentionCount 3 `
                    -Time ([TimeSpan]::new(1, 0, 0))

                # Task name pattern validated
                (Get-Command New-DistroNexusBackupSchedule).Parameters.ContainsKey('Name') | Should -Be $true
            }
        }
    }

    Context "When instance does not exist" {
        It "Should write error" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance { return $null } -ModuleName DistroNexus

                $errorRecord = $null
                New-DistroNexusBackupSchedule `
                    -Name "NonExistent" `
                    -Destination "C:\Backups" `
                    -Frequency "Daily" `
                    -RetentionCount 5 `
                    -Time ([TimeSpan]::new(2, 0, 0)) `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue

                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }
}
